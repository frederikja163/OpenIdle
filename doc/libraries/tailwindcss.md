# Tailwind CSS

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 4.3.3 (declared `^4.3.0`)

## 1. Problem

The game client needs styling: layout for a dashboard of resource counters, progress bars, inventory grids, modals, and a consistent visual language across screens. The question is whether that needs a CSS framework, or whether [Svelte](./svelte.md)'s built-in component-scoped CSS — already paid for, already present — is sufficient.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **Tailwind CSS 4** (chosen) | 4.3.3, 827 KB core, 0 npm deps — but requires [@tailwindcss/vite](./tailwindcss-vite.md), which pulls the `@tailwindcss/oxide` Rust binary | Utility classes; v4 uses a Rust engine and CSS-first config (`@import 'tailwindcss'`); a **closed** design scale for spacing/colour/type; unused utilities never emitted | Very active: 118M weekly downloads, 2637 releases, latest 2026-07-16, Tailwind Labs | MIT | High: authoring speed, and a design scale that is *enforced* rather than suggested — what a solo project with no design review actually needs |
| **Svelte scoped CSS alone** | 0 bytes | Already built into [Svelte](./svelte.md). Component-scoped by default, no build plugin, no extra packages. Modern CSS (nesting, custom properties, `color-mix`) covers much of what utility frameworks were invented to work around | Same as Svelte | MIT | Medium: costs nothing and is already present, but offers no design scale at all and still requires naming every class |
| Build in-house (Svelte scoped CSS + a custom-property design system) | 0 bytes | Same as above, plus a `:root` token block for colours, spacing, and typography | Us | n/a | Medium: the closest competitor. Loses because the token block is advisory — nothing prevents arbitrary values — and it does not remove the naming or context-switching cost |
| Plain CSS with a naming convention (BEM) | 0 bytes | No dependency; works anywhere | n/a | n/a | Low: Svelte's scoping already solves what BEM solves, so the convention is redundant |
| A component library (DaisyUI, Skeleton, shadcn-svelte) | varies | Pre-built components | Varies | MIT | Low: a game UI is heavily custom; generic components would be fought, not used |

Why the others lost: a component library is the wrong shape for a bespoke game interface, and BEM solves a problem Svelte already solves. The real contest was against Svelte's own scoped CSS plus a hand-written token block — genuinely close, and decided in section 3 on the grounds that a hand-rolled token set is a convention rather than a constraint, and that it leaves the per-component naming and context-switching cost untouched.

## 3. Decision & rationale

Adopt **Tailwind CSS 4**, on two grounds the owner argued explicitly: **authoring speed** and **enforced visual consistency**.

**The challenge this had to answer.** Tailwind's most-cited benefits — scoped styles without naming collisions, and no dead CSS shipped — are largely what Svelte's compiler already provides: component `<style>` blocks are automatically scoped, and unused selectors are reported by [svelte-check](./svelte-check.md). Since built-in scoped CSS was itself a stated reason for choosing [Svelte](./svelte.md) over React, adding a CSS framework on top looked like paying twice, at a cost of three `package.json` entries and the `@tailwindcss/oxide` Rust binary in the build.

**Why the case holds anyway.** The overlap is narrower than it first appears, because scoping is not what Tailwind is actually being bought for here.

*Consistency is enforced, not merely offered.* This is the decisive point and the one most easily under-weighted. The alternative — a `:root` block of CSS custom properties — is **advisory**: nothing stops anyone writing `margin: 13px` next to `margin: var(--space-3)`, and on a solo project with no reviewer, nothing will. Tailwind's scale is a **closed set**: `p-4` exists, arbitrary values require deliberately stepping outside the system via bracket syntax, which is visible in review and in a diff. That is the difference between a convention and a constraint, and conventions are exactly what erode on a project with one developer and no design review. For an idle-game dashboard — dense rows of counters, progress bars, and inventory grids where small misalignments read as sloppiness across hundreds of repeated elements — a constrained spacing and type scale is doing real, continuous work.

*Authoring speed is a first-order concern on this project.* Utility classes remove two per-component taxes that scoped CSS does not: inventing class names, and switching between the markup and the `<style>` block for every change. Svelte's scoping removes the risk of a name *colliding*; it does not remove the need to *think of* one. Across a UI with many small components, that compounds. Owner velocity is the same lever that decided [C# / .NET](./csharp-dotnet.md), where fluency was called the single biggest factor for a solo open-source project — it is consistent to weigh it the same way here.

*The token system is the part that survives the "just write CSS" argument.* A spacing scale, colour ramps, and type sizes that were designed to work together are genuinely valuable to a developer without a designer, and reproducing them by hand means either copying Tailwind's scale (in which case we have Tailwind's design work without its tooling) or inventing our own (in which case we are doing design work we chose Tailwind to avoid).

