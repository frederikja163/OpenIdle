---
name: document-library
description: Use when the user wants to add, replace, or evaluate a third-party library, package, or dependency for this project — trigger words like "add a library", "use package X", "npm install Y", "need a library for Z", "dependency", "what should we use for". Also use when reviewing or auditing an existing library decision. Produces a decision document under doc/libraries/ recording the problem, an alternatives comparison table, pros/cons, a build-vs-buy analysis, and a low/medium/high risk assessment.
---

# Library Decision Documentation

This project uses as few third-party libraries as possible. Every dependency must justify its existence. This skill produces the documentation that records that justification and forces the decision to be defended before it is accepted.

## Core principles (enforced)

1. **Smaller is better.** All else equal, prefer the smallest dependency that does the job.
2. **Build before buy.** If the needed code can plausibly be written in a short amount of time — a reasonable rule of thumb is one focused feature, hours not weeks — writing it in-house is preferred and adding a library is NOT a good idea. Default position: do not add the library until the case is made.
3. **Every library needs a reason.** "It's popular", "I'm used to it", or "everyone uses it" is not a reason. The reason must name the specific problem the library solves that we don't want to solve ourselves.
4. **Transitive dependencies count.** Evaluate the dependency tree, not just the top-level package. A tiny package that pulls in 50 transitive deps is a big package.
5. **Push back.** If the user's reasoning is wrong, weak, or unexamined, challenge it. Force them to defend the position. The doc is only written once the decision holds up.
6. **"No library" is a valid outcome.** If the analysis lands on "don't add anything" or "build it ourselves", write that conclusion down in doc/libraries/ anyway. Documented non-decisions are just as valuable.

## Workflow

### Step 1: Pin down the problem
Ask the user to state, in one sentence, the problem the library is meant to solve. Then pin down specifics:
- What exact functionality is needed?
- How much of the library's surface would we actually use? (We often use 10% of a library.)
- Is this needed now, or is it just anticipated? (Anticipated needs are better documented than added to.)
- What constraints apply? (language/runtime, platform, license, bundle size, performance.)

Use the `question` tool where a real choice exists. Don't make decisions for the user silently.

### Step 2: Build the alternatives table
Find 3-5 genuine alternatives, always including "build in-house" and, when appropriate, "no library at all / do nothing". Research with `websearch`/`webfetch` — do not rely on memory for version numbers, feature sets, or license terms.

Build a table where columns are the attributes that actually differentiate the options:

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|

Recommended minimum columns: what it is, what makes it different, maintenance/community health, license, and fit for our use case. Add columns (performance, platform support, bundle size, security posture, API stability) only when they actually differentiate. The table is the core artifact — it forces a real comparison instead of vibes.

### Step 3: Challenge the decision (pushback)
Before writing anything, interrogate the candidate choice:
- "Why this library and not the alternatives in the table?"
- "Could we build this ourselves in a short amount of time?" If yes, say so and argue for in-house. The user must then defend why the library still wins.
- "What is the total weight including transitive dependencies?"
- "What happens if this library is abandoned tomorrow? Who maintains the fork?"
- "What assumptions are being made that should be double-checked?" — license compatibility, platform support, security track record, performance characteristics, API stability.
- If the user asserts something wrong or unverifiable, question it explicitly and ask for the source. Never let a bad choice pass silently to keep the peace.

### Step 4: Write the documentation
Create `doc/libraries/<lowercase-kebab-name>.md` using the structure in the template section below (also available at `doc/libraries/TEMPLATE.md`). One file per decision. If a file for that library already exists, update it rather than creating a duplicate. If `doc/libraries/` does not exist yet, create it along with `README.md` (an index of all decisions and their current status) and `TEMPLATE.md`.

### Step 5: Verify
- All five required sections are present.
- The alternatives table has at least "build in-house" plus one other real option.
- Risk ratings use exactly `low`, `medium`, or `high` — nothing else.
- The recommendation matches the analysis (no internal contradictions).
- The chosen option's version or exact commit is pinned in the doc header.
- The index at `doc/libraries/README.md` reflects the new entry.

## Document template (required structure)

```markdown
# <Library name>

- Status: <adopted | rejected | in-house | under-review>
- Date: <YYYY-MM-DD>
- Decided by: <person or "collective">
- Version / commit pinned: <exact version or commit, or "n/a">

## 1. Problem

What problem are we trying to solve? Why does it need a solution at all? One clear paragraph; no jargon.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|

Brief note on why each non-chosen alternative lost.

## 3. Decision & rationale

Which option was chosen and why. Then the honest pros and cons of the chosen option.

### Pros

- ...

### Cons

- ...

## 4. Build-vs-buy

Rough effort estimate for building the equivalent in-house (e.g. "~2 days to cover the 10% of features we use"). Why buying won — or why building won, or why nothing was added at all.

## 5. Risk

### Undo risk — <low | medium | high>

How hard would it be to remove or replace this later? Is its use confined to one module or spread everywhere?

### Security risk — <low | medium | high>

Known CVEs, maintenance/abandonment status, attack surface, supply-chain considerations (e.g. publish frequency, maintainer count, whether it builds native binaries).
```

## Pushback rules

- Be direct, not rude. Name the specific flawed assumption.
- Use evidence from research — versions, sizes, licenses, CVEs, maintenance activity — not general impressions.
- If the user cannot defend the choice after being challenged, treat the choice as rejected and document that outcome instead.
- Never "yes" a library by default. The default answer is no until the case is made.

## Conventions

- File naming: `doc/libraries/<lowercase-kebab-name>.md`
- One decision per file. Decisions stay on record even if later reversed (update Status + add a note instead of deleting).
- The index lives at `doc/libraries/README.md`, listing each decision with status and date.
