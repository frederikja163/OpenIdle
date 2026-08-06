# shadcn-svelte

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 1.5.0, preset `vega` (`--preset bIkeymG`), base colour `neutral`, icon library `lucide`

## 1. Problem

The game client needs the ordinary application interface that surrounds the game itself: a login screen, a profile picker, settings, forms, and eventually modals and menus. Two parts of that are genuinely hard. The first is **accessible interactive primitives** — dialogs, selects, dropdowns, comboboxes and tooltips need focus trapping, keyboard navigation, ARIA wiring, click-outside handling, scroll locking, and floating-element positioning that survives being near a viewport edge. The second is a **visual baseline** — a coherent set of colour, elevation and radius tokens plus a dark mode, decided by someone other than a developer with no designer. [Tailwind CSS](./tailwindcss.md) supplies a spacing and colour *scale* but no components and no semantic tokens, so neither of these is currently solved.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **shadcn-svelte** (chosen) | 1.5.0; CLI 903 KB. Adds 6 direct + ~11 transitive packages, of which most reach the browser | **Not a library — a code generator.** The CLI copies component *source* into `src/lib/components/ui/`, which we then own and edit. Wraps [bits-ui](./bits-ui.md) for behaviour, Tailwind for style. Ships a semantic token layer (`--background`, `--primary`, …) and a dark variant | Very active; huntabyte + shadcn ecosystem, tracks shadcn/ui upstream | MIT | High: solves both problems at once, and because the output is our source there is no abstraction to fight later |
| [bits-ui](./bits-ui.md) alone | 2.18.1 | The headless primitives without any styling or token layer. Would be adopted anyway — shadcn-svelte depends on it | Active | MIT | Medium: solves the hard half (accessibility) but leaves the visual baseline entirely to us, which was an explicit driver |
| Melt UI | builder-based headless kit | Same headless category as bits-ui, different API shape (builders rather than components). bits-ui was originally built on it | Active | MIT | Medium: equivalent capability, but reaching it through shadcn-svelte is not an option here — shadcn-svelte standardised on bits-ui |
| Skeleton | full framework | Pre-built, pre-styled components consumed as a package | Active | MIT | Low: a real component *library*. Its components are imported, not owned, so customising a game UI means fighting or overriding someone else's markup |
| Build in-house | 0 bytes | Hand-written primitives against Tailwind's scale, in the style of the existing `Row.svelte` | Us | n/a | Split: fine for Button/Card/Input, poor for Dialog/Select/Combobox — see section 4 |
| Do nothing (native elements) | 0 bytes | `<dialog>`, `<select>`, `<details>` — real accessibility for free from the platform | n/a | n/a | Low–medium: genuinely viable for simple cases and the honest baseline, but `<select>` and `<dialog>` cannot be styled or composed to the degree a game UI will want |

Why the others lost: Skeleton is the wrong *shape* — it hands us components we import rather than own. bits-ui alone and Melt UI solve accessibility but not theming, and bits-ui arrives anyway as a dependency of the chosen option, so choosing it alone would forfeit the token layer for no saving. Native elements are the strongest zero-cost answer and remain the right call for anything simple, but do not compose or style well enough for the whole interface. Build-in-house is addressed on its merits in section 4, where it wins for the simple half and loses for the hard half.

## 3. Decision & rationale

Adopt **shadcn-svelte 1.5.0**, on the three drivers the owner named: accessible interactive primitives, a visual baseline, and speed on application chrome.

**This reverses an earlier recorded position, and the earlier reasoning was wrong.** [Tailwind CSS](./tailwindcss.md) lists "a component library (DaisyUI, Skeleton, shadcn-svelte)" among its rejected alternatives, dismissed on the grounds that "a game UI is heavily custom; generic components would be fought, not used". That objection is sound against DaisyUI and Skeleton and misplaced against shadcn-svelte, because shadcn-svelte is not a component library in that sense. Its CLI writes component source files into our repository; from that moment they are ordinary project files under version control, indistinguishable from hand-written ones. There is no package boundary to fight and no override layer to maintain — to change a component you edit it. The rejected-alternatives row in that document has been corrected accordingly.

**What actually justifies the cost is the accessibility half.** A styled button is a morning's work and would not come close to justifying this. A correct dialog is not: focus must be trapped and restored, `Escape` and click-outside must dismiss, background scroll must lock, the rest of the page must be inert to screen readers, and a combobox or dropdown adds roving-tabindex keyboard semantics and floating-element positioning that flips and shifts near viewport edges. That is the work being bought, it is measured in weeks rather than hours, and it is the kind of code that is quietly wrong rather than visibly broken — a solo project with no reviewer and no screen-reader testing will not discover the defects. That clears this project's "hours not weeks" build threshold decisively; nothing else here does.

