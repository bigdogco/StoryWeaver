using StoryWeaver.App;
using StoryWeaver.Core;
using StoryWeaver.Llm.Configuration;
using StoryWeaver.Storage;

namespace StoryWeaver.Harness;

/// <summary>
/// Offline checks on <see cref="SessionOpener"/> — the thirteen decisions that used to be the
/// console's private business.
///
/// These touch the real filesystem, under a temporary root that is deleted afterwards, because
/// most of what opening does *is* filesystem behaviour: taking a lock, writing the first save,
/// recording what a playthrough began against, noticing the pack has moved. A fake repository
/// would test none of it.
///
/// No API calls: opening constructs the provider client but never uses it, which is exactly the
/// property that makes this suite free to run.
/// </summary>
internal static class SessionOpenerSelfTest
{
    public static int Run()
    {
        Console.WriteLine("SessionOpener self-test");

        string root = Path.Combine(Path.GetTempPath(), "sw-opener-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);

        int failures = 0;

        try
        {
            failures += CheckAuthoredPlayerOpensDirectly(root);
            failures += CheckUnauthoredPlayerWaitsThenCompletes(root);
            failures += CheckCompletedPlayerPersists(root);
            failures += CheckResumeNeverAsks(root);
            failures += CheckAbandoningReleasesTheLock(root);
            failures += CheckHeldSaveIsRefused(root);
            failures += CheckForceOverridesAHeldSave(root);
            failures += CheckSaveOriginIsWrittenOnceAndDriftIsReported(root);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // A leaked handle should fail the suite that leaked it, not this cleanup.
            }
        }

        Console.WriteLine(failures == 0
            ? "  all SessionOpener checks passed"
            : $"  {failures} SessionOpener check(s) failed");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// `the-last-lantern` and `marrow` both ship `characters/player.md`, so opening them never
    /// stops to ask — the pack has already said who you are.
    /// </summary>
    private static int CheckAuthoredPlayerOpensDirectly(string root)
    {
        SessionOpening opening = Open(root, "the-last-lantern", "authored");

        if (opening.WasRefused || opening.IsWaitingForPlayer || opening.Session is null)
        {
            Console.WriteLine("  FAIL  a pack that authors its player should open directly.");
            Dispose(opening);
            return 1;
        }

        if (opening.Context!.Resumed)
        {
            Console.WriteLine("  FAIL  a brand new save reported as resumed.");
            Dispose(opening);
            return 1;
        }

        opening.Session.Dispose();
        Console.WriteLine("  ok    a pack that authors the player opens without asking");
        return 0;
    }

    /// <summary>
    /// `ashfall` ships no `player.md`, so opening stops. The point of the two-phase shape is that
    /// everything is already loaded — completing is one write, not a second open.
    ///
    /// This is also the pack that used to hang: character creation looped forever on a closed
    /// stdin while holding the save lock, because there was no way for the prompt to give up.
    /// </summary>
    private static int CheckUnauthoredPlayerWaitsThenCompletes(string root)
    {
        SessionOpening opening = Open(root, "ashfall", "asks");

        if (!opening.IsWaitingForPlayer)
        {
            Console.WriteLine("  FAIL  a pack with no player.md should wait for one.");
            Dispose(opening);
            return 1;
        }

        // The banner is available before the question, which is the reason Context is set on
        // the waiting outcome as well as the opened one.
        if (opening.Context is null)
        {
            Console.WriteLine("  FAIL  a waiting outcome carries no context to render.");
            Dispose(opening);
            return 1;
        }

        using StorySession session = opening.NeedsPlayer!
            .CompleteAsync("Vessa", "A courier with a bad knee.")
            .GetAwaiter().GetResult();

        if (session.World.Player?.Name != "Vessa"
            || session.World.Player.Description != "A courier with a bad knee.")
        {
            Console.WriteLine("  FAIL  the completed player is not in canon.");
            return 1;
        }

        Console.WriteLine("  ok    a pack with no player.md waits, then completes into a session");
        return 0;
    }

    /// <summary>
    /// The completion has to reach disk, or the next open asks again and the answer was
    /// theatre.
    /// </summary>
    private static int CheckCompletedPlayerPersists(string root)
    {
        SessionOpening first = Open(root, "ashfall", "persists");
        first.NeedsPlayer!.CompleteAsync("Ambrose", "Thin, and always cold.")
            .GetAwaiter().GetResult().Dispose();

        SessionOpening second = Open(root, "ashfall", "persists");

        if (second.IsWaitingForPlayer)
        {
            Console.WriteLine("  FAIL  reopening asked for a player again.");
            Dispose(second);
            return 1;
        }

        using StorySession session = second.Session!;

        if (session.World.Player?.Name != "Ambrose")
        {
            Console.WriteLine($"  FAIL  expected Ambrose, got '{session.World.Player?.Name}'.");
            return 1;
        }

        Console.WriteLine("  ok    a completed player persists, and reopening resumes");
        return 0;
    }

    private static int CheckResumeNeverAsks(string root)
    {
        SessionOpening first = Open(root, "ashfall", "resume");
        first.NeedsPlayer!.CompleteAsync("Tarn").GetAwaiter().GetResult().Dispose();

        SessionOpening second = Open(root, "ashfall", "resume");

        if (!second.Context!.Resumed || second.IsWaitingForPlayer)
        {
            Console.WriteLine("  FAIL  reopening an existing save did not report a resume.");
            Dispose(second);
            return 1;
        }

        second.Session!.Dispose();
        Console.WriteLine("  ok    resuming reports a resume and never asks");
        return 0;
    }

    /// <summary>
    /// The question can be abandoned — a closed dialog, a Ctrl-C at the prompt. The save lock is
    /// held while it is on screen, so abandoning has to give it back or the save is unopenable
    /// until somebody deletes a dotfile they do not know about.
    /// </summary>
    private static int CheckAbandoningReleasesTheLock(string root)
    {
        SessionOpening opening = Open(root, "ashfall", "abandoned");
        opening.NeedsPlayer!.Dispose();

        string lockFile = Path.Combine(root, "saves", "abandoned", SaveLock.FileName);

        if (File.Exists(lockFile))
        {
            Console.WriteLine("  FAIL  abandoning the player question left the lock behind.");
            return 1;
        }

        // And the proof that matters: it can be opened again.
        SessionOpening again = Open(root, "ashfall", "abandoned");

        if (again.WasRefused)
        {
            Console.WriteLine($"  FAIL  reopening after abandoning was refused: {again.RefusedBecause}.");
            return 1;
        }

        Dispose(again);
        Console.WriteLine("  ok    abandoning the question hands the save back");
        return 0;
    }

    private static int CheckHeldSaveIsRefused(string root)
    {
        SessionOpening first = Open(root, "the-last-lantern", "contested");
        using StorySession held = first.Session!;

        SessionOpening second = Open(root, "the-last-lantern", "contested");

        if (!second.WasRefused)
        {
            Console.WriteLine("  FAIL  a save already open was not refused.");
            Dispose(second);
            return 1;
        }

        if (string.IsNullOrWhiteSpace(second.HeldBy))
        {
            Console.WriteLine("  FAIL  the refusal did not say who holds the save.");
            return 1;
        }

        Console.WriteLine("  ok    a held save is refused, and the refusal names the holder");
        return 0;
    }

    /// <summary>
    /// `--force` still takes a save that is genuinely held, which is the escape hatch for the
    /// day the detection is wrong. Worth a check of its own now that a same-process holder is
    /// refused: the in-memory set must not become a lock nothing can break.
    /// </summary>
    private static int CheckForceOverridesAHeldSave(string root)
    {
        SessionOpening first = Open(root, "the-last-lantern", "forced");
        using StorySession held = first.Session!;

        SessionOpening forced = SessionOpener.OpenAsync(
            Settings(), "the-last-lantern", "forced", force: true,
            saveRoot: Path.Combine(root, "saves"), packRoot: "worlds").GetAwaiter().GetResult();

        if (forced.WasRefused)
        {
            Console.WriteLine($"  FAIL  --force did not take a held save: {forced.RefusedBecause}.");
            return 1;
        }

        Dispose(forced);
        Console.WriteLine("  ok    --force still takes a save that is held");
        return 0;
    }

    /// <summary>
    /// `save.json` records what a playthrough began against, and is never rewritten — that is
    /// what makes drift detectable at all. Then a pack whose version has moved is reported on
    /// the outcome rather than printed.
    /// </summary>
    private static int CheckSaveOriginIsWrittenOnceAndDriftIsReported(string root)
    {
        SessionOpening first = Open(root, "the-last-lantern", "drift");
        first.Session!.Dispose();

        string originPath = Path.Combine(root, "saves", "drift", "save.json");

        if (!File.Exists(originPath))
        {
            Console.WriteLine("  FAIL  save.json was not written on a fresh open.");
            return 1;
        }

        string written = File.ReadAllText(originPath);

        // Pretend the save began against an older version of the pack.
        File.WriteAllText(originPath, written.Replace("\"packVersion\"", "\"packVersion\"")
            .Replace(VersionInFile(written), "0.1"));

        SessionOpening second = Open(root, "the-last-lantern", "drift");

        if (!second.Context!.PackHasMoved || second.Context.PackVersionAtStart != "0.1")
        {
            Console.WriteLine(
                $"  FAIL  drift not reported; PackVersionAtStart='{second.Context.PackVersionAtStart}'.");
            Dispose(second);
            return 1;
        }

        second.Session!.Dispose();

        // And it was not rewritten to match the pack, or drift would be undetectable next time.
        if (!File.ReadAllText(originPath).Contains("0.1", StringComparison.Ordinal))
        {
            Console.WriteLine("  FAIL  save.json was rewritten on resume.");
            return 1;
        }

        Console.WriteLine("  ok    save.json is written once, and drift is reported not printed");
        return 0;
    }

    private static string VersionInFile(string json)
    {
        int at = json.IndexOf("\"packVersion\"", StringComparison.Ordinal);
        int open = json.IndexOf('"', json.IndexOf(':', at) + 1);
        int close = json.IndexOf('"', open + 1);
        return json[(open + 1)..close];
    }

    private static SessionOpening Open(string root, string packId, string saveId) =>
        SessionOpener.OpenAsync(
            Settings(),
            packId,
            saveId,
            force: false,
            saveRoot: Path.Combine(root, "saves"),
            packRoot: "worlds").GetAwaiter().GetResult();

    private static void Dispose(SessionOpening opening)
    {
        opening.Session?.Dispose();
        opening.NeedsPlayer?.Dispose();
    }

    /// <summary>
    /// Enough settings to construct the provider client, which opening does and never uses. No
    /// key is needed because no call is made — and if that ever stops being true, this suite
    /// starts failing rather than quietly spending money.
    /// </summary>
    private static StoryWeaverSettings Settings() => new()
    {
        Provider = new ProviderSettings { ApiKey = "not-used-offline" },
    };
}
