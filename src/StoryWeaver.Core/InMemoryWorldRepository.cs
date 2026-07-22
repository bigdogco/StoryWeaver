namespace StoryWeaver.Core;

/// <summary>
/// Non-persisting <see cref="IWorldRepository"/>. Everything is lost on exit.
///
/// Written before the JSON implementation on purpose. The interface has to survive a
/// second implementation to be trusted not to leak storage details, and writing the JSON
/// one first would have shaped the interface around it — after which "exposes no
/// JSON-specific types" verifies itself and means nothing.
///
/// Also genuinely useful: it makes the turn loop runnable while the save format is still
/// moving, which is the whole reason section 7 comes before section 6.
/// </summary>
public sealed class InMemoryWorldRepository : IWorldRepository
{
    private readonly Dictionary<string, WorldState> _worlds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TurnRecord>> _history = new(StringComparer.OrdinalIgnoreCase);

    public Task<WorldState?> LoadAsync(string worldId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_worlds.TryGetValue(worldId, out WorldState? state) ? state : null);

    public Task SaveAsync(string worldId, WorldState state, CancellationToken cancellationToken = default)
    {
        // No copy is taken. The caller holds the same graph this hands back, so a mutation
        // after "saving" is silently persisted — which a file-backed store would not do.
        // Acceptable because the turn loop treats save as a commit point and does not touch
        // state afterwards, but it is the one behaviour here that is not representative.
        _worlds[worldId] = state;
        return Task.CompletedTask;
    }

    public Task AppendTurnAsync(
        string worldId,
        TurnRecord turn,
        CancellationToken cancellationToken = default)
    {
        if (!_history.TryGetValue(worldId, out List<TurnRecord>? turns))
        {
            turns = [];
            _history[worldId] = turns;
        }

        turns.Add(turn);
        return Task.CompletedTask;
    }

    public Task ReplaceLastTurnAsync(
        string worldId,
        TurnRecord turn,
        CancellationToken cancellationToken = default)
    {
        if (_history.TryGetValue(worldId, out List<TurnRecord>? turns) && turns.Count > 0)
        {
            turns[^1] = turn;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TurnRecord>> LoadHistoryAsync(
        string worldId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TurnRecord> turns = _history.TryGetValue(worldId, out List<TurnRecord>? found)
            ? found.AsReadOnly()
            : [];

        return Task.FromResult(turns);
    }

    public Task<IReadOnlyList<string>> ListWorldsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. _worlds.Keys]);
}
