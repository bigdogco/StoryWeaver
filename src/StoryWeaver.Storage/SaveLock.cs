using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StoryWeaver.Storage;

/// <summary>
/// Exclusive ownership of one save directory, for as long as a session is playing it.
///
/// <b>Written after two CLI instances played the same save at once and nobody noticed.</b> A
/// 250-turn run had 72 duplicated turn numbers: two processes each loaded canon at turn N,
/// both wrote N+1, and each overwrote the other's world every turn for a hundred turns. It
/// completed without an error, produced a log of the right shape, and was only caught because
/// the prose felt wrong and someone read the timestamps.
///
/// That silence is the whole argument for this class. A loud failure gets avoided twice; a
/// silent one costs a long run, and long runs are how this project learns.
///
/// <b>Deliberately not on <c>IWorldRepository</c>.</b> The turn engine has no business knowing
/// about locks. A lock belongs to a *session*, which has a beginning and an end; a repository
/// does not.
///
/// <b>Not a guard against hand-editing.</b> A text editor takes no lock, and canon is meant to
/// be opened and edited — that is what re-reading state on demand is for. This stops two
/// *engines* writing, which is the case where both believe they are authoritative.
/// </summary>
public sealed class SaveLock : IDisposable
{
    public const string FileName = ".session.lock";

    private readonly string _path;
    private bool _released;

    private SaveLock(string path) => _path = path;

    /// <summary>
    /// Take the lock for <paramref name="saveId"/>, or return null if another live session
    /// holds it — in which case <paramref name="heldBy"/> describes the holder, for a refusal
    /// message worth reading.
    ///
    /// A lock whose process is gone is stale and taken silently: a crash must never leave a
    /// save permanently unopenable. <paramref name="force"/> takes a lock held by a process
    /// that really is alive, which is the escape hatch for the day this detection is wrong.
    /// </summary>
    public static SaveLock? Acquire(string root, string saveId, bool force, out string? heldBy)
    {
        heldBy = null;

        string directory = Path.Combine(root, saveId);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, FileName);

        if (Read(path) is { } holder && !force)
        {
            // Our own id is not a conflict. A session that somehow re-acquires its own lock is
            // taking back something it already owns.
            if (holder.ProcessId != Environment.ProcessId && IsAlive(holder))
            {
                heldBy = holder.Describe();
                return null;
            }
        }

        Write(path);
        return new SaveLock(path);
    }

    /// <summary>
    /// Give the save up. Safe to call twice, and safe if the file is already gone — a lock
    /// that cannot be cleaned up must not take the session down with it on the way out.
    /// </summary>
    public void Dispose()
    {
        if (_released)
        {
            return;
        }

        _released = true;

        try
        {
            // Only remove a lock that is still ours. If --force handed the save to somebody
            // else while we were running, deleting on exit would strip *their* lock.
            if (Read(_path) is { } holder && holder.ProcessId == Environment.ProcessId)
            {
                File.Delete(_path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void Write(string path)
    {
        using Process self = Process.GetCurrentProcess();

        Holder holder = new()
        {
            ProcessId = Environment.ProcessId,
            StartedUtc = self.StartTime.ToUniversalTime(),
            Machine = Environment.MachineName,
            OpenedUtc = DateTime.UtcNow,
        };

        File.WriteAllText(path, JsonSerializer.Serialize(holder, Options));
    }

    private static Holder? Read(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Holder>(File.ReadAllText(path), Options)
                : null;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // An unreadable lock is treated as no lock. The alternative — refusing to open a
            // save because a stray file will not parse — fails in the direction that costs the
            // player their world.
            return null;
        }
    }

    /// <summary>
    /// Is the process named by this lock still running?
    ///
    /// <b>Process ids are reused</b>, so the id alone would eventually match some unrelated
    /// program and refuse a save forever. Comparing start time as well is what makes "is this
    /// still the session that wrote the lock" answerable rather than a guess.
    /// </summary>
    private static bool IsAlive(Holder holder)
    {
        if (!string.Equals(holder.Machine, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            // Another machine's process cannot be inspected from here. Treated as live: a
            // shared directory is out of scope, and guessing "dead" would be the unsafe guess.
            return true;
        }

        try
        {
            using Process other = Process.GetProcessById(holder.ProcessId);

            // One second of slack: the recorded time round-trips through JSON and the two
            // clocks are the same clock, so anything larger is a different process wearing a
            // recycled id.
            return Math.Abs((other.StartTime.ToUniversalTime() - holder.StartedUtc).TotalSeconds) < 1;
        }
        catch (ArgumentException)
        {
            // No process with that id: the session is gone and the lock is stale.
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private sealed record Holder
    {
        public int ProcessId { get; init; }

        public DateTime StartedUtc { get; init; }

        public string Machine { get; init; } = string.Empty;

        public DateTime OpenedUtc { get; init; }

        public string Describe() =>
            $"process {ProcessId} on {Machine}, opened {OpenedUtc:yyyy-MM-dd HH:mm:ss} UTC";
    }
}
