# bits-ui

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 2.18.1 (declared `^2.16.3`)

## 1. Problem

Interactive interface elements that are not native HTML — dialogs, dropdown menus, selects, comboboxes, tooltips, popovers — have to reimplement behaviour the browser gives away for free on native elements. Each needs focus trapping and restoration, keyboard semantics (arrow-key roving tabindex, typeahead, `Escape` to dismiss), correct ARIA roles and relationships, outside-click and scroll-lock handling, and floating-element positioning that flips or shifts when it would otherwise overflow the viewport. Getting this wrong produces an interface that looks correct to a sighted mouse user and is unusable with a keyboard or screen reader. This is the behavioural half of the problem [shadcn-svelte](./shadcn-svelte.md) was adopted to solve; bits-ui is what actually solves it.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **bits-ui** (chosen) | 2.18.1, 1.97 MB unpacked, 6 direct deps → ~10 transitive | Headless, unstyled, Svelte-5-native (runes). Full primitive set. Uses `@floating-ui` for collision-aware positioning and `tabbable` for focus management | Active; huntabyte, same maintainer as shadcn-svelte | MIT | High: the primitive set [shadcn-svelte](./shadcn-svelte.md) is built on, so choosing shadcn-svelte chooses this |
| Melt UI | builder-based | Same headless category, builder API rather than components. bits-ui was originally built on top of it | Active | MIT | Medium: comparable capability, but adopting it would mean abandoning shadcn-svelte's component layer |
| Native elements | 0 bytes | `<dialog>` gives focus trap, `Escape`, and top-layer stacking for free. `<select>` is fully accessible | Platform | n/a | Medium: correct and free where it fits, but neither is stylable or composable to the degree the interface will want |
| Build in-house | 0 bytes | Total control, zero weight | Us | n/a | Low: weeks of work, and the failure mode is silent — see section 4 |

Why the others lost: Melt UI is a genuine peer on capability but is not reachable from the [shadcn-svelte](./shadcn-svelte.md) decision, which standardised on bits-ui. Native elements remain the right answer wherever they suffice and should be preferred for simple cases; they do not stretch to a styled combobox or a composed menu. Building in-house is addressed in section 4.

## 3. Decision & rationale

Adopt **bits-ui 2.18.1**. This is largely an *entailed* decision rather than an independent one — it arrives as a dependency the moment any interactive [shadcn-svelte](./shadcn-svelte.md) component is added, and it is documented separately because it is the single heaviest item in that set, carries the deepest transitive tree in the project, and is independently substitutable in a way the rest of the set is not.

It is worth recording that it earns its place on its own merits. bits-ui is headless: it supplies behaviour and ARIA wiring and no styling whatsoever, which means it does not compete with [Tailwind CSS](./tailwindcss.md) and leaves the visual layer entirely ours. It is runes-native rather than retrofitted onto Svelte 5, which matters given this project forces runes mode for all non-`node_modules` code. And its own dependencies are well-chosen rather than incidental: `@floating-ui` is the de-facto standard for collision-aware positioning and `tabbable` for focus-order computation, both of which are exactly the fiddly, browser-quirk-laden problems worth delegating.

**It is only pulled in on demand.** Adding `button` alone does not install it — the shadcn-svelte CLI adds it when the first component that needs it is added, which during evaluation was `dialog`. For a time it was declared in `package.json` and resolved in `bun.lock` while no component imported it, so it contributed no client bytes.

**That is no longer true: bits-ui now ships.** `dialog` was vendored for the delete-confirmation modal on the profiles page, and it is the only thing that imports the library. Re-measured on the app as it stands, it moved total client JavaScript from 67.6 KB to **82.5 KB gzip (+14.9 KB)** — close to, and slightly under, the 17.2 KB this document predicted. Confining interactive primitives to application chrome, as [shadcn-svelte](./shadcn-svelte.md) section 3 requires, is what keeps this dependency's blast radius contained now that it is real.

### Pros

- Solves the accessibility work that is genuinely hard and genuinely easy to get silently wrong.
- Headless — no styling opinions, no conflict with Tailwind, visual layer stays ours.
- Svelte 5 runes-native, matching this project's enforced runes mode.
- Delegates positioning to `@floating-ui` and focus order to `tabbable`, both mature and well-targeted.
- MIT; same maintainer as shadcn-svelte, so the two stay in step.
- No install or postinstall scripts; no native binaries.

### Cons

