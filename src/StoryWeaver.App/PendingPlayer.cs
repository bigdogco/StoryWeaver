using StoryWeaver.Core;
using StoryWeaver.Llm.OpenRouter;
using StoryWeaver.Storage;

namespace StoryWeaver.App;

/// <summary>
/// A session that is loaded and waiting on one answer: who is the player?
///
/// <b>Why this exists as a state rather than a callback.</b> Opening a session is otherwise a
/// pure sequence, but a pack that ships no <c>player.md</c> has to ask. A callback would invert
/// control and make a window block inside a load; this stops instead, hands back everything
/// already done, and lets the client ask however suits it — a console prompt, a dialog, a form
/// on a page.
///
/// <b>The save lock is already held.</b> That is the point of holding the loaded state rather
/// than starting over: nobody else can take the save while the question is on screen. It also
/// means an abandoned question must give the lock back, which is why this is
/// <see cref="IDisposable"/>.
/// </summary>
public sealed class PendingPlayer : IDisposable
{
    private readonly string _saveId;
    private readonly string _packId;
    private readonly WorldState _world;
    private readonly TurnEngine _engine;
    private readonly JsonWorldRepository _repository;
    private readonly WorldPack _pack;
    private readonly SaveLock _sessionLock;
    private readonly OpenRouterClient _client;
    private readonly string _saveRoot;

    private bool _spent;

    internal PendingPlayer(
        string saveId,
        string packId,
        WorldState world,
        TurnEngine engine,
        JsonWorldRepository repository,
        WorldPack pack,
        SaveLock sessionLock,
        OpenRouterClient client,
        string saveRoot)
    {
        _saveId = saveId;
        _packId = packId;
        _world = world;
        _engine = engine;
        _repository = repository;
        _pack = pack;
        _sessionLock = sessionLock;
        _client = client;
        _saveRoot = saveRoot;
    }

    /// <summary>
    /// The character the pack seeded for the player, so a client can show what it is about to
    /// overwrite — and so a description left blank keeps whatever the seed already said.
    /// </summary>
    public Character? Player => _world.Player;

    /// <summary>
    /// Name the player and open the session.
    ///
    /// <paramref name="description"/> is optional: blank keeps whatever the seed supplied, which
    /// matters because the narrator reads it and a pack may have written something better than
    /// a hurried sentence at a prompt.
    ///
    /// The name is not optional. Everyone in the world uses it, and an empty one produces a
    /// narrator addressing a character with no name.
    /// </summary>
    public async Task<StorySession> CompleteAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_spent, this);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The player needs a name — everyone in the world uses it.", nameof(name));
        }

        if (_world.Player is { } player)
        {
            player.Name = name.Trim();

            if (!string.IsNullOrWhiteSpace(description))
            {
                player.Description = description.Trim();
            }
        }

        // Marked before the write, so a failure part-way cannot leave this reusable — the lock
        // would then be disposed twice by two different owners.
        _spent = true;

        return await SessionOpener.FinishAsync(
            _saveId, _packId, _world, _engine, _repository, _pack, _sessionLock, _client, _saveRoot,
            resumed: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Give the save back without opening it — the player closed the dialog, or pressed Ctrl-C
    /// at the prompt. Harmless once <see cref="CompleteAsync"/> has succeeded, because the
    /// session owns the lock from that point.
    /// </summary>
    public void Dispose()
    {
        if (_spent)
        {
            return;
        }

        _spent = true;
        _sessionLock.Dispose();
        _client.Dispose();
    }
}
