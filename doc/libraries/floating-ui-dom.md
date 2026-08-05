# @floating-ui/dom

- Status: adopted
- Date: 2026-08-05
- Decided by: project owner
- Version / commit pinned: 1.8.0 (declared `^1.8.0`)

## 1. Problem

The design-system tooltip (`Tooltip.svelte`) positions its panel absolutely inside a `relative` wrapper. Every parent on the game board clips it: the inventory well and the skill grid scroll internally (`overflow-y-auto`), and the board itself hides overflow — so tooltips at the top row are cut off above their anchor and tooltips at the bottom row are hidden behind the well's edge. No `z-index` can fix this, because `overflow` clipping applies regardless of stacking: the only way out is to render the panel somewhere no ancestor can clip it — appended to `<body>` — and to position it from the trigger's screen coordinates with `position: fixed`.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **@floating-ui/dom** (chosen) | 1.8.0, ~10 KB, 2 direct deps | De-facto standard for collision-aware floating positioning. Handles viewport flipping/shifting, `autoUpdate` across scroll and resize, and gives exact x/y in fixed coordinates | Active; the same library [bits-ui](./bits-ui.md) already delegates positioning to | MIT | High: already resolved in `bun.lock` behind bits-ui, so it adds zero new packages |
| bits-ui `Tooltip` | 2.18.1, ~17.2 KB gzip | A full accessible tooltip primitive with floating-ui built in | Active | MIT | Medium: would replace the design-system tooltip wholesale, and bits-ui is scoped to application chrome — the game UI must not pull it in |
| Hand-rolled portal | 0 bytes | Fixed positioning from `getBoundingClientRect()` on open, teleported to `<body>` | Us | n/a | Medium: fine while nothing moves, but no viewport-edge flipping and the panel goes stale if the user scrolls a panel while hovering |

Why the others lost: bits-ui's own tooltip is the *right* tool for application chrome but the [bits-ui](./bits-ui.md) adoption is conditioned on the game UI staying free of interactive primitives, so it cannot be the vehicle here. Hand-rolling reproduces the exact class of bug the project already argued against in the [bits-ui](./bits-ui.md) build-vs-buy section — positioning that flips or shifts near the viewport edge is the hard part, and floating-ui exists because it is.

## 3. Decision & rationale

Promote **@floating-ui/dom 1.8.0** from a transitive dependency of [bits-ui](./bits-ui.md) to a direct declaration, and use it to teleport the tooltip panel to `<body>` and position it as `fixed`.

It is already the positioning engine in this dependency tree — [bits-ui](./bits-ui.md) documents it as its own delegation for collision-aware placement — so this costs no new packages, no new transitive tree, and no new licence surface. The tooltip keeps its existing API (`title`, `meta`, `rows`, `side`, `children`) and its whole visual layer; only the positioning changes, from "absolute inside the wrapper" to "fixed at the trigger's coordinates, flipping or shifting when the viewport edge would cut the panel off". The `oi-pop` entrance animation is preserved by applying floating-ui's coordinates to `left`/`top` rather than `transform`, so the scale keyframes never fight the position.

### Pros

- Fixes the reported defect at its root: a body-portaled `fixed` element cannot be clipped or buried by any board ancestor.
- Reuses the positioning library the project already adopted and documented via [bits-ui](./bits-ui.md), rather than hand-rolling the fiddly part.
- `flip` and `shift` middleware fix a second, latent bug the old fixed-placement code had: tooltips near the viewport edge overflowed the screen.
- `autoUpdate` keeps the panel glued to its trigger across panel scrolling and window resizing.
- Zero bundle change beyond what bits-ui already ships — the package is already on disk and already hashed in `bun.lock`.

### Cons

- One more direct declaration, and the game board (previously floating-position-free) now ships a positioning dependency to the browser.
- `autoUpdate` and the async `computePosition` add a small amount of machinery a naive "absolute + z-index" component would not have had — justified because that approach cannot work here.
- Duplicates the positioning logic bits-ui would supply if the tooltip were ever rewritten on the primitive layer; the two must not drift in behaviour.

## 4. Build-vs-buy

The in-house estimate is modest for the happy path — an open handler that reads `getBoundingClientRect()`, appends a fixed node to `<body>`, and positions it — but that version has exactly the two failure modes the project has already ruled out paying for: no collision handling near viewport edges, and stale positioning when the trigger moves under a scroll. Recovering either means reimplementing `flip`, `shift`, and `autoUpdate`, which is the several-hours-to-wrong answer [bits-ui](./bits-ui.md) section 4 describes in the abstract. Buying is ~10 KB already in the tree.

## 5. Risk

### Undo risk — low

Confined to a single component: `Tooltip.svelte` is the only importer, and the import is a plain function call rather than a component API other code reaches. Reverting means restoring the old absolute-positioned panel, or swapping the internals for any other positioning strategy, without touching any consumer.

### Security risk — low

Already present in the tree and hashed in `bun.lock` since the [bits-ui](./bits-ui.md) adoption, so this decision changes the attack surface not at all. `@floating-ui/dom` is MIT, one of the most widely deployed positioning libraries in the ecosystem (the React and Vue floaters), pure DOM reading and style writing with no `innerHTML`, no install/postinstall scripts, and no native binaries. It ships nothing that touches input validation or storage. Lockfile changes remain reviewable events per the project's standing practice.
