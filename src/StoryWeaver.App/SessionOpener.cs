using StoryWeaver.Core;
using StoryWeaver.Llm;
using StoryWeaver.Llm.Configuration;
using StoryWeaver.Llm.Logging;
using StoryWeaver.Llm.OpenRouter;
using StoryWeaver.Llm.Story;
using StoryWeaver.Storage;

namespace StoryWeaver.App;

/// <summary>
/// Turns a pack id and a save id into a playable <see cref="StorySession"/>.
///
/// <b>Why this project exists.</b> Opening a session needs Storage (the lock, the pack, the
/// repository, the save origin), Llm (prompts, the provider client, the narrator and extractor)
/// and Core. <c>Core</c> references nothing, and Llm and Storage are siblings that cannot see
/// each other — so <i>no existing project could hold this sequence</i>, which is exactly why it
/// lived in the console. <c>StoryWeaver.App</c> is the composition layer: it knows how to
/// assemble a playable session out of the three libraries, and both clients use it.
///
/// <b>What it is not.</b> It renders nothing and asks nothing. Every decision comes back as
/// data on <see cref="SessionOpening"/>, because a window makes the same decisions and shows
/// them differently.
///
/// <b>Why opening is two-phase.</b> Twelve of the thirteen steps are a pure sequence. One is
/// not: when a pack ships no <c>player.md</c>, somebody has to be asked who they are. Rather
/// than take a callback — which inverts control and makes a UI block inside a load — opening
/// stops and hands back a <see cref="PendingPlayer"/> with everything already loaded and the
/// save lock already held. The client asks in whatever way suits it and completes.
/// </summary>
public static class SessionOpener
{
    public const string DefaultSaveRoot = "saves";

    public const string DefaultPackRoot = "worlds";

    /// <param name="packId">Which world. Content.</param>
    /// <param name="saveId">
    /// Which playthrough. State. Null follows the pack, which is what you want unless you are
    /// deliberately keeping two playthroughs of one world apart — <b>two packs sharing one save
    /// is not a configuration, it is a corruption</b>, since the ids written by one world do not
    /// exist in the other.
    /// </param>
    /// <param name="force">Open a save whose lock is still held. See <see cref="SaveLock"/>.</param>
    public static async Task<SessionOpening> OpenAsync(
        StoryWeaverSettings settings,
        string packId,
        string? saveId = null,
        bool force = false,
        string saveRoot = DefaultSaveRoot,
        string packRoot = DefaultPackRoot,
        CancellationToken cancellationToken = default)
    {
        string pack = packId.Trim();
        string save = string.IsNullOrWhiteSpace(saveId) ? pack : saveId.Trim();

        // Taken before anything is read. Two engines writing one save corrupt it silently, so
        // this refuses rather than warns.
        SaveLock? sessionLock = SaveLock.Acquire(saveRoot, save, force, out string? heldBy);

        if (sessionLock is null)
        {
            return SessionOpening.Refused($"the save '{save}' is already open in another session", heldBy);
        }

        // From here the lock is held, so every failure path has to give it back. A leaked lock
        // makes a save unopenable until somebody deletes a dotfile they do not know about.
        try
        {
            return await OpenLockedAsync(
                settings, pack, save, sessionLock, saveRoot, packRoot, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // The client is created inside, so anything that throws before the session takes
            // ownership disposes only what exists here.
            sessionLock.Dispose();
            throw;
        }
    }

    private static async Task<SessionOpening> OpenLockedAsync(
        StoryWeaverSettings settings,
        string packId,
        string saveId,
        SaveLock sessionLock,
        string saveRoot,
        string packRoot,
        CancellationToken cancellationToken)
    {
        FileLlmLog log = new(settings.Logging);
        OpenRouterClient client = new(settings, log);

        // Authored content and engine prompts, both loaded once and both loud on failure. A
        // silently dropped lore entry or a missing prompt is the failure this genre is worst at.
        WorldPack pack = WorldPack.Load(packRoot, packId);
        PromptLibrary prompts = PromptLibrary.Load();

        JsonWorldRepository repository = new(saveRoot);
        int historyTurns = settings.Story.HistoryTurns;

        // The wiring a second client would otherwise have to reproduce exactly — and the pack's
        // voice is the argument that would go missing, which reads as the model being worse
        // rather than as a dropped parameter.
        TurnEngine engine = new(
            new LlmNarrator(client, prompts, pack.Voice),
            new LlmStateExtractor(client, prompts),
            repository,
            historyTurns,
            pack.Lore,
            pack.Sheets,
            pack.Scenario,
            pack.Opening);

        WorldState? loaded = await repository.LoadAsync(saveId, cancellationToken).ConfigureAwait(false);
        bool resumed = loaded is not null;

        // No WorldSeeds fallback any more. Every pack ships a seed, and the built-in fixture it
        // used to fall back to is eval scaffolding — instrumentation does not belong in the
        // path that opens a real playthrough.
        WorldState world = loaded ?? pack.Seed
            ?? throw new InvalidDataException(
                $"The pack '{packId}' ships no seed.json, so there is no world to start from.");

        SessionContext context = new(
            pack, prompts, log.FilePath, repository.RootDirectory, resumed, world.TurnNumber, historyTurns)
        {
            PackVersionAtStart = PackVersionIfMoved(pack, Path.Combine(saveRoot, saveId)),
        };

        // The one question this cannot answer on its own. Everything above is already done, so
        // completing costs a single write.
        if (!resumed && !pack.AuthorsThePlayer)
        {
            return SessionOpening.AwaitingPlayer(
                new PendingPlayer(saveId, packId, world, engine, repository, pack, sessionLock, client, saveRoot),
                context);
        }

        StorySession session = await FinishAsync(
            saveId, packId, world, engine, repository, pack, sessionLock, client, saveRoot, resumed,
            cancellationToken).ConfigureAwait(false);

        return SessionOpening.Opened(session, context);
    }

    /// <summary>
    /// The tail both paths share: write the first save if this is a new world, record what it
    /// began against, and hand canon to the session.
    ///
    /// <b>Order matters.</b> The save is written before <see cref="SaveOrigin"/> so the
    /// directory exists to write the origin into.
    /// </summary>
    internal static async Task<StorySession> FinishAsync(
        string saveId,
        string packId,
        WorldState world,
        TurnEngine engine,
        JsonWorldRepository repository,
        WorldPack pack,
        SaveLock sessionLock,
        OpenRouterClient client,
        string saveRoot,
        bool resumed,
        CancellationToken cancellationToken)
    {
        if (!resumed)
        {
            await repository.SaveAsync(saveId, world, cancellationToken).ConfigureAwait(false);
        }

        SaveOrigin.WriteIfAbsent(Path.Combine(saveRoot, saveId), pack.Id, pack.Version);

        // Both live exactly as long as the session: the lock says the save is ours, and the
        // client is the socket the turn engine narrates through.
        return new StorySession(
            saveId, packId, world, engine, repository, pack.Lore, [sessionLock, client]);
    }

    /// <summary>
    /// The version this save began against, when the pack on disk has moved since — otherwise
    /// null. Absent on every save made before manifests existed, which is why a missing origin
    /// is not a problem.
    /// </summary>
    private static string? PackVersionIfMoved(WorldPack pack, string saveDirectory)
    {
        if (SaveOrigin.Read(saveDirectory) is not { } origin
            || string.IsNullOrWhiteSpace(origin.PackVersion)
            || string.IsNullOrWhiteSpace(pack.Version)
            || string.Equals(origin.PackVersion, pack.Version, StringComparison.Ordinal))
        {
            return null;
        }

        return origin.PackVersion;
    }
}
