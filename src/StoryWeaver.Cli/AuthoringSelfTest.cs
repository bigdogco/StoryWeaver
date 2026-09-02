using StoryWeaver.Core;
using StoryWeaver.Storage;

namespace StoryWeaver.Cli;

/// <summary>
/// Offline checks on <see cref="Authoring"/> — the policy the console and, later, an editor
/// window both call.
///
/// It exists because that policy just stopped having a single caller. While the rules lived
/// inside the console prompts, "does the id convention agree with the loader" and "did a
/// failed commit write the file" were answered by reading forty lines in one place. With two
/// callers, both questions become the kind that is answered wrong quietly.
/// </summary>
internal static class AuthoringSelfTest
{
    public static int Run()
    {
        Console.WriteLine("Authoring self-test");

        int failures = 0;

        failures += CheckSlugsAreWellFormed();
        failures += CheckSlugShape();
        failures += CheckIdConflicts();
        failures += CheckFactKnowledgeIsSeparate();
        failures += CheckNothingAcceptedWritesNothing();
        failures += CheckAcceptedCommitPersists();

        Console.WriteLine(failures == 0
            ? "  all authoring checks passed"
            : $"  {failures} authoring check(s) failed");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// The bridge between two projects. <c>Slug</c> is in Core; <c>EntityId.IsWellFormed</c> is
    /// in Storage, and Core cannot reference it because dependencies point inward. They are
    /// nonetheless one convention, and an id that a slug produces but the loader rejects is a
    /// character whose sheet and seed entry never meet.
    /// </summary>
    private static int CheckSlugsAreWellFormed()
    {
        string[] inputs =
        [
            "The Drowned Crow",
            "King's Investigators",
            "Innkeeper Hald",
            "Bill stole the grain from the mill last winter",
            "  Ashfall   Ridge  ",
            "Nessa — the figure in the cistern",
            "St. Aubry's Lantern, No. 7",
            "a shivering figure in rags",
        ];

        foreach (string input in inputs)
        {
            string slug = Authoring.Slug(input);

            if (!EntityId.IsWellFormed(slug))
            {
                Console.WriteLine($"  FAIL  slug '{slug}' from \"{input}\" is not a well-formed id.");
                return 1;
            }
        }

        Console.WriteLine($"  ok    {inputs.Length} slugs all satisfy EntityId.IsWellFormed");
        return 0;
    }

    /// <summary>
    /// The two rules that are taste rather than validity, and would otherwise be free to drift:
    /// an apostrophe is dropped rather than separated, and a fact slug stops at four words.
    /// </summary>
    private static int CheckSlugShape()
    {
        int failures = 0;

        failures += ExpectSlug("King's Investigators", "kings-investigators");
        failures += ExpectSlug("Bill stole the grain from the mill", "bill-stole-the-grain");
        failures += ExpectSlug("The Drowned Crow", "the-drowned-crow");

        return failures;
    }

    private static int ExpectSlug(string input, string expected)
    {
        string actual = Authoring.Slug(input);

        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            Console.WriteLine($"  FAIL  slug \"{input}\": expected '{expected}', got '{actual}'.");
            return 1;
        }

        Console.WriteLine($"  ok    \"{input}\" → {expected}");
        return 0;
    }

    /// <summary>
    /// All three namespaces, because facts and lore share an id space with entities — the
    /// property that lets a lore entry be learned without a delta kind of its own, and the
    /// reason a check against characters alone would be a hole rather than an oversight.
    /// </summary>
    private static int CheckIdConflicts()
    {
        WorldState world = new();
        world.Characters["innkeeper-hald"] = new Character { Id = "innkeeper-hald", Name = "Hald" };
        world.Locations["marrow-tavern"] = new Location { Id = "marrow-tavern", Name = "The Drowned Crow" };
        world.Facts["bill-stole-the-grain"] = new Fact { Id = "bill-stole-the-grain", Text = "Bill stole it." };

        int failures = 0;

        failures += ExpectConflict(world, "innkeeper-hald", conflicts: true, "a character id");
        failures += ExpectConflict(world, "marrow-tavern", conflicts: true, "a location id");
        failures += ExpectConflict(world, "bill-stole-the-grain", conflicts: true, "a fact id");
        failures += ExpectConflict(world, "warrior-mike", conflicts: false, "an unused id");
        failures += ExpectConflict(world, "", conflicts: true, "an empty id");
        failures += ExpectConflict(world, null, conflicts: true, "a null id");

        return failures;
    }

    private static int ExpectConflict(WorldState world, string? id, bool conflicts, string what)
    {
        bool actual = Authoring.IdConflict(world, id) is not null;

        if (actual != conflicts)
        {
            Console.WriteLine(
                $"  FAIL  {what} '{id}': expected conflict={conflicts}, got {actual}.");
            return 1;
        }

        Console.WriteLine($"  ok    {what} reported correctly");
        return 0;
    }

