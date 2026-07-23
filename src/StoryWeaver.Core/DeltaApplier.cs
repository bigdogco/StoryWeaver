namespace StoryWeaver.Core;

/// <summary>
/// Applies validated deltas to canon.
///
/// Separate from <see cref="DeltaValidator"/> on purpose. Validation decides what is
/// legitimate and applying performs it; folding them together would mean the rules were
/// expressed halfway through mutating the world, where a rejection has already left
/// something changed.
///
/// This assumes it is handed <see cref="ValidationOutcome.Accepted"/> and does not re-check.
/// Applying an unvalidated batch will produce a corrupt world — that is why the only path
/// to here in normal use runs through <see cref="TurnEngine"/>.
/// </summary>
public static class DeltaApplier
{
    public static void Apply(WorldState world, IReadOnlyList<StateDelta> deltas)
    {
        foreach (StateDelta delta in deltas)
        {
            Apply(world, delta);
        }
    }

    private static void Apply(WorldState world, StateDelta delta)
    {
        switch (delta)
        {
            case CharacterIntroduced d:
                world.Characters[d.CharacterId] = new Character
                {
                    Id = d.CharacterId,
                    Name = d.Name,
                    Description = d.Description,
                    LocationId = d.LocationId,
                    LastSeenTurn = world.TurnNumber,
                };
                break;

            case LocationIntroduced d:
                world.Locations[d.LocationId] = new Location
                {
                    Id = d.LocationId,
                    Name = d.Name,
                    Description = d.Description,
                };
                break;

            // Name and description only. The id is deliberately untouched — see
            // CharacterRenamed — so every reference to this character survives the reveal.
            case CharacterRenamed d:
                if (world.FindCharacter(d.CharacterId) is { } renamed)
                {
                    renamed.Name = d.Name;

                    if (!string.IsNullOrWhiteSpace(d.Description))
                    {
                        renamed.Description = d.Description;
                    }
                }

                break;

            case CharacterMoved d:
                if (world.FindCharacter(d.CharacterId) is { } moved)
                {
                    moved.LocationId = d.ToLocationId;
                }

                break;

            // The player is an ordinary character, so this is the same operation as
            // CharacterMoved. It stays a distinct delta kind because the extractor benefits
            // from being able to say "the player moved" without knowing the reserved id.
            case PlayerMoved d:
                if (world.Player is { } player)
                {
                    player.LocationId = d.ToLocationId;
                }

                break;

            case StatusChanged d:
                if (world.FindCharacter(d.CharacterId) is { } statusTarget)
                {
                    statusTarget.Status = d.Status;
                }

                break;

            case MoodChanged d:
                if (world.FindCharacter(d.CharacterId) is { } moodTarget)
                {
                    moodTarget.Mood = d.Mood;
                }

                break;

            case RelationshipChanged d:
                if (world.FindCharacter(d.CharacterId) is { } related)
                {
                    related.RelationshipToPlayer = new Relationship(d.Standing, d.Summary);
                }

                break;

            case FactEstablished d:
                world.Facts[d.FactId] = new Fact
                {
                    Id = d.FactId,
                    Text = d.Text,
                    EstablishedTurn = world.TurnNumber,
                };
                break;

            case FactLearned d:
                world.FindCharacter(d.CharacterId)?.Knows.Add(d.FactId);
                break;

            default:
                throw new InvalidOperationException(
                    $"No apply rule for delta type '{delta.GetType().Name}'. A delta kind was " +
                    "added without extending DeltaApplier.");
        }
    }
}