**What this decision explicitly accepts.** Verbose markup, `high` undo risk, three packages, and a native binary in the build. Those are recorded in Cons and in Risk and are not diminished by the decision going this way. The undo risk in particular is real: this is now a choice that is expensive to reverse once the UI is built out, and it should be treated as settled rather than revisited casually.

### Pros

- **Faster to author**: no class names to invent, no context switch between markup and `<style>` block. Svelte's scoping removes name *collisions*, not the need to think of names.
- **Consistency is enforced rather than suggested**: the scale is a closed set, so arbitrary values require visibly stepping outside it. A hand-rolled custom-property block is advisory and erodes without review.
- A token system designed as a coherent whole (spacing, colour ramps, type scale) gives a developer without a designer a defensible visual baseline.
- Particularly suited to a dense game dashboard, where hundreds of repeated small elements make alignment drift immediately visible.
- v4's Rust engine is fast, and CSS-first configuration removes the old `tailwind.config.js` JavaScript layer.
- Only utilities actually used are emitted, so output CSS stays small.
- Zero npm dependencies in the core package.
- MIT, 118M weekly downloads, Tailwind Labs, very active.

### Cons

- Duplicates [Svelte](./svelte.md)'s built-in scoped CSS, which was itself a stated reason for choosing Svelte over React.
- Requires two companion packages ([@tailwindcss/vite](./tailwindcss-vite.md), [prettier-plugin-tailwindcss](./prettier-plugin-tailwindcss.md)) — three entries for one capability.
- Pulls the `@tailwindcss/oxide` Rust native binary into the build.
- Verbose markup: long class strings make templates harder to read, which is why a class-sorting plugin becomes necessary.
- Highest undo risk in the frontend set — utility classes spread across every component by design.
- v3→v4 was a substantial migration; a future major will likely be another.

## 4. Build-vs-buy

The in-house option is real and cheap, and this is the one place in the frontend set where a library was adopted despite that.

What we would build is not a CSS framework. It is a `:root` block of custom properties — a spacing scale, a colour ramp, font sizes, border radii — plus component-scoped `<style>` blocks in each Svelte component. Roughly 40 lines of CSS, a couple of hours, no dependency, no build plugin, no native binary. Modern CSS covers much of what utility frameworks were originally invented to work around: nesting is standard, custom properties handle theming, `color-mix()` handles ramps, and container queries handle responsive layout. On effort alone, that sits comfortably inside this project's "hours not weeks" threshold, which normally means build.

**Buying wins because the two things being bought are not the ones the effort estimate measures.**

The first is *enforcement*. Writing the token block takes two hours; keeping every component faithful to it takes forever, and on a solo project with no reviewer nothing enforces it. A custom-property block does not prevent `margin: 13px` — it only makes a better alternative available. Tailwind's scale is closed, so leaving it requires bracket syntax that is visible in a diff. The deliverable we would build in two hours is a *suggestion*; what Tailwind provides is a *constraint*, and the gap between those is not closable with more CSS.

The second is *design*. Tailwind's scales are a coherent system, not arbitrary numbers. Hand-rolling them means either transcribing Tailwind's values — taking the design work while rejecting the tooling — or inventing our own, which is design work we do not want to do and are not equipped to do well.

The effort estimate stands and is the honest counterweight: this is a defensible decision, not an obvious one, and it is the frontend dependency most reasonably second-guessed. It is recorded as adopted because the case was argued on merits, not inherited from the scaffold.

## 5. Risk

### Undo risk — high

The highest in the frontend set, and it is a property of the tool rather than of our current usage. Tailwind is used by writing utility classes directly into markup, so once adopted in earnest its usage is spread across every component and removing it means rewriting the styling of the entire UI.

While styling is still ahead of us, removal is deleting three `package.json` entries, one Vite plugin line, one CSS import, and two lines from `prettier.config.js` — perhaps ten minutes. **The `high` rating reflects where this ends up, not where it starts**, and that gap is exactly why the decision is worth making deliberately rather than by default.

### Security risk — low

MIT, Tailwind Labs, actively maintained, no known outstanding CVEs. The core package has zero npm dependencies and no install or postinstall scripts. Build-time only — Tailwind generates a CSS file; no JavaScript reaches the browser, so there is no runtime attack surface at all.

The one item worth naming is the `@tailwindcss/oxide` Rust native binary pulled in via [@tailwindcss/vite](./tailwindcss-vite.md): a prebuilt platform-specific executable that runs during every build and cannot be meaningfully reviewed. This is the same category of exposure as `rolldown` and `lightningcss` under [Vite](./vite.md), and is covered by the same mitigation — exact resolutions and integrity hashes in `bun.lock`, with lockfile changes treated as reviewable. Removing Tailwind would remove one such binary from the build.
