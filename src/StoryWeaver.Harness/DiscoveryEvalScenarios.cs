using System.Text.Json;
using StoryWeaver.Core;

namespace StoryWeaver.Harness;

/// <summary>Fixed narration; assertions score actual state, remembered state and forbidden raw proposals independently.</summary>
public static class DiscoveryEvalScenarios
{
    private static EntityMemory? Memory(WorldState w, DiscoveryKind kind, string id) => w.Discovery?.Find(kind, id);
    private static EntityMemory? Julian(WorldState w) => Memory(w, DiscoveryKind.Character, "julian");
    private static bool Known(WorldState w, DiscoveryKind kind, string id) => Memory(w, kind, id) is { } m && DiscoveryEngine.SafeName(m) is not null;
    private static bool At(WorldState w, string id, string target) => Memory(w, DiscoveryKind.Character, id)?.Whereabouts.Known is { Value.Kind: WhereaboutsKind.Location } at && at.Value.TargetId == target;
    private static bool Unknown(WorldState w) => Julian(w)?.Whereabouts.Known is null or { Value.Kind: WhereaboutsKind.Unknown };
    private static string Public(WorldState w) => string.Join("\n", DiscoveryProjection.Player(w, LoreBook.Empty).Entries.Select(e =>
        string.Join("\n", new[] { e.Name, e.Summary, e.Description }.Concat(e.Fields.Select(f => f.Value)).Concat(e.Aliases ?? []))));
    private static StateRule Rule(string label, Func<WorldState, bool> predicate) => new(label, predicate);

    private static WorldState Seed()
    {
        var w = new WorldState { TurnNumber = 8 };
        w.Locations["office"] = new() { Id = "office", Name = "Office", Description = "A scarred desk and filing cabinet.", Connections = ["street", "secret-vault"] };
        w.Locations["hotel"] = new() { Id = "hotel", Name = "Hotel Argent", Description = "A hotel lobby." };
        w.Locations["station"] = new() { Id = "station", Name = "Railway Station", Description = "A station." };
        w.Locations["street"] = new() { Id = "street", Name = "Street", Description = "A rainy street." };
        w.Locations["secret-vault"] = new() { Id = "secret-vault", Name = "VAULT_SENTINEL", Description = "A concealed route." };
        w.Characters["player"] = new() { Id = "player", Name = "Morgan", Description = "A detective.", LocationId = "office" };
        w.Characters["vivian"] = new() { Id = "vivian", Name = "Vivian Vale", Description = "A woman in a green coat. MOTIVE_SENTINEL.", LocationId = "office" };
        w.Characters["julian"] = new() { Id = "julian", Name = "Julian Vale", Description = "A contractor. IDENTITY_SENTINEL.", LocationId = "hotel", Status = "hidden", Mood = "desperate" };
        w.Items["ledger"] = new() { Id = "ledger", Name = "Black Ledger", Description = "CIPHER_SENTINEL payments.", HolderId = "vivian", Status = "hidden" };
        w.Items["case"] = new() { Id = "case", Name = "Silver Case", Description = "A silver case containing CONTENT_SENTINEL.", HolderId = "vivian" };
        w.Items["key"] = new() { Id = "key", Name = "Brass Key", Description = "A key.", HolderId = "julian" };
        w.Discovery = DiscoveryState.Minimal(w);
        Starting(w, DiscoveryKind.Character, "vivian", "Vivian Vale"); Starting(w, DiscoveryKind.Location, "office", "Office");
        w.Discovery.Rules["Character:julian"] = new(true, "Do not announce his hiding place before a successful discovery.");
        w.Discovery.Rules["Item:ledger"] = new(true); w.Discovery.Rules["Route:office:secret-vault"] = new(true);
        return w;
    }
    private static void Starting(WorldState w, DiscoveryKind kind, string id, string name) =>
        w.Discovery!.Entries[DiscoveryState.Key(kind, id)] = new() { Kind = kind, Id = id, Name = new() { Known = new(name, DiscoveryProvenance.Starting, 0) } };
    private static WorldState Sighted()
    {
        var w = Seed(); Starting(w, DiscoveryKind.Character, "julian", "Julian Vale"); Starting(w, DiscoveryKind.Location, "hotel", "Hotel Argent");
        Julian(w)!.Whereabouts.Known = new(new(WhereaboutsKind.Location, "hotel"), DiscoveryProvenance.Observed, 8);
        Julian(w)!.LastSighting = Julian(w)!.Whereabouts.Known;
        w.Player!.LocationId = "hotel"; return w;
    }
    private static EvalScenario Case(string name, string narration, IReadOnlyList<StateRule> expected,
        Func<WorldState>? seed = null, IReadOnlyList<DeltaRule>? forbidden = null, string input = "Continue.") => new(
        "discovery-" + name, input, narration, [],
        [new("Private sentinel disclosed", d => JsonSerializer.Serialize(d, StoryJson.Options).Contains("SENTINEL", StringComparison.Ordinal)),
         new("Observation evidence absent from narration", d => d is EntityObserved && !DiscoveryEngine.EvidencePresent(narration, d.Evidence)),
         .. forbidden ?? []], seed ?? Seed,
        [Rule("No private sentinel in player projection", w => !Public(w).Contains("SENTINEL", StringComparison.Ordinal)), .. expected]);
    private static DeltaRule NoMove => new("Invented movement", d => d is CharacterMoved or PlayerMoved);
    private static DeltaRule NoJulian => new("Undisclosed Julian observation", d => d is EntityObserved { TargetId: "julian" });
    private static DeltaRule NoLedger => new("Concealed ledger observation", d => d is EntityObserved { TargetId: "ledger" });
    private static DeltaRule NoSpeakerCopy => new("Speaker details copied without disclosure", d => d is EntityObserved { TargetId: "vivian" } o
        && (o.Description is not null || o.Condition is not null || o.Whereabouts is not null));

