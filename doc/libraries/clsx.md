# clsx

- Status: adopted (**not removable — see section 3**)
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 2.1.1

## 1. Problem

Building a class-name string from a mix of static strings, conditionals and objects — `cn('btn', isActive && 'btn-active', { disabled })` — without emitting `false`, `undefined` or `null` into the DOM. This is the first half of the `cn()` helper that every [shadcn-svelte](./shadcn-svelte.md) component calls; the second half is [tailwind-merge](./tailwind-merge.md).

**It is not only our problem.** [Svelte](./svelte.md) itself has the same one — `class={{ active: true }}` and `class={[a, b]}` are supported syntax — and solves it with this same package.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **clsx** (chosen) | 2.1.1, 8 KB unpacked, ~240 B minified, **0 dependencies** | Handles strings, objects, arrays and nested arrays; falsy values dropped. Ships `ClassValue` types | Very active, Luke Edwards, ubiquitous | MIT | High: already in the tree via [Svelte](./svelte.md) and [bits-ui](./bits-ui.md), so using it costs nothing extra |
| Build in-house | 0 bytes *nominal*, **+57 bytes measured** | A recursive filter-and-join; genuinely ~15 lines | Us | n/a | **Low — measured counterproductive. See section 4** |
| Force our implementation on all consumers (bundler alias / lockfile override) | 0 bytes | Redirect every `clsx` import, including Svelte's, to our own module | Us | n/a | Low: the only route that actually removes the package, and it is the riskiest option on the table |
| `classnames` | 2.5.1 | The older React-era equivalent, same idea | Maintained | MIT | Low: strictly larger and slower than clsx with no compensating feature |
| Template literals / `.filter().join(' ')` | 0 bytes | No abstraction at all | n/a | n/a | Low: fine for one call site, and does not remove the package either |

Why the others lost: `classnames` is dominated by clsx on every axis. Inline joining and a hand-written helper both leave the package in the bundle regardless (section 4), so they add code rather than removing it. The override route is the only one that genuinely eliminates it, and is rejected on risk.

## 3. Decision & rationale

Adopt **clsx 2.1.1**. The earlier framing of this as a package "entailed by [shadcn-svelte](./shadcn-svelte.md)" was wrong and is corrected here: **clsx was already in this project's dependency tree before shadcn-svelte was installed, and cannot be removed by any change to our own code.**

Two packages depend on it independently of anything we wrote:

| Consumer | Declares | Imports it at | Reaches us via |
|---|---|---|---|
| [Svelte](./svelte.md) 5.56.8 | `clsx: ^2.1.1` (regular dependency) | `internal/shared/attributes.js`, `internal/client/dom/elements/class.js` | the framework itself |
| `svelte-toolbelt` 0.10.6 | `clsx: ^2.1.1` (regular dependency) | `dist/utils/merge-props.js` | [bits-ui](./bits-ui.md) |

The shadcn-svelte CLI promoted clsx to a *direct* `devDependency` so that `cn()` could import it by name, but it did not introduce it. What changed on 2026-08-03 was the declaration, not the dependency.

**Measured behaviour on this project.** clsx reaches the client bundle in two ways, and both are unavoidable. Svelte's class-attribute code path pulls it in whenever a component renders dynamic classes — in the Svelte runtime chunk, not in ours — and the app chrome's `cn()` import brings it in through our own module as well. Section 4 shows that removing our `cn()` import alone does not remove the package.

The consequence is that the usual build-vs-buy question does not apply in its usual form, and the honest answer is the opposite of what the package's size suggests. Section 4 sets out the measurement.

Its own footprint remains minimal. No components are vendored yet, but the `cn()` helper was written ahead of them: it lives at `Frontend/src/lib/utils/stylingUtils.ts`, imports clsx, and is already the single import site in our source — the app chrome uses it, so clsx ships via `cn()` as well as via Svelte's own class handling.

### Pros

- **Already present via [Svelte](./svelte.md) and [bits-ui](./bits-ui.md)**, so importing it in `cn()` adds zero bytes — the module is in the bundle either way.
- ~240 bytes minified; negligible next to [tailwind-merge](./tailwind-merge.md) in the same helper.
- Zero dependencies.
- Ships its own `ClassValue` type, which the helper's signature uses.
- Using the same implementation as Svelte's own class handling means one set of semantics, not two subtly different ones.
- MIT, extremely widely deployed, no known CVEs.

### Cons

- **Cannot be removed** by any change to our code — it is Svelte's dependency, not ours to drop.
- One more `package.json` entry for behaviour we do not control the presence of.
- Ships to the browser, unlike most of the frontend set.
- The direct declaration is arguably redundant with the transitive one, though relying on a transitive would be worse practice.

## 4. Build-vs-buy

**Buy — and unusually, building would make things worse rather than merely equal. This was measured, not assumed.**

The naive estimate still holds: recursing over arguments, keeping truthy strings, expanding object keys and joining is about fifteen minutes' work with no hidden complexity. Under this project's normal rule that says build.

It is wrong here, because **removing the import does not remove the package.** The experiment: `cn()` was rewritten with a hand-rolled `toClass` implementation and no `clsx` import, and the project rebuilt.

| Variant | Total client JS (raw) | Total client JS (gzip) |
|---|---|---|
| `cn()` importing clsx (as shipped) | 210,257 B | **66,691 B** |
| `cn()` hand-rolled, no clsx import | 210,477 B | **66,748 B** |

Hand-rolling produced a bundle **57 bytes larger**. clsx was still present in the client output, pulled in by Svelte's runtime and `svelte-toolbelt` exactly as before — so the result was two implementations of the same function shipping side by side instead of one. The saving is not small; it is negative.

**The only route that genuinely removes it** is forcing every consumer onto our implementation via a bundler alias or a lockfile override, so that Svelte and `svelte-toolbelt` resolve `clsx` to our module. That is rejected, and not merely on effort. Svelte deliberately wraps clsx rather than calling it directly, because Svelte's handling of falsy class values differs from clsx's — the wrapper carries a `TODO Svelte 6 revisit this` comment saying so. Substituting our own implementation means matching semantics that two upstream libraries depend on, that differ between them, and that are documented as due to change. The failure mode is a wrong `class` attribute somewhere in a dependency, which is silent, and the payoff is roughly 240 bytes.

The conclusion is not "this package is worth its cost". It is that **there is no cost to avoid**: it is in the bundle regardless, and the only question is whether we also ship a duplicate.

## 5. Risk

### Undo risk — low

Low, with an important qualification: low because *our* usage is one import in one file, not because the package is removable. Rewriting `cn()` to avoid it is a two-line change that compiles and runs — it simply makes the bundle marginally larger, as section 4 shows. Genuinely eliminating clsx would require overriding it for [Svelte](./svelte.md) and `svelte-toolbelt` as well, which is a different and considerably riskier undertaking.

### Security risk — low

MIT, zero dependencies, no install or postinstall scripts, no native binaries. Small enough to read end to end in a few minutes, which is a real mitigation rather than a formality. Extremely widely deployed, so a compromise would be caught quickly by the wider ecosystem. It does ship to the browser — see [shadcn-svelte](./shadcn-svelte.md) section 5 — but its surface is a pure string function with no I/O, no DOM access and no dynamic evaluation.

Worth noting for threat-modelling: because [Svelte](./svelte.md) depends on clsx directly, this package is part of the project's attack surface **whether or not shadcn-svelte is ever used**. It is not an exposure the component decision introduced, and dropping shadcn-svelte would not retire it.
