---
name: running-tests
description: When to run the test suite — at checkpoints (large features, reviews, end of a branch), not after every prompt. Use whenever about to run vitest, playwright, `bun run test`, or deciding whether a change needs verifying.
---

# Running tests

The suite is a checkpoint, not a save button. Running it after every edit costs minutes,
floods the transcript, and tells you nothing the previous run didn't.

## Run tests at these three points

1. **After a large feature lands** — a new route, a store, a component with real behaviour,
   a refactor that moves code across files. Not after each edit that builds toward it.
2. **When doing a review** — a code review, a self-review before handing work back, or when
   the user asks whether something works.
3. **At the end of development on a branch** — before the branch is proposed as done, and
   before opening a PR.

Between those points, just make the change and say what changed. If the user asks for tests,
run them — an explicit request always wins.

## The one exception

When you are actively working *on* a test — writing it, or debugging a failure you already
know about — run that file alone and iterate:

```shell
bun run test:unit -- --run src/lib/ws/protocol.spec.ts
bun run test:e2e e2e/chrome.e2e.ts
```

That is a tight loop on one file, not a suite run, and it stops when the test is green.

## What to run at a checkpoint

```shell
bun run test        # test:unit --run + test:e2e — the full checkpoint
bun run check       # svelte-check, when types moved
bun run lint        # prettier + eslint, before a PR
```

`bun run test:e2e` needs browsers installed once (`bun run test:e2e:setup`).

## When a run fails

Report the failure with the actual output — never summarise a red run as "mostly passing".
Fix the cause, re-run only the failing file until it is green, then re-run the full suite
once to confirm nothing else moved.
