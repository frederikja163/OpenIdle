# tailwind-merge

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 3.6.0

## 1. Problem

When a component defines default utility classes and a caller passes more, the two can conflict: `<Button class="p-8">` against a button whose base is `p-4` produces `class="p-4 p-8"`. CSS does not resolve that by source order in the attribute — it resolves by the order the rules appear in the stylesheet, which [Tailwind CSS](./tailwindcss.md) generates and we do not control. The result is that overriding a component's padding sometimes works and sometimes silently does not, depending on which utility Tailwind happened to emit last. Any component that accepts a `class` prop needs conflicting utilities *removed*, not merely concatenated — which is what [clsx](./clsx.md) alone does.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **tailwind-merge** (chosen) | 3.6.0, 991 KB unpacked, **0 dependencies**. Largest browser-bound cost after [bits-ui](./bits-ui.md) | Knows Tailwind's full utility taxonomy — which classes belong to the same conflict group (`p-*` vs `px-*` vs `pt-*`, `text-*` for both colour and size) — and keeps only the last of each. Handles arbitrary values, modifiers and prefixes | Very active, dcastil; tracks Tailwind releases | MIT | High: solves a problem with no cheap alternative, and is the accepted second half of the `cn()` idiom |
| Build in-house | 0 bytes | Would require reproducing Tailwind's conflict map | Us | n/a | Low: not an hours-scale job, and needs updating with every Tailwind release — see section 4 |
| Don't merge; forbid overrides | 0 bytes | Components accept no `class` prop; all variation goes through explicit props | n/a | n/a | Medium: genuinely coherent and free, but incompatible with shadcn-svelte, whose every component takes `class` |
| Merge naively (last wins by string order) | ~5 lines | Deduplicate by exact class name only | Us | n/a | Low: does not fix the problem, since `p-4` and `p-8` are different strings and both survive |

Why the others lost: naive deduplication does not address conflicts at all, only exact repeats. Forbidding overrides is a real architectural position but is ruled out by the [shadcn-svelte](./shadcn-svelte.md) decision, whose components are built around a `class` prop. Building the conflict map in-house is addressed below.

## 3. Decision & rationale

Adopt **tailwind-merge 3.6.0**. Unlike [clsx](./clsx.md), this one earns its place on its own merits and would be worth considering even without [shadcn-svelte](./shadcn-svelte.md).

The value is not the merging logic, which is simple, but the **data**: a complete map of Tailwind's utility namespace into conflict groups. Knowing that `px-4` overrides `p-4` on one axis but not the other, that `text-red-500` and `text-lg` are the same prefix but different groups, that `[mask-type:luminance]` is an arbitrary property with its own grouping — that map is Tailwind's taxonomy encoded, and it has to move whenever Tailwind moves. That is the asset being bought.

**The cost is real and is the main argument against it.** At 991 KB unpacked it is the single largest browser-bound package in the project after [bits-ui](./bits-ui.md), and it is the dominant part of the 18.4 KB gzip fixed cost that [shadcn-svelte](./shadcn-svelte.md) section 3 measures on the first component. That cost is paid once rather than per component, which is what makes it tolerable.

Like [clsx](./clsx.md), it is reached through exactly **one import site** — the `cn()` helper at `Frontend/src/lib/utils/stylingUtils.ts`. That helper now exists and is imported by the app chrome, so tailwind-merge is already in the client bundle.

### Pros

- Solves a real correctness bug — silently ignored overrides — not merely a stylistic concern.
- Encodes Tailwind's conflict taxonomy, which is data we would otherwise have to maintain by hand.
- Zero dependencies.
- Fixed cost paid once on the first component, not per component.
- Single import site; the rest of the codebase never touches it.
- Actively tracked against Tailwind releases; MIT.

### Cons

- **The heaviest non-bits-ui browser-bound package in the project** and the bulk of the first-component bundle cost.
- Its correctness is coupled to Tailwind's version: a Tailwind major that reorganises utilities requires a matching tailwind-merge release.
- Runs on every render that builds a class string — cheap per call, but not free.
- Would be unnecessary under a design where components do not accept `class` overrides.

## 4. Build-vs-buy

This is the clearest "buy" among the small packages, and the contrast with [clsx](./clsx.md) is instructive: the two sit in the same helper and have opposite answers.

The *algorithm* is easy — group the classes, keep the last of each group, join. The **conflict map is not**. It has to enumerate every Tailwind utility family and decide which families override which: padding against its per-axis and per-side variants, the several unrelated groups sharing the `text-` prefix, borders, rings, gradients, arbitrary values and arbitrary properties, plus modifier and prefix handling. That is days of careful work to draft, and then it is not finished — it is a permanent maintenance commitment that has to be re-verified against every Tailwind release, including the v4-to-v5 migration that [Tailwind CSS](./tailwindcss.md) already anticipates.

A partial map is worse than none, because the failure mode is silent: an unhandled group means an override that quietly does not apply, in one component, discovered visually much later. This project has no design review to catch that.

Buying wins clearly, and it would still win if shadcn-svelte were not in the picture.

## 5. Risk

### Undo risk — low

One import site, in `Frontend/src/lib/utils/stylingUtils.ts`. Removing it is a two-line edit — `cn()` degrades to plain [clsx](./clsx.md) concatenation, which compiles and runs fine. What breaks is not the build but the *behaviour*: class overrides stop reliably winning, silently. The rating reflects mechanical ease; the practical consequence of removal is worse than the rating suggests.

### Security risk — low

MIT, zero dependencies, no install or postinstall scripts, no native binaries, no known CVEs. Attack surface is a pure string transformation — no I/O, no DOM access, no dynamic evaluation. It does ship to the browser, per [shadcn-svelte](./shadcn-svelte.md) section 5. Its size comes from lookup tables rather than executable logic, which keeps the reviewable surface much smaller than the 991 KB unpacked figure implies.