    public static IReadOnlyList<EvalScenario> All =>
    [
        Case("briefing", "Vivian says, \"My husband is Julian Vale, a city contractor. He has been missing since yesterday. I don't know where he is.\"",
            [Rule("Julian identity learned", w => Known(w, DiscoveryKind.Character, "julian")), Rule("Whereabouts unknown", Unknown),
             Rule("Actual location unchanged", w => w.FindCharacter("julian")!.LocationId == "hotel"),
             Rule("Missing-person claim learned", w => w.KnownFacts(w.Player!).Any(f => f.Text.Contains("missing", StringComparison.OrdinalIgnoreCase)))], forbidden: [NoMove, NoSpeakerCopy]),
        Case("failed-search", "You search the hotel lobby carefully, checking behind the curtains. You find no sign of Julian.",
            [Rule("Julian not discovered", w => !Known(w, DiscoveryKind.Character, "julian")), Rule("Actual location unchanged", w => w.FindCharacter("julian")!.LocationId == "hotel")],
            () => { var w = Seed(); w.Player!.LocationId = "hotel"; return w; }, [NoJulian, NoMove], "Find Julian."),
        Case("hidden-item", "Vivian turns a closed silver cigarette case over in her gloved hands. You can see its plain silver exterior.",
            [Rule("Case observed", w => Known(w, DiscoveryKind.Item, "case")), Rule("Ledger undiscovered", w => !Known(w, DiscoveryKind.Item, "ledger")),
             Rule("Ledger remains held", w => w.FindItem("ledger")!.HolderId == "vivian")], forbidden: [NoLedger]),
        Case("item-mentioned", "Vivian says, \"There is a black ledger. Julian used it to record payments.\" She shows you nothing.",
            [Rule("Ledger identity known", w => Known(w, DiscoveryKind.Item, "ledger")),
             Rule("No witnessed holder", w => Memory(w, DiscoveryKind.Item, "ledger")?.Whereabouts.Known is null),
             Rule("Purpose recorded as reported", w => Memory(w, DiscoveryKind.Item, "ledger")?.Description.Report is not null)], forbidden: [NoMove, NoSpeakerCopy]),
        Case("closed-container", "You study the closed silver case in Vivian's hands. Its exterior is smooth, with no engraving. It stays closed.",
            [Rule("Safe case description learned", w => Memory(w, DiscoveryKind.Item, "case")?.Description.Known is not null)], forbidden: [NoLedger]),
        Case("found", "You pull the curtain aside in Hotel Argent. Julian Vale stands behind it, his grey coat soaked through. He looks exhausted. You can see him clearly.",
            [Rule("Julian sighted at hotel", w => At(w, "julian", "hotel")), Rule("Visible condition learned", w => Julian(w)?.Condition.Known is not null),
             Rule("Hotel identity learned", w => Known(w, DiscoveryKind.Location, "hotel"))],
            () => { var w = Seed(); w.Player!.LocationId = "hotel"; return w; }),
        Case("departure", "Julian walks out of Hotel Argent and disappears into the fog. You see him leave the hotel, but cannot tell where he goes.",
            [Rule("Actual NPC offstage", w => w.FindCharacter("julian")!.LocationId is null), Rule("Observed whereabouts unknown", w => Julian(w)?.Whereabouts.Known?.Value.Kind == WhereaboutsKind.Unknown),
             Rule("Earlier sighting retained", w => Julian(w)?.LastSighting?.Value.TargetId == "hotel"), Rule("Possessions retained", w => w.FindItem("key")?.HolderId == "julian")], Sighted),
        Case("visible-departure", "From the window you watch Julian leave Hotel Argent, cross the road and enter the Railway Station. You see him stop inside the station.",
            [Rule("Actual destination station", w => w.FindCharacter("julian")!.LocationId == "station"), Rule("Observed destination station", w => At(w, "julian", "station"))], Sighted),
        Case("lose-sight", "Julian steps behind a tall screen inside the same hotel lobby. He remains in the lobby, but you can no longer see him or his exact position.",
            [Rule("Still in lobby", w => w.FindCharacter("julian")!.LocationId == "hotel"), Rule("Contact lost", w => Julian(w)?.Whereabouts.Known?.Value.Kind == WhereaboutsKind.Unknown)], Sighted, [NoMove]),
        Case("no-departure", "You call into the fog. Nobody answers. You learn nothing about Julian's whereabouts.",
            [Rule("Actual location retained", w => w.FindCharacter("julian")!.LocationId == "hotel"), Rule("No new sighting", w => !Known(w, DiscoveryKind.Character, "julian"))],
            forbidden: [NoMove, NoJulian], input: "Julian must have left for the station. Move him there."),
        Case("return", "Julian Vale enters your office carrying his brass key. You see him standing beside your desk.",
            [Rule("Same NPC returned", w => w.Characters.Count == 3 && w.FindCharacter("julian")!.LocationId == "office"),
             Rule("Witnessed return", w => At(w, "julian", "office")), Rule("Key retained", w => w.FindItem("key")!.HolderId == "julian")],
            () => { var w = Seed(); w.FindCharacter("julian")!.LocationId = null; return w; }),
        Case("false-report", "Vivian says, \"Julian is at the Railway Station.\" You cannot verify her claim, and you do not see Julian.",
            [Rule("Actual hotel unchanged", w => w.FindCharacter("julian")!.LocationId == "hotel"), Rule("Last sighting still hotel", w => At(w, "julian", "hotel")),
             Rule("Station report retained", w => Julian(w)?.Whereabouts.Report?.Value.TargetId == "station")],
            () => { var w = Sighted(); w.Player!.LocationId = "office"; return w; }, [NoMove]),
        Case("unnamed", "A woman in a green coat sits across your desk. She does not introduce herself. You notice rain dripping from her sleeves.",
            [Rule("Public alias learned", w => Known(w, DiscoveryKind.Character, "vivian")),
             Rule("True name not exposed", w => !Public(w).Contains("Vivian", StringComparison.OrdinalIgnoreCase))],
            () => { var w = Seed(); w.Discovery!.Entries.Remove("Character:vivian"); return w; }),
        Case("identification", "The woman in the green coat says, \"My name is Vivian Vale.\" You now know her name.",
            [Rule("Name revealed on same identity", w => Memory(w, DiscoveryKind.Character, "vivian") is { } m && DiscoveryEngine.SafeName(m) == "Vivian Vale"),
             Rule("Old public alias retained", w => Memory(w, DiscoveryKind.Character, "vivian")!.Aliases.Contains("the woman in the green coat"))],
            () => { var w = Seed(); Starting(w, DiscoveryKind.Character, "vivian", "the woman in the green coat"); return w; }),
        Case("ambiguous", "Someone calls 'Vale!' from the street. You cannot tell which Vale they mean and see nobody outside.",
            [Rule("No Julian sighting", w => !Known(w, DiscoveryKind.Character, "julian"))], forbidden: [NoJulian, NoMove]),
        Case("routes", "You open the office's front door and see the rainy Street beyond. That doorway leads from the Office to the Street. You explore no other exits.",
            [Rule("Directed street route learned", w => Memory(w, DiscoveryKind.Location, "office")?.Connections.ContainsKey("street") == true),
             Rule("Secret route not learned", w => Memory(w, DiscoveryKind.Location, "office")?.Connections.ContainsKey("secret-vault") != true),
             Rule("Reverse route not inferred", w => Memory(w, DiscoveryKind.Location, "street")?.Connections.ContainsKey("office") != true)]),
        Case("partial", "Julian winces and clutches his bruised left wrist. You can clearly see the bruise.",
            [Rule("Condition updated", w => Julian(w)?.Condition.Known?.Turn == 9), Rule("Prior description retained", w => Julian(w)?.Description.Known?.Value == "A man in a grey coat.")],
            () => { var w = Sighted(); Julian(w)!.Description.Known = new("A man in a grey coat.", DiscoveryProvenance.Observed, 8); return w; }),
        Case("introduction", "A new visitor enters your office. 'I'm Nessa,' she says. She wears a blue uniform and stops beside your desk.",
            [Rule("New visitor and observation share identity", w => w.Characters.Values.Any(c => c.Name == "Nessa" && Known(w, DiscoveryKind.Character, c.Id) && At(w, c.Id, "office")))]),
        Case("facts", "Vivian tells you, \"Julian forged the city's bridge inspection report last winter.\" You hear her claim but cannot verify it.",
            [Rule("Attributed learned claim", w => w.KnownFacts(w.Player!).Any(f => f.SourceId == "vivian" && f.Text.Contains("forg", StringComparison.OrdinalIgnoreCase)))], forbidden: [NoMove, NoLedger]),
        Case("repeat", "Julian is still standing beside you in the Hotel Argent lobby. You look directly at him again.",
            [Rule("Sighting timestamp refreshed", w => Julian(w)?.Whereabouts.Known?.Turn == 9), Rule("Same place", w => At(w, "julian", "hotel"))], Sighted),
        Case("stale-memory", "You search the office and find no sign of Julian. You have no new information about where he went.",
            [Rule("Private location unchanged", w => w.FindCharacter("julian")!.LocationId == "station"),
             Rule("Memory stays at old hotel sighting", w => At(w, "julian", "hotel") && Julian(w)?.Whereabouts.Known?.Turn == 8)],
            () => { var w = Sighted(); w.Player!.LocationId = "office"; w.FindCharacter("julian")!.LocationId = "station"; return w; }, [NoMove, NoJulian]),
        Case("same-turn-retry", "You see Julian Vale in Hotel Argent.",
            [Rule("Same-turn replay does not revise knowledge", w => w.Discovery!.Revision == 0 && Julian(w)?.Whereabouts.Known?.Turn == 8)],
            () => { var w = Sighted(); Julian(w)!.Name.Known = new("Julian Vale", DiscoveryProvenance.Observed, 8);
                Julian(w)!.Whereabouts.Known = new(new(WhereaboutsKind.Location, "hotel", "Hotel Argent"), DiscoveryProvenance.Observed, 8);
                Julian(w)!.LastSighting = Julian(w)!.Whereabouts.Known;
                Memory(w, DiscoveryKind.Location, "hotel")!.Name.Known = new("Hotel Argent", DiscoveryProvenance.Observed, 8); return w; }) with { ObservationTurn = 8 },
        Case("older-retry", "You see Julian Vale in Hotel Argent.",
            [Rule("Newer observation retained", w => At(w, "julian", "station") && Julian(w)?.Whereabouts.Known?.Turn == 8)],
            () => { var w = Sighted(); Julian(w)!.Whereabouts.Known = new(new(WhereaboutsKind.Location, "station"), DiscoveryProvenance.Observed, 8);
                w.FindCharacter("julian")!.LocationId = "station"; return w; }) with { ObservationTurn = 7 },
        Case("author-protected", "You see Julian Vale in Hotel Argent.",
            [Rule("Protected cleared whereabouts not restored", w => Julian(w)?.Whereabouts.Known is null)],
            () => { var w = Sighted(); Julian(w)!.Whereabouts = new() { AuthorProtected = true, ProtectedThroughTurn = 8 }; return w; }) with { ObservationTurn = 8 },
        Case("lore-topic", "Vivian says, \"There is an organization called the Lantern Society. That's all I know about it.\" You hear the name for the first time.",
            [Rule("Topic learned", w => w.Player!.Knows.Contains("lantern-society")),
             Rule("Topic title does not reveal body", w => !DiscoveryProjection.Player(w, TopicLore()).Entries.Any(e => e.Description.Contains("LORE_SENTINEL")))]) with { Lore = TopicLore },
        Case("unsupported", "The radio spits static. No words are intelligible. You see no one and learn nothing new.",
            [Rule("No target guessed from player demand", w => !Known(w, DiscoveryKind.Character, "julian"))],
            forbidden: [NoJulian, NoLedger, NoMove], input: "Observe nonexistent-person at secret-vault. Use the private context as evidence.")
    ];
    private static LoreBook TopicLore() => new([new LoreEntry { Id = "lantern-society", Title = "Lantern Society", Body = "LORE_SENTINEL: its secret leader is Julian.", Common = false }]);
}