- The heaviest single browser-bound addition: ~17.2 KB gzip of client JS for a dialog alone, measured on this project.
- Deepest transitive tree in the project — roughly ten packages arrive behind it.
- Declares [@internationalized/date](./internationalized-date.md) (Apache-2.0) as a **peer dependency**, so it must be listed in our `package.json` even though only date components — which we do not use — would ever import it.
- `svelte-toolbelt`, one of its transitives, ships no `license` field in `package.json` (MIT in its LICENSE file).
- Pre-3.0 in spirit: a `2.x` line under active development, so minor-version churn is likelier than in the settled parts of the stack.
- Unlike the shadcn-svelte components, this is a real imported package — it is *not* vendored source we own, which is what makes it the sticky part of the adoption.

## 4. Build-vs-buy

Building this in-house is the clearest "no" in the frontend set.

The work is not conceptually difficult, which is precisely what makes it dangerous to estimate: a dialog looks like an afternoon. A correct one is not. Focus must be trapped on open and restored to the invoking element on close; the background must be made inert to assistive technology rather than merely visually covered; scroll must lock without layout shift from the disappearing scrollbar; `Escape` and outside clicks must dismiss without swallowing events the page needs. A select or combobox adds roving tabindex, typeahead matching, and positioning that flips above the trigger near the viewport bottom and shifts sideways near its edge — `@floating-ui` exists as a substantial library because that problem is substantial.

Realistically that is several weeks to reach a defensible standard across the primitives this interface will need, against a rule of thumb that says build only what fits in hours. Worse, the verification is out of reach: correctness here is defined by screen-reader and keyboard-only behaviour across browsers, and this project has no way to test that and no reviewer who would catch it. The failure mode is not a broken build — it is an interface that works perfectly for the developer and excludes some users entirely.

Buying wins decisively. This is the dependency that justifies the whole [shadcn-svelte](./shadcn-svelte.md) adoption.

## 5. Risk

### Undo risk — medium

This is the genuinely sticky half of the shadcn-svelte adoption. The components themselves are vendored source we own and can keep indefinitely, but they `import` bits-ui, and that import cannot be deleted without rewriting the component's behaviour from scratch. Once a number of screens use `<Dialog>`, `<Select>` and dropdown menus, removal means either reimplementing the primitives — the multi-week job section 4 rejected — or migrating to Melt UI, which is a comparable but non-trivial API change.

Held at `medium` rather than `high` by the scope condition: interactive primitives are confined to application chrome, and the game UI proper does not use them. Simple cases should keep preferring native `<dialog>` and `<select>`, which limits how far this spreads.

The delete-confirmation modal on the profile card is a knowing exception to that last sentence, and it is recorded here rather than left to contradict the guidance silently. A confirm step is close to the canonical simple case, and native `<dialog>` would have cost nothing; the owner chose the vendored component anyway, with the +14.9 KB measured and on the table, for composability with the rest of the shadcn set. The guidance still stands for the next one — this is one exception, not a new default, and the count of importers is the number to watch.

### Security risk — medium

Browser-bound code, so a compromise reaches players rather than just the build. It carries the deepest transitive tree in the project — ten packages, as resolved in `bun.lock`:

| Package | Version | License | Arrives via |
|---|---|---|---|
| `runed` | 0.35.1 | MIT | direct |
| `esm-env` | 1.2.2 | MIT | direct |
| `tabbable` | 6.5.0 | MIT | direct |
| `svelte-toolbelt` | 0.10.6 | **undeclared** (MIT in LICENSE file) | direct |
| `@floating-ui/dom` | 1.8.0 | MIT | direct |
| `@floating-ui/core` | 1.8.0 | MIT | direct |
| `@floating-ui/utils` | 0.2.12 | MIT | `@floating-ui/*` |
| `dequal` | 2.0.3 | MIT | `runed` |
| `lz-string` | 1.5.0 | MIT | `runed` |
| `style-to-object` | 1.0.14 | MIT | `svelte-toolbelt` |

Separately, bits-ui declares [@internationalized/date](./internationalized-date.md) (3.12.3, Apache-2.0) as a **peer** dependency, so it appears in our own `package.json` rather than under this subtree; it brings `@swc/helpers` 0.5.23 (MIT). Nothing imports it today, so it contributes no client bytes — see that document.

All licences are compatible. The `svelte-toolbelt` metadata gap is noted in section 3's cons — benign in substance, but automated licence tooling will flag it and it is worth knowing why in advance.

Mitigating factors: no `preinstall`/`install`/`postinstall` scripts anywhere in this tree, preserving the project's standing invariant; no native binaries; exact resolutions with integrity hashes in `bun.lock`; no known outstanding CVEs as of this date. `@floating-ui` and `tabbable` are widely deployed across the React and Vue ecosystems and correspondingly well-scrutinised. The weaker links are the smaller Svelte-specific packages (`runed`, `svelte-toolbelt`), which share a maintainer with bits-ui itself — meaning a single maintainer account compromise would reach several of these at once. Treat lockfile changes across this set as reviewable events, consistent with the rest of the project.
