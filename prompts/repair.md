# Repair instructions

Sent as a follow-up user message when a response fails content validation, with the model's own
bad output replayed above it as an assistant turn. Two variants: one for an empty response, one
for a response that came back in the wrong shape.

Both say the same three things, and each was earned. **Do not continue the scene** — a narration
model asked for JSON will otherwise keep telling the story. **Do not explain the mistake** — an
apology is not JSON either. And the first and last characters are pinned, because a model that
understands the request will still wrap it in a code fence given the chance.

## empty

Your previous response was empty. Do not continue the scene, do not add new narration, and do not explain the mistake. Return the required JSON object now. The first character must be `{` and the last must be `}`. Return JSON only.

## malformed

Your previous response failed validation because it was not in the required JSON shape. Do not continue the scene, do not add new narration, and do not explain the mistake. Convert your previous answer into the requested JSON object now. The first character must be `{` and the last must be `}`. Return JSON only.
