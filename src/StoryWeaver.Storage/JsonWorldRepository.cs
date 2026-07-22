using System.Text.Json;
using StoryWeaver.Core;

namespace StoryWeaver.Storage;

/// <summary>
/// File-backed <see cref="IWorldRepository"/>. One directory per world under a save root:
///
/// <code>
/// {root}/{worldId}/canon.json      the WorldState, rewritten whole each turn
/// {root}/{worldId}/history.jsonl   one TurnRecord per line, append-only
/// </code>
///
/// The split mirrors the interface's own reasoning: canon is small and rewritten whole;
/// history grows without bound and is only appended to. Keeping history as JSONL means a
/// turn is one file append, never a read-modify-write of the whole log — which also keeps
/// the eventual move of the log to SQLite an isolated change.
/// </summary>
public sealed class JsonWorldRepository : IWorldRepository
{
    private const string CanonFile = "canon.json";
    private const string HistoryFile = "history.jsonl";

    private readonly string _root;

    /// <param name="rootDirectory">Directory under which world folders live. Created lazily
    /// on first save; does not need to exist yet.</param>
    public JsonWorldRepository(string rootDirectory)
    {
        _root = Path.GetFullPath(rootDirectory);
    }

    /// <summary>Absolute path of the save root, for showing the user where saves go.</summary>
    public string RootDirectory => _root;

    public async Task<WorldState?> LoadAsync(
        string worldId,
        CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(WorldDirectory(worldId), CanonFile);

        if (!File.Exists(path))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<WorldState>(json, SaveJson.Canon)
               ?? throw new InvalidDataException($"Canon file for world '{worldId}' deserialized to null: {path}");
    }

    public async Task SaveAsync(
        string worldId,
        WorldState state,
        CancellationToken cancellationToken = default)
    {
        string directory = WorldDirectory(worldId);
        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, CanonFile);
        string temp = path + ".tmp";

        string json = JsonSerializer.Serialize(state, SaveJson.Canon);
        await File.WriteAllTextAsync(temp, json, cancellationToken).ConfigureAwait(false);

        // The atomicity the interface requires: the previous canon.json is replaced in one
        // move, so a crash leaves either the old save or the new one, never a half-written
        // file. The temp file is a sibling so the move stays on one volume.
        File.Move(temp, path, overwrite: true);
    }

    public async Task AppendTurnAsync(
        string worldId,
        TurnRecord turn,
        CancellationToken cancellationToken = default)
    {
        string directory = WorldDirectory(worldId);
        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, HistoryFile);
        string line = JsonSerializer.Serialize(turn, SaveJson.History);
        await File.AppendAllTextAsync(path, line + "\n", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rewrites the log with its final entry replaced.
    ///
    /// The whole file is rewritten rather than the last line truncated in place: JSONL lines
    /// vary in length, so seeking back would risk leaving a fragment of the old record behind.
    /// Written through the same temp-and-move as canon, because a half-rewritten history is
    /// exactly as bad as a half-written save. Rare enough that the cost does not matter.
    /// </summary>
    public async Task ReplaceLastTurnAsync(
        string worldId,
        TurnRecord turn,
        CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(WorldDirectory(worldId), HistoryFile);

        if (!File.Exists(path))
        {
            return;
        }

        List<TurnRecord> turns = [.. await LoadHistoryAsync(worldId, cancellationToken).ConfigureAwait(false)];

        if (turns.Count == 0)
        {
            return;
        }

        turns[^1] = turn;

        string temp = path + ".tmp";
        string body = string.Concat(turns.Select(t => JsonSerializer.Serialize(t, SaveJson.History) + "\n"));
        await File.WriteAllTextAsync(temp, body, cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
    }

    public async Task<IReadOnlyList<TurnRecord>> LoadHistoryAsync(
        string worldId,
        CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(WorldDirectory(worldId), HistoryFile);

        if (!File.Exists(path))
        {
            return [];
        }

        List<TurnRecord> turns = [];

        foreach (string raw in await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            TurnRecord? turn;
            try
            {
                turn = JsonSerializer.Deserialize<TurnRecord>(raw, SaveJson.History);
            }
            catch (JsonException)
            {
                // A crash mid-append can leave a truncated final line. Treat the first
                // unparseable line as the end of the good log rather than failing the load —
                // losing the last partial turn is recoverable, refusing to load the world is
                // not.
                break;
            }

            if (turn is not null)
            {
                turns.Add(turn);
            }
        }

        return turns;
    }

    public Task<IReadOnlyList<string>> ListWorldsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_root))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        IReadOnlyList<string> worlds =
        [
            .. Directory.EnumerateDirectories(_root)
                .Where(d => File.Exists(Path.Combine(d, CanonFile)))
                .Select(d => Path.GetFileName(d)!)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        return Task.FromResult(worlds);
    }

    private string WorldDirectory(string worldId) => Path.Combine(_root, worldId);
}
