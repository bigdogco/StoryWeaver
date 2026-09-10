using System.Text.Json.Nodes;

namespace StoryWeaver.Llm.Story;

internal static class DiscoverySchema
{
    private static JsonObject String() => new() { ["type"] = "string" };
    private static JsonObject Nullable(JsonNode value) => new() { ["anyOf"] = new JsonArray(value, new JsonObject { ["type"] = "null" }) };
    private static JsonObject Object(params (string Key, JsonNode Value)[] fields)
    {
        var properties = new JsonObject(); var required = new JsonArray();
        foreach (var (key, value) in fields) { properties[key] = value; required.Add(key); }
        return new() { ["type"] = "object", ["properties"] = properties, ["required"] = required, ["additionalProperties"] = false };
    }
    private static JsonObject Observation(JsonNode value) => Object(("value", value),
        ("provenance", new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Observed", "Reported") }),
        ("factId", Nullable(String())));

    public static string Extend(string original)
    {
        var root = JsonNode.Parse(original)!;
        var branches = root["properties"]!["deltas"]!["items"]!["anyOf"]!.AsArray();
        branches[0]!["properties"]!["toLocationId"] = Nullable(String());
        branches[0]!["description"] = "An existing NPC left their location. Null destination ONLY for an explicit departure with no established destination; never for a failed search, rumour or losing sight within the same room. The player always requires a real destination.";
        foreach (string kind in new[] { "character", "location", "item" })
        {
            var fields = new List<(string, JsonNode)>
            {
                ("kind", new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(kind + "_observed") }),
                ("targetId", String()), ("identity", Nullable(Observation(String()))),
                ("descriptionObservation", Nullable(Observation(String()))), ("condition", Nullable(Observation(String()))),
                ("whereabouts", Nullable(Observation(Object(
                    ("kind", new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Unknown", "Location", "Holder") }),
                    ("targetId", Nullable(String())), ("disclosedLabel", Nullable(String())))))),
                ("evidence", new JsonObject { ["type"] = "string", ["description"] = "A short contiguous verbatim narration excerpt. Prefer a few words without surrounding quotation marks; never add punctuation or close a quote that does not close there in the narration." })
            };
            if (kind == "location") fields.Add(("connections", Nullable(new JsonObject { ["type"] = "array",
                ["items"] = Object(("destinationId", String()), ("label", Observation(String()))) })));
            var branch = Object(fields.ToArray());
            branch["properties"]!["descriptionObservation"]!["description"] = "Disclosed appearance, background or purpose, including a reported purpose. Never copy private description. A reported purpose is retained here alongside its attributed learned fact.";
            branch["description"] = "Record ONLY the supported entity aspects actually disclosed to the protagonist in the current narration. Quote exact narration evidence. Null aspects leave previous memories unchanged. Do not copy private fields. Name can be a disclosed alias. Reported aspects require an attributed fact learned by player in this or a prior turn. Location whereabouts must be null; connections are individual disclosed directed routes. Unknown whereabouts is an explicit loss-of-contact observation, not actual movement.";
            branches.Add(branch);
        }
        return root.ToJsonString();
    }
}
