# tailwind-variants

- Status: adopted
- Date: 2026-08-03
- Decided by: project owner
- Version / commit pinned: 3.3.1

## 1. Problem

A component with `variant` and `size` props needs to map each combination to a set of utility classes, with defaults, and have TypeScript know which values are legal so `<Button variant="destrucive">` is a compile error rather than an unstyled button. Written by hand this is a nested lookup object plus a hand-maintained union type that must be kept in sync with it.

## 2. Alternatives considered

| Alternative | Version / size | Differentiating features | Maintenance health | License | Our fit |
|---|---|---|---|---|---|
| **tailwind-variants** (chosen) | 3.3.1, 324 KB unpacked, **0 dependencies** | `tv()` defines base + variants + compound variants + defaults; `VariantProps<typeof x>` *infers* the prop union from the definition, so types cannot drift from values. Slot support for multi-part components | Active; the Tailwind-native successor to CVA | MIT | High: entailed by [shadcn-svelte](./shadcn-svelte.md), and type inference is the part that is annoying to hand-roll |
| Build in-house | 0 bytes | A lookup object plus [clsx](./clsx.md); `keyof typeof` recovers most of the typing | Us | n/a | Medium: the second-most plausible build candidate here — see section 4 |
| `class-variance-authority` (CVA) | 0.7.x | The React-ecosystem original; same concept, no slots, less Tailwind-specific | Maintained | Apache-2.0 | Low: equivalent capability, but shadcn-svelte standardised on tailwind-variants, and CVA is Apache-2.0 against an otherwise MIT set |
| Plain conditional class strings | 0 bytes | No abstraction; `class={variant === 'x' ? '…' : '…'}` inline | n/a | n/a | Low: unreadable once a component has two variant axes and a compound rule |

Why the others lost: CVA is a genuine peer but is not reachable — the generated components call `tv()`, and swapping it means rewriting each one after every regeneration. Inline conditionals do not survive two variant dimensions. The hand-rolled lookup object is the real alternative and is weighed below.

## 3. Decision & rationale

Adopt **tailwind-variants 3.3.1**, entailed by the [shadcn-svelte](./shadcn-svelte.md) decision.

Of the three packages behind the shadcn styling idiom, this sits between the other two in how defensible it is standing alone. [clsx](./clsx.md) is trivially replaceable; [tailwind-merge](./tailwind-merge.md) is genuinely hard to replace. This one is *moderately* hard, and the difficulty is concentrated in one place: **type inference**. A hand-written lookup object gives the runtime behaviour easily, but keeping the `ButtonVariant` union in sync with the object's keys means either hand-maintaining a parallel type or writing the `keyof typeof` machinery to derive it. `VariantProps<typeof buttonVariants>` does that for free, and it is the reason `<Button variant="typo">` fails to compile today.

It also composes with the rest of the idiom rather than duplicating it: `tv()` handles variant selection, and conflict resolution is still delegated to [tailwind-merge](./tailwind-merge.md).

**Import sites grow one-per-component.** [clsx](./clsx.md) and [tailwind-merge](./tailwind-merge.md) are reached through the single `cn()` helper; `tv()` is instead called directly in the `module` block of each variant-bearing component. The vendored `button` at `src/lib/components/ui/button/` is the first such component and ships `tv()` to the browser; every component added after it adds another import site. That is why its undo risk is rated above [clsx](./clsx.md)'s.

### Pros

- Prop-type unions are *inferred* from the variant definition, so types and values cannot drift apart.
- Compound variants and defaults are declarative rather than nested conditionals.
- Zero dependencies; 324 KB unpacked, modest next to [tailwind-merge](./tailwind-merge.md).
- Slot support is available if multi-part components are needed later.
- MIT, consistent with the rest of the set.

### Cons

- Another browser-bound package for behaviour that is largely a lookup table.
- Import sites grow with each component added, unlike the single-site [clsx](./clsx.md) and [tailwind-merge](./tailwind-merge.md).
- A second abstraction layer over class strings, on top of `cn()` — a reader must know both.
- Its usefulness is mostly typing, which is a build-time benefit paid for with runtime bytes.

## 4. Build-vs-buy

The runtime half is genuinely small. A `variants` object keyed by prop name, a `defaultVariants` object, and a resolver that looks up each prop and passes the collected strings to `cn()` is perhaps thirty to fifty lines and covers everything the generated `button` component does. Compound variants add a filter step. On effort this is an afternoon, which by this project's rule of thumb says build.

**Two things push it the other way, and neither is effort.** The first is typing: getting `VariantProps`-equivalent inference — mapping an object literal's nested keys into a prop union that narrows correctly — is fiddly generic TypeScript, and getting it *slightly* wrong yields either `string` (no safety at all) or errors that are hard to read. That is the part worth not owning.

The second is the same regeneration friction that decided [clsx](./clsx.md), but stronger: every generated component with variants calls `tv()` in its `module` block. Replacing it means editing each component after every `shadcn-svelte update`, and unlike `cn()` — which lives in one file we control — these edits are spread across the whole `ui/` directory.

Buying wins, though less emphatically than for [tailwind-merge](./tailwind-merge.md). If this project ever drops shadcn-svelte but keeps the components, replacing `tv()` with a local resolver would be a reasonable cleanup.

## 5. Risk

### Undo risk — low

The vendored `button` calls `tv()`, but because that component is vendored source we own, replacement is a mechanical edit rather than a fight with a package boundary, and the resolver itself is of modest size. The rating scales with the number of variant-bearing components added — each one calls `tv()` in its `module` block.

### Security risk — low

MIT, zero dependencies, no install or postinstall scripts, no native binaries, no known CVEs. Surface is object lookup and string assembly — no I/O, no DOM access, no dynamic evaluation. Ships to the browser, per [shadcn-svelte](./shadcn-svelte.md) section 5.
