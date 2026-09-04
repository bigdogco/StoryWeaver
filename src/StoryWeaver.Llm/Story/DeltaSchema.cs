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
                  "text": { "type": "string", "description": "The claim as one sentence. State it plainly - do not write 'X claims that...', that is what sourceId is for." },
                  "sourceId": { "type": ["string", "null"], "description": "The id of the character who asserted this, if a character did. Null when the narration states it as plain truth rather than somebody saying it. Two characters can contradict each other and both claims are recorded - the source is what keeps them apart." },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "factId", "text", "sourceId", "evidence"],
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
                "description": "A character who IS in the known ids is now called something else — usually because the story revealed the name of someone introduced anonymously. Use their existing id; the id never changes.",
                "properties": {
                  "kind": { "type": "string", "enum": ["character_renamed"] },
                  "characterId": { "type": "string", "description": "The character's EXISTING id from the known ids. Do not invent a new one." },
                  "name": { "type": "string", "description": "The name they are known by now, e.g. 'Nessa'." },
                  "description": { "type": ["string", "null"], "description": "A revised description, or null to keep the current one. Who they are, not what just happened." },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "characterId", "name", "description", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "A physical object entered the story in a way that matters: handled, given, taken, produced from a pocket, put on a table. NOT for scenery. A room's furniture, fittings and background objects are description only and must never become items.",
                "properties": {
                  "kind": { "type": "string", "enum": ["item_introduced"] },
                  "itemId": { "type": "string", "description": "A new slug id, e.g. 'silver-pendant'." },
                  "name": { "type": "string" },
                  "description": { "type": "string", "description": "What the thing IS, not what just happened to it." },
                  "locationId": { "type": ["string", "null"], "description": "Where it is, if nobody is holding it. Set this OR holderId, never both, never neither." },
                  "holderId": { "type": ["string", "null"], "description": "Who has it, if somebody does. Set this OR locationId, never both, never neither." },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "itemId", "name", "description", "locationId", "holderId", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "An item changed hands, was picked up, or was put down. Set toLocationId OR toHolderId, never both, never neither.",
                "properties": {
                  "kind": { "type": "string", "enum": ["item_moved"] },
                  "itemId": { "type": "string" },
                  "toLocationId": { "type": ["string", "null"] },
                  "toHolderId": { "type": ["string", "null"] },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "itemId", "toLocationId", "toHolderId", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "What an item IS was revised - either it turned out to be something else ('old foundation blocks' revealed as a carved capstone), or a closer look revealed something permanent about it that was always true. Use this whenever examining an object reveals a lasting property: a maker's mark, a carving, an inscription, what it is made of. The name may stay exactly the same and only the description change - that is a normal and expected use. Use its EXISTING id; the id never changes.",
                "properties": {
                  "kind": { "type": "string", "enum": ["item_renamed"] },
                  "itemId": { "type": "string", "description": "The item's EXISTING id. Do not invent a new one." },
                  "name": { "type": "string" },
                  "description": { "type": ["string", "null"], "description": "A revised description, or null to keep the current one." },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "itemId", "name", "description", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "An item's physical condition changed: ground to powder, burned, broken, soaked. Not for where it is or who has it.",
                "properties": {
                  "kind": { "type": "string", "enum": ["item_status_changed"] },
                  "itemId": { "type": "string" },
                  "status": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "itemId", "status", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "An object turned out to be a person. Use this the moment a thing you recorded as an item proves to be alive: it breathes, moves on its own, or speaks. Keeps the same id, so a fact in this same batch may name it as a source.",
                "properties": {
                  "kind": { "type": "string", "enum": ["item_revealed_as_character"] },
                  "itemId": { "type": "string", "description": "The item's EXISTING id. Do not invent a new one and do not also introduce a character." },
                  "name": { "type": "string" },
                  "description": { "type": "string", "description": "Who they are, now that you can see." },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "itemId", "name", "description", "evidence"],
                "additionalProperties": false
              },
              {
                "type": "object",
                "description": "A place's condition changed: flooding, burning, filling with smoke, fallen silent. What the place is DOING now, not what it permanently is. Use this for a place changing rather than writing it down as a fact.",
                "properties": {
                  "kind": { "type": "string", "enum": ["location_status_changed"] },
                  "locationId": { "type": "string" },
                  "status": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["kind", "locationId", "status", "evidence"],
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
