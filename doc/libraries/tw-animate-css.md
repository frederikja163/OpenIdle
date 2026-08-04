# tw-animate-css

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 1.4.0

## 1. Problem

[shadcn-svelte](./shadcn-svelte.md) components animate on open and close using state-driven utility classes — `data-[state=open]:animate-in`, `data-[state=closed]:fade-out-0`, `zoom-in-95`, `slide-in-from-top-2`. [Tailwind CSS](./tailwindcss.md) v4 core ships `animate-spin`, `animate-pulse` and friends but none of the enter/exit primitives these classes refer to. Without them the dialog still works and is still accessible, but the utilities resolve to nothing and it appears and disappears instantly.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **tw-animate-css** (chosen) | 1.4.0, 46 KB unpacked, **0 dependencies**. Pure CSS — **no JavaScript reaches the browser** | Tailwind v4 native, distributed as a CSS file imported via `@import`. Supplies `animate-in`/`animate-out` plus fade, zoom, slide and spin modifiers. The maintained successor to `tailwindcss-animate` | Active; the package shadcn/ui itself moved to for v4 | MIT | High: exactly the missing utilities, at zero runtime cost |
| `tailwindcss-animate` | 1.0.7 | The v3-era original these class names came from | **Effectively unmaintained**; predates Tailwind v4's CSS-first config | MIT | Low: superseded, and built for the old JavaScript-config model |
| Build in-house | 0 bytes | Hand-write the `@keyframes` and utility classes into `layout.css` | Us | n/a | Medium: very achievable — see section 4 |
| No animation | 0 bytes | Delete the animation classes from generated components | n/a | n/a | Medium: costs nothing and breaks nothing functionally, but the classes return on every regeneration |

Why the others lost: `tailwindcss-animate` is the abandoned predecessor and is built for a configuration model Tailwind v4 no longer uses. Dropping animation entirely is viable but fights the generator, since `shadcn-svelte update` reinstates the class names. Hand-writing the keyframes is the real alternative, considered below.

## 3. Decision & rationale

Adopt **tw-animate-css 1.4.0**. This is the cheapest addition in the [shadcn-svelte](./shadcn-svelte.md) set and the only one that costs the browser nothing.

**It is pure CSS.** The package is consumed by a single `@import 'tw-animate-css';` line in [layout.css](../../Frontend/src/routes/layout.css) and contributes no JavaScript whatsoever. Tailwind emits only the utilities actually used, so the 46 KB unpacked figure is not what ships — the handful of classes the dialog references is. It therefore sits outside the client-weight concern that governs the rest of the set, and belongs in the same category as [Tailwind CSS](./tailwindcss.md) itself: build-time tooling that produces CSS.

The alternative of writing the keyframes by hand is close to viable, and the argument against it is regeneration rather than effort — the same one that decided [clsx](./clsx.md), but weaker here because the cost is genuinely near zero.

### Pros

- **No JavaScript reaches the browser** — a CSS `@import`, nothing more.
- Only the utilities actually used are emitted, so real cost is a few hundred bytes of CSS.
- Purpose-built for Tailwind v4's CSS-first configuration; no JavaScript config layer.
- Matches the class names generated components already use, so nothing needs patching.
- Zero dependencies, MIT, actively maintained, and the direction shadcn/ui itself took.

### Cons

- A `package.json` entry for what is ultimately a stylesheet.
- Animation names and defaults are decided by the package; overriding means shadowing its CSS.
- Only meaningful while shadcn-svelte components are in use — it has no independent purpose here.

## 4. Build-vs-buy

Building this is real and cheap: the enter/exit primitives are a handful of `@keyframes` blocks (fade, zoom, slide on each axis) plus utility classes wiring them to `animate-in` and `animate-out`. Perhaps sixty to eighty lines of CSS in [layout.css](../../Frontend/src/routes/layout.css), an hour or two, no dependency. By this project's usual rule that says build.

**Buying wins on a narrow but sufficient margin.** The class names are not ours to choose — they are baked into the components `shadcn-svelte add` writes, and they change as upstream evolves. Hand-written keyframes would have to match that vocabulary exactly and be extended whenever a newly added component references an animation we had not implemented. The failure mode is quiet: a missing utility is not an error, just an element that snaps instead of sliding, noticed late if at all.

Against that, the package costs zero runtime bytes and one `@import`. There is very little to win by building, and a small ongoing chore to lose. Where a *custom* game animation is wanted, that is written directly in CSS as normal — this package covers only the component vocabulary, and does not constrain anything else.

## 5. Risk

### Undo risk — low

The lowest in the set. One `@import` line in [layout.css](../../Frontend/src/routes/layout.css) and one `package.json` entry. Removing it leaves the components fully functional and accessible — they simply lose their transitions, since unresolved utility classes are inert rather than erroneous.

### Security risk — low

MIT, zero dependencies, no install or postinstall scripts, no native binaries, no known CVEs. It contributes **no executable code to the client at all** — the output is CSS, so there is no runtime attack surface in the sense that applies to [bits-ui](./bits-ui.md) or [tailwind-merge](./tailwind-merge.md). The worst realistic outcome from a compromised release is malicious CSS, which is a defacement and data-exfiltration-via-selector concern rather than code execution, and would be visible in the built stylesheet. This is the same risk category as [Tailwind CSS](./tailwindcss.md) itself.