**What this decision explicitly accepts is client-side weight, and it is substantial.** This is the first dependency in the frontend set whose code is shipped to the browser, which retires the "zero runtime dependencies" property recorded in the index. Measured on this project with a production build, total client JavaScript:

| Page | Client JS (gzip) | Delta vs baseline |
|---|---|---|
| Baseline, no shadcn components | 29.8 KB | — |
| One `<Button>` | 48.2 KB | **+18.4 KB (+62%)** |
| `<Button>` + `<Dialog>` | 65.4 KB | **+35.6 KB (+120%)** |

Those figures were measured during evaluation, with `button` and `dialog` vendored. **`button` is now vendored** at `src/lib/components/ui/button/`, re-tabled against the design system's own Button spec, so the first-component cost above is being paid. `dialog` is not, and [bits-ui](./bits-ui.md) therefore still reaches nothing; components continue to be added on demand with `shadcn-svelte add`.

The first component is the expensive one: `tailwind-merge` and `tailwind-variants` are a fixed cost paid on first use, not per component. Adding the dialog — and with it [bits-ui](./bits-ui.md) — costs a further 17.2 KB. Doubling baseline client JavaScript for two components is a real price, it is being paid knowingly, and for an idle game whose sessions are long and whose bundle is cached after first load it is judged acceptable. It would not be acceptable for a landing page.

**Scope discipline is the condition attached to this.** shadcn-svelte supplies application chrome. It supplies essentially nothing for the game proper — no resource counters, no progress bars, no inventory grids — and those remain hand-written against Tailwind's scale. The owner did not claim it for the game UI, and that boundary is what keeps the undo risk at `medium` rather than `high`: the dependency stays confined to the surrounding screens rather than spreading into the game itself. Adopting it as a general answer to "how do we build UI" would invalidate this analysis.

### Pros

- Buys weeks of accessibility work — focus management, keyboard semantics, ARIA, scroll locking, floating positioning — that is hard to get right and harder to notice getting wrong.
- Components are **vendored source we own**, not an imported package: no abstraction to fight, no override layer, fully editable, visible in diffs.
- Ships a semantic token layer (`--background`, `--primary`, `--ring`, …) and a dark-mode variant, which is the visual baseline this project has no designer to produce.
- The CLI is a scaffolding tool, so most of the surface can be removed later by simply not running it again.
- Everything lands in `devDependencies`, consistent with the existing convention.
- Fonts are self-hosted — no external CDN request, no third-party origin at runtime. The preset runs on the OpenIdle Design System's typefaces (see the [typefaces section](./README.md#typefaces)).
- MIT, actively maintained, tracks the much larger shadcn/ui ecosystem upstream.
- No install or postinstall scripts anywhere in the added tree — the project's standing invariant survives.

### Cons

- **Retires the "zero runtime dependencies" property.** This is the first third-party code shipped to users' browsers.
- Doubles baseline client JavaScript at two components (+35.6 KB gzip), with an 18.4 KB fixed cost on the very first one.
- Adds 6 direct and ~11 transitive packages; via [bits-ui](./bits-ui.md) this is the deepest dependency tree in the project.
- The CLI **fetches code over the network and writes it into our source tree** — a distinctive supply-chain shape addressed in section 5.
- Generated components do not match the project's Prettier config (double quotes) and tripped `svelte/no-navigation-without-resolve`; both needed handling, and the fix must live in `eslint.config.js` rather than in-file because `shadcn-svelte update` regenerates these files wholesale.
- Updates are a manual, diff-reviewing exercise, not a version bump — the cost of owning the source.
- `style` is marked deprecated under Tailwind v4 in the config schema while the CLI still writes it, so the config format is not fully settled.

## 4. Build-vs-buy

The honest answer differs by component, and the decision follows that split rather than papering over it.

**The simple half we could build, and the estimate is not close.** A `Button` with variants is perhaps an hour against Tailwind's scale; `Card`, `Input` and `Label` are less. The existing `Row.svelte` shows the pattern already works. Judged on effort alone this sits far inside "hours not weeks" and the rule says build. Taken in isolation, `cn()` plus four small components would not justify a single package, let alone seventeen.

**The hard half we could not, and that is what decides it.** A production-quality `Dialog` — focus trap with restoration, `Escape` and outside-click dismissal, scroll lock, `aria-modal` with the background inert, portalling — is days of work to write and considerably more to verify. `Select` and `Combobox` are worse: roving tabindex, typeahead, virtualisation of long lists, and collision-aware floating positioning. Realistically this is several weeks to reach a standard a competent screen-reader user would accept, and this project has no way to test that. It is also failure-prone in the specific way that matters most: the bugs are invisible to a sighted developer using a mouse. This is squarely outside the build threshold.

