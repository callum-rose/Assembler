---
name: post-mortem
description: >
  Use this skill when the user wants to wrap up a working session by reviewing what went wrong, what
  was deferred, or what should be followed up on. Trigger on requests like "do a post-mortem", "post
  mortem", "what issues did we hit", "what should we file tickets for", "what did we punt on", "wrap
  up and flag follow-ups", or any end-of-session ask to surface problems found and turn them into
  GitHub tickets. The skill reviews the *current conversation* (not a fresh code audit), briefly flags
  every potential issue it encountered, and then proposes GitHub tickets worth making, ordered by
  priority. Also use it after a long debugging or refactoring session when the user says something
  like "ok we're done — anything we should write up?".
---

# Post-Mortem

Wrap up the current session: look back over everything that happened in *this conversation*, flag the
potential issues that surfaced, and turn the ones worth tracking into prioritised GitHub ticket
suggestions.

This is a **retrospective of the conversation**, not a fresh audit of the codebase. The signal you
want is already in the transcript — bugs you found, hacks you applied under time pressure, things the
user explicitly punted on, surprises in the code, missing tests you noticed but didn't write. Mine
that. Don't go spelunking for brand-new problems unrelated to the work just done (that's what
`/code-review` or `/security-review` are for).

**Be as brief as possible.** This skill runs at the *end* of a session, when the context window is
often already heavily used and your attention is stretched. A long, padded write-up is exactly the
wrong thing here — it burns the remaining budget and buries the signal. Favour terse one-liners over
prose, skip preamble and recap, and don't re-explain things already established in the conversation.
The whole output should be skimmable in seconds. Brevity is the priority, not completeness of phrasing.

## Step 1 — Scan the conversation for issues

Re-read the session with a critical eye and collect anything that could bite later. Useful prompts to
ask yourself:

- **Bugs** — defects found during the work, whether fixed or not. If fixed, was the fix complete or a
  patch over a symptom?
- **Workarounds & hacks** — anything done "for now" to keep moving: hardcoded values, disabled checks,
  commented-out code, `.skip`/`xfail`, copy-paste that should be shared.
- **Deferred / punted work** — things the user or you explicitly said you'd "do later", scope you
  consciously cut, "good enough for this PR" calls.
- **Fragile or surprising code** — places where the existing code behaved unexpectedly, was hard to
  understand, or was clearly going to break under a slightly different input.
- **Missing test coverage** — behaviour you changed or relied on that has no test, especially around a
  bug you just fixed.
- **Tech debt touched** — debt you brushed against and decided not to clean up.
- **Gotchas worth documenting** — tribal knowledge you had to (re)discover that the next person will
  also trip on.
- **Performance / correctness risks** — concerns you noticed but didn't chase down.
- **Confirmed TODO/FIXME** — existing markers you verified are still real and relevant.

Be honest and specific. "We disabled the null check in `Foo.Bar` to get the build green" beats "some
code quality issues". If the session was clean and nothing surfaced, say so plainly rather than
inventing problems.

## Step 2 — Present the flagged issues (briefly)

List what you found as terse one-liners, grouped by the categories above (drop empty groups). Each line
should name the concrete thing and where it lives. Keep this scannable — it's a triage list, not an
essay.

```
## Issues flagged this session

**Bugs**
- `InventorySystem.Stack` double-counted items when the slot was full — patched, but root cause (off-by-one in `TryMerge`) untouched.

**Workarounds**
- Hardcoded `timeout = 30` in `NetClient` to dodge a flaky test; real fix is to make the test deterministic.

**Deferred**
- Undo/redo for the new tag was cut from this PR by agreement.

**Missing tests**
- No coverage for the `!range` parser path added today.
```

## Step 3 — Propose GitHub tickets, ordered by priority

Not every flagged issue deserves a ticket. Collapse duplicates, drop the trivial, and merge closely
related items into one. For the rest, propose tickets **ordered highest-priority first**, with a one-
line justification for the ranking so the user can sanity-check your judgement.

Rank by impact × likelihood × cost-of-delay, roughly:

- **P0 / High** — correctness or data-loss bugs, anything actively broken or a workaround that will
  silently rot. File it now.
- **P1 / Medium** — real debt or missing coverage that will slow the next change in this area.
- **P2 / Low** — nice-to-haves, documentation, speculative cleanups.

For each proposed ticket use this shape — concise, ready to paste into GitHub:

```
### 1. [P0] Fix off-by-one in InventorySystem.TryMerge
**Why now:** root cause of a shipped double-count bug; current fix only masks the symptom.
**Labels:** bug
**Acceptance:** TryMerge merges into a full slot without inflating count; regression test added.

### 2. [P1] Make NetClient timeout test deterministic
**Why:** the 30s hardcode is a workaround for flakiness and will hide real regressions.
**Labels:** test, tech-debt
**Acceptance:** test no longer depends on wall-clock timing; hardcoded timeout removed.
```

Keep acceptance criteria to a line or two — enough to define "done", not a spec.

## Step 4 — Offer to create them

End by asking whether to file any of these. Don't create issues unprompted — the user decides which
are worth it. If they say yes, find the repo with `gh repo view --json nameWithOwner -q .nameWithOwner`
and create the chosen tickets:

```bash
gh issue create --title "Fix off-by-one in InventorySystem.TryMerge" \
  --body "Root cause of a shipped double-count bug..." --label bug
```

Only apply `--label` values that already exist in the repo (`gh label list`); skip or create labels
rather than letting the command fail. Report back the URLs of anything you file.