    /// <summary>
    /// Establishing a fact says nothing about who knows it. If these ever collapse into one
    /// delta, an author can no longer write down a truth their own character has not yet
    /// discovered — which is most of what authoring a mystery consists of.
    /// </summary>
    private static int CheckFactKnowledgeIsSeparate()
    {
        IReadOnlyList<StateDelta> unknown = Authoring.Fact("f", "Bill stole the grain.", playerKnows: false);
        IReadOnlyList<StateDelta> known = Authoring.Fact("f", "Bill stole the grain.", playerKnows: true);

        if (unknown.Count != 1 || unknown[0] is not FactEstablished)
        {
            Console.WriteLine($"  FAIL  fact without knowledge: expected 1 FactEstablished, got {unknown.Count}.");
            return 1;
        }

        if (known.Count != 2 || known[1] is not FactLearned { CharacterId: Character.PlayerId })
        {
            Console.WriteLine($"  FAIL  fact with knowledge: expected FactEstablished + player FactLearned, got {known.Count} deltas.");
            return 1;
        }

        Console.WriteLine("  ok    a fact and knowing it are separate deltas");
        return 0;
    }

    /// <summary>
    /// A commit that accepts nothing must not touch the file. Harmless today; wrong anyway,
    /// because the author may have the save open in another window, and rewriting it for a
    /// change that did not happen is how an editor eats an edit.
    /// </summary>
    private static int CheckNothingAcceptedWritesNothing()
    {
        WorldState world = new();
        world.Characters["innkeeper-hald"] = new Character { Id = "innkeeper-hald", Name = "Hald" };

        CountingRepository repository = new();

        // Reusing a character id as a location — rejected by the validator, as the
        // cross-namespace check in JsonSelfTest establishes.
        ValidationOutcome outcome = Authoring.CommitAsync(
            Authoring.Place("innkeeper-hald", "The Drowned Crow", "A taproom."),
            "test", world, repository).GetAwaiter().GetResult();

        if (outcome.Accepted.Count != 0)
        {
            Console.WriteLine($"  FAIL  expected nothing accepted, got {outcome.Accepted.Count}.");
            return 1;
        }

        if (repository.Saves != 0)
        {
            Console.WriteLine($"  FAIL  a rejected commit saved {repository.Saves} time(s).");
            return 1;
        }

        if (world.Locations.Count != 0)
        {
            Console.WriteLine("  FAIL  a rejected commit changed canon in memory.");
            return 1;
        }

        Console.WriteLine("  ok    a commit that accepts nothing writes nothing");
        return 0;
    }

    private static int CheckAcceptedCommitPersists()
    {
        WorldState world = new();
        CountingRepository repository = new();

        ValidationOutcome outcome = Authoring.CommitAsync(
            Authoring.Place("marrow-tavern", "The Drowned Crow", "A taproom over black water."),
            "test", world, repository).GetAwaiter().GetResult();

        if (outcome.Accepted.Count != 1 || repository.Saves != 1 || !world.Locations.ContainsKey("marrow-tavern"))
        {
            Console.WriteLine(
                $"  FAIL  accepted commit: {outcome.Accepted.Count} accepted, {repository.Saves} save(s), " +
                $"present={world.Locations.ContainsKey("marrow-tavern")}; expected 1, 1, true.");
            return 1;
        }

        Console.WriteLine("  ok    an accepted commit applies and saves exactly once");
        return 0;
    }

    /// <summary>Counts saves. The in-memory repository cannot say whether one happened.</summary>
    private sealed class CountingRepository : IWorldRepository
    {
        private readonly InMemoryWorldRepository _inner = new();

        public int Saves { get; private set; }

        public Task<WorldState?> LoadAsync(string worldId, CancellationToken cancellationToken = default) =>
            _inner.LoadAsync(worldId, cancellationToken);

        public Task SaveAsync(string worldId, WorldState state, CancellationToken cancellationToken = default)
        {
            Saves++;
            return _inner.SaveAsync(worldId, state, cancellationToken);
        }

        public Task AppendTurnAsync(string worldId, TurnRecord turn, CancellationToken cancellationToken = default) =>
            _inner.AppendTurnAsync(worldId, turn, cancellationToken);

        public Task ReplaceLastTurnAsync(string worldId, TurnRecord turn, CancellationToken cancellationToken = default) =>
            _inner.ReplaceLastTurnAsync(worldId, turn, cancellationToken);

        public Task<IReadOnlyList<TurnRecord>> LoadHistoryAsync(string worldId, CancellationToken cancellationToken = default) =>
            _inner.LoadHistoryAsync(worldId, cancellationToken);

        public Task<IReadOnlyList<string>> ListWorldsAsync(CancellationToken cancellationToken = default) =>
            _inner.ListWorldsAsync(cancellationToken);
    }
}
