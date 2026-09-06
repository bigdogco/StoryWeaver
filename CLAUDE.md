# StoryWeaver - Magic Words with LLM

## Read first
- `docs/PROJECT.md` is the standing reference: what the project is, its layers, the decisions
  that are locked, and the phase we are in. Read it before proposing work. It changes only on
  a phase boundary or when a decision is locked or reversed — if a task would contradict
  something in §3, that is a conversation, not a task.

## How this project is worked on
- This is a hobby project. There is no deadline and nothing ships. **Doing it RIGHT matters more
  than saving time, tokens, lines changed, or churn** — the constraints worth optimising against
  elsewhere do not exist here, so optimising against them just produces worse work.
- If a separation is worth doing — logic/simulation from UI, for example — **do it properly and
  keep iterating until we are both happy with it.** Not halfway, not a minimal slice, not "the
  least that still fixes the problem." A half-done separation is worse than none: it leaves two
  conventions and the drift between them.
- Once a design is agreed, implement what it says. Do not offer a reduced version and do not
  defer the design's own open questions for being awkward — answer them, or ask. Cutting scope is
  the player's call, and they will say so. **Asking is fine; quietly shrinking is not.**

## Core Project Guidelines:
- You must create a timestamped dev log file in `docs/devlog` before each commit
- Add identified issues and challenges to the 'docs/CHALLENGES.md' document
- When starting on a new task (usually a new chat), create a `docs/todo/TODO_{TASK}_.md` document and mirror all tasks to mysite StoryWeaver project
- If there are any structural changes to the project, update the `TODO_{TASK}_.md` document and update tasks on mysite StoryWeaver project
- Future work is located in 'docs/todo/TODO_FUTURE_WORK.md', look at it for new tasks and keep it up to date with the ideas we come up with 
- If anything is unclear or you need clarification, you must ask
- NEVER EVER ASSUME ANYTHING, ask me
- Always ask me before starting on a task
- Always work step by step and show reasoning
- Always ask before committing and pushing
- Alwasy ask permisiion before making any structural changes to the project
- Always ask before start writing any code
- Currently any testing will be done by me manually, only automatic test that can be done is build of the project
- Update 'docs/todo/TODO_{TASK}_.md' with any new tasks that you identify during development, update tasks on mysite StoryWeaver project, and check off completed tasks before committing or pushing
- Make sure 'docs/todo/TODO_FUTURE_WORK.md' is up to date after finishing a task and update tasks on mysite StoryWeaver project (all tasks in TODO_{TASK}_.md are completed).
- When a `TODO_{TASK}_.md` is finished, no unchecked item may be left in it. Each one either moves to `docs/todo/TODO_FUTURE_WORK.md` or is struck out with a reason. A task doc is only done when it has no open boxes.
