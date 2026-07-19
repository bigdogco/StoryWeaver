namespace StoryWeaver.Llm.Story;

/// <summary>
/// The JSON schema constraining extraction output to the closed <c>StateDelta</c> set.
///
/// Verified working as a nine-branch <c>anyOf</c> under <c>strict: true</c> — see
/// docs/CHALLENGES.md. Strict mode has two requirements that are easy to get wrong: every
/// object must set <c>additionalProperties: false</c>, and every property must appear in
/// <c>required</c>. Optionality is expressed by allowing null, never by omission.
///
/// Kept as a hand-written string rather than generated from the C# types. Generation would
/// keep the two in sync automatically, but the descriptions here are prompt engineering —
/// they are the only place the model is told what distinguishes a status from a mood, or
/// that establishing a fact is not the same as someone learning it. Those sentences are
/// doing real work and would not survive being derived from type shapes.
///
/// <b>Adding a delta kind means editing this and <c>DeltaApplier</c> together.</b>
/// </summary>
public static class DeltaSchema
{
    public const string Name = "state_deltas";

    public const string Json = """
    {
      "type": "object",
      "properties": {
        "deltas": {
          "type": "array",
          "description": "Every state change the narration supports. Empty if nothing changed.",
          "items": {
            "anyOf": [
              {
                "type": "object",
                "description": "An existing character changed location.",
                "properties": {
                  "kind": { "type": "string", "enum": ["character_moved"] },
                  "characterId": { "type": "string" },
                  "toLocationId": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "characterId", "toLocationId", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "The player changed location.",
                "properties": {
                  "kind": { "type": "string", "enum": ["player_moved"] },
                  "toLocationId": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "toLocationId", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "Physical or situational condition changed: wounded, asleep, imprisoned. Not for emotions.",
                "properties": {
                  "kind": { "type": "string", "enum": ["status_changed"] },
                  "characterId": { "type": "string" },
                  "status": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "characterId", "status", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "Emotional register changed: wary, delighted, furious. Emit this whenever the prose shows a shift in how a character feels, however brief.",
                "properties": {
                  "kind": { "type": "string", "enum": ["mood_changed"] },
                  "characterId": { "type": "string" },
                  "mood": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "characterId", "mood", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "A character's standing toward the player changed. Never valid for the player themselves.",
                "properties": {
                  "kind": { "type": "string", "enum": ["relationship_changed"] },
                  "characterId": { "type": "string" },
                  "standing": { "type": "integer", "description": "-100 (hostile) to 100 (devoted)." },
                  "summary": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "characterId", "standing", "summary", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "A new piece of world truth entered the story, regardless of who knows it. This does NOT make anyone aware of it - emit fact_learned separately for that.",
                "properties": {
                  "kind": { "type": "string", "enum": ["fact_established"] },
                  "factId": { "type": "string", "description": "A new slug id, e.g. 'cellar-poisoning'." },
                  "text": { "type": "string", "description": "The claim as one sentence." },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "factId", "text", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "A character came to know a fact. The fact must already exist in the known ids, or be established by a fact_established earlier in this same list.",
                "properties": {
                  "kind": { "type": "string", "enum": ["fact_learned"] },
                  "characterId": { "type": "string" },
                  "factId": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "characterId", "factId", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "A character who is NOT in the known ids appeared for the first time. Never use this for a character that already exists.",
                "properties": {
                  "kind": { "type": "string", "enum": ["character_introduced"] },
                  "characterId": { "type": "string", "description": "A new slug id, e.g. 'militia-woman'." },
                  "name": { "type": "string" },
                  "description": { "type": "string", "description": "Who they are, not what just happened to them." },
                  "locationId": { "type": ["string", "null"] },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "characterId", "name", "description", "locationId", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "A place that is NOT in the known ids appeared for the first time. Merely mentioning a known place is not introducing it.",
                "properties": {
                  "kind": { "type": "string", "enum": ["location_introduced"] },
                  "locationId": { "type": "string" },
                  "name": { "type": "string" },
                  "description": { "type": "string", "description": "What the place is like, not an event that happened there." },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "locationId", "name", "description", "evidence"],
                "additionalProperties": false
              }
            ]
          }
        }
      },
      "required": ["deltas"],
      "additionalProperties": false
    }
    """;
}