**Buying wins because the two halves cannot be separated cheaply.** Taking bits-ui for the hard half while hand-writing the easy half is a coherent position, and it would save very little: bits-ui is the bulk of the weight, and the remaining packages (`clsx`, `tailwind-merge`, `tailwind-variants`) are the fixed cost of the `cn()`/variants pattern that any consistent component set converges on anyway. Having paid for bits-ui, the marginal cost of also taking shadcn's buttons, its token layer and its dark mode is small — and the token layer was an independently stated driver that hand-writing does not address at all.

The build-in-house case therefore genuinely wins for Button, Card and Input in isolation, and is recorded here as the reason to keep the adoption **scoped to application chrome**. It loses overall because the components that justify the dependency are the ones we cannot responsibly write.

## 5. Risk

### Undo risk — medium

Lower than it looks, because the components are our source files rather than imported symbols. Removing shadcn-svelte does not require removing the components — deleting the CLI from `devDependencies` and never running it again leaves a working, self-maintained component set behind. The `components.json` file and the registry connection are the only parts that vanish cleanly.

What is genuinely sticky is the shape rather than the package: `cn()` and the `tailwind-variants` idiom propagate into every component that follows the house pattern, and every interactive component imports [bits-ui](./bits-ui.md), which is a real library and not vendored. Once a dozen screens use `<Dialog>` and `<Select>`, replacing bits-ui means rewriting them.

It is `medium` rather than `high` on the strength of the scope condition in section 3 — confined to application chrome, with the game UI hand-written. If that boundary erodes and shadcn becomes the default answer for game components too, this rating should be raised to `high` and the decision revisited.

### Security risk — medium

The highest in the frontend set, for three reasons that compound.

**This code executes in users' browsers.** Every other frontend dependency is build-time only; a compromise there is a compromise of our build machine. These packages ship, so a compromise reaches every player. That is a categorical change in exposure, not a matter of degree.

**The tree is the deepest in the project.** Seven direct additions survive today (`shadcn-svelte`, `bits-ui`, `clsx`, `tailwind-merge`, `tailwind-variants`, `tw-animate-css`, `@lucide/svelte`, plus the peer `@internationalized/date`), pulling roughly eleven transitives, enumerated in [bits-ui](./bits-ui.md). The preset's eighth, `@fontsource-variable/inter`, is gone: the typeface slot is now held by the three Fontsource packages the design system requires — [@fontsource/chakra-petch](./fontsource-chakra-petch.md), [@fontsource-variable/ibm-plex-sans](./fontsource-variable-ibm-plex-sans.md) and [@fontsource/ibm-plex-mono](./fontsource-ibm-plex-mono.md), all 5.3.0 and all in `bun.lock`. That is three font packages where the preset left one, so the shipped-font surface grew rather than shrank; each contributes CSS and woff2 only, with no JavaScript. One qualification on the list above: [clsx](./clsx.md) was **already in the tree** as a dependency of [Svelte](./svelte.md) itself — the CLI promoted it to a direct declaration but did not introduce it, so it is not new exposure. Licensing was checked across all of them: MIT throughout except `@lucide/svelte` (ISC), `@internationalized/date` (Apache-2.0) and the three typefaces (OFL-1.1) — all compatible. `svelte-toolbelt` declares **no `license` field in its `package.json`**, though its bundled LICENSE file is MIT under the same maintainer as shadcn-svelte itself. Benign, but it is a metadata gap in a project that tracks licence terms, and automated licence tooling will flag it.

**The CLI writes fetched code into our source tree.** `shadcn-svelte add` retrieves component source from `https://shadcn-svelte.com/registry` and writes it into `src/`. A registry compromise, or a hijacked domain, injects code directly into our repository rather than into `node_modules`. The mitigation is unusually good, though, and is the reason this is not `high`: everything the CLI writes lands in a reviewable diff before it is committed, which is strictly more visible than a transitive package update. **Treat `shadcn-svelte add` output as untrusted input and read the diff before committing it** — that habit is the control, and it only works if it is actually followed.

Mitigating factors: no `preinstall`/`install`/`postinstall` scripts anywhere in the added tree, so the project's standing "no install scripts" invariant still holds and the appearance of one remains a red flag. No native binaries are added. `bun.lock` pins exact resolutions with integrity hashes. No known outstanding CVEs against any package in the set as of this date. Fonts are self-hosted, so no third-party origin is contacted at runtime.
