namespace StoryWeaver.Core;

/// <summary>
/// Persistence for worlds. Deliberately narrow, and deliberately free of any hint about how
/// the data is stored — the JSON implementation is expected to be replaced by a hybrid
/// (JSON canon, SQLite turn log) once full-text search over history is wanted, and that
/// swap is only cheap if nothing here leaks a storage detail.
///
/// Canon and turn history are separate methods rather than one blob. They have opposite
/// shapes: canon is small, bounded, and rewritten whole; history grows without limit and is
/// only ever appended to. Storing them together would mean rewriting the entire session
/// history on every turn, and would block moving the log to a different store later.
/// </summary>
public interface IWorldRepository
{
    /// <summary>Load a world's canon, or null if no world exists under that id.</summary>
    Task<WorldState?> LoadAsync(string worldId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist a world's canon, replacing what is there.
    ///
    /// Implementations must make this atomic: a crash partway through must leave the
    /// previous save intact rather than a truncated file. A half-written world is worse than
    /// a slightly stale one, because it is not obvious from the outside that it happened.
    /// </summary>
    Task SaveAsync(string worldId, WorldState state, CancellationToken cancellationToken = default);

    /// <summary>Append one completed turn to the world's history.</summary>
    Task AppendTurnAsync(string worldId, TurnRecord turn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Overwrite the most recent turn.
    ///
    /// The one exception to history being append-only, and deliberately narrow: re-running
    /// extraction over prose the player has already read repairs that turn rather than
    /// creating a second one, because the story did not happen twice. Appending instead would
    /// duplicate the narration in the log — and therefore in the narrator's memory window,
    /// which is built from it.
    ///
    /// Does nothing when there is no history.
    /// </summary>
    Task ReplaceLastTurnAsync(string worldId, TurnRecord turn, CancellationToken cancellationToken = default);

    /// <summary>Read back turn history, oldest first.</summary>
    Task<IReadOnlyList<TurnRecord>> LoadHistoryAsync(
        string worldId,
        CancellationToken cancellationToken = default);

    /// <summary>Ids of every world that can be loaded.</summary>
    Task<IReadOnlyList<string>> ListWorldsAsync(CancellationToken cancellationToken = default);
}
