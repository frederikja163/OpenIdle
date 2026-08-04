---
name: prefer-scale-classes
description: Spend design-system tokens (gap-(--sp-4), h-(--h-topbar), rounded-sm, oi-body-md) before Tailwind's numeric scale, and the numeric scale before arbitrary brackets. Use whenever writing or reviewing class strings in .svelte files.
---

# Prefer scale classes over arbitrary values

Every value in a class string should come from the OpenIdle token layer
(`src/lib/styles/openidle/`) if the design system has a name for it. Brackets are the last
resort, not the shortcut.

## Order of preference

1. **A design-system token**, via Tailwind v4's CSS-variable shorthand `prop-(--token)`:
   `gap-(--sp-4)` `px-(--gutter-app)` `h-(--h-topbar)` `shadow-(--shadow-card)`
   `duration-(--dur-fast)`
2. **Tailwind's numeric scale** when no token carries that value — the base is `0.25rem`
   (4px) and fractional steps are supported: `py-1.5` `px-2.75` `size-5.5` `-top-1.5`
3. **An arbitrary bracket value** `h-[22px]` — only when neither of the above can express it,
   which for spacing and sizing is almost never.

```svelte
<!-- ✅ -->
<header class="flex h-(--h-topbar) items-center gap-(--sp-6) px-(--gutter-app)">
<a class="rounded-sm px-2.75 py-1.5 duration-(--dur-fast) ease-out">

<!-- ❌ -->
<header class="flex h-[52px] items-center gap-[16px] px-[14px]">
<a class="rounded-[5px] px-[11px] py-[6px] duration-[140ms]">
```

## The `--sp-*` indices are not Tailwind's numeric scale

The spacing ramp is deliberately non-linear, so the token's *number* and the utility's
*number* mean different things. Convert by pixel value, never by index — `--sp-6` is 16px
while `p-6` is 24px.

| token    | px  |     | token    | px  |     | token     | px  |
| -------- | --- | --- | -------- | --- | --- | --------- | --- |
| `--sp-0` | 0   |     | `--sp-5` | 12  |     | `--sp-9`  | 32  |
| `--sp-1` | 2   |     | `--sp-6` | 16  |     | `--sp-10` | 40  |
| `--sp-2` | 4   |     | `--sp-7` | 20  |     | `--sp-11` | 48  |
| `--sp-3` | 6   |     | `--sp-8` | 24  |     | `--sp-12` | 64  |
| `--sp-4` | 8   |     |          |     |     |           |     |

Where a value has a *structural* name, prefer that name over the raw step — it says why the
value is what it is, and it moves when the design system moves:
`--gutter-app` (app frame padding) · `--gutter-panel` (inside a panel) · `--gap-grid`
(between action/item cards) · `--gap-stack` · `--pad-card` · `--pad-control-y` /
`--pad-control-x`.

Fixed chrome likewise has named sizes rather than numbers: `--h-topbar` `--h-tabstrip`
`--w-skillrail` `--size-slot` `--size-action-card` `--h-meter` `--h-meter-lg`. Use
`h-(--h-topbar)`, not `h-13`.

## Other properties have their own token families

Spacing is only one of them. Reaching for a bracket in any of these is a mistake:

- **Colour** — every brand token is a first-class utility: `bg-surface-panel`
  `text-text-muted` `border-line-soft` `bg-verdant-600`. Never `bg-[#1a1f24]`, and never
  `bg-[var(--surface-panel)]` when the utility exists.
- **Type** — never express type as utilities. `layout.css` maps the composite `font:`
  shorthands onto component classes: `oi-display-xl/lg/md/sm` `oi-body-lg/md/sm`
  `oi-label-md/sm` `oi-num-lg/md/sm`. Write `class="oi-body-md"`, not
  `class="text-[13px] leading-[1.5]"`, and never hand-type UPPERCASE — `oi-label-*` applies
  it. Numbers always get an `oi-num-*` (or `.num`) class so digits stay tabular.
- **Radius** — `rounded-xs/sm/md/lg/xl` are remapped to the machined 3/5/8/12/16 scale.
  Panels `lg`, cards `md`, controls `sm`, chips and inner wells `xs`; only meters and status
  dots take `rounded-(--radius-full)`.
- **Depth** — `shadow-(--shadow-panel|--shadow-card|--shadow-card-hover|--shadow-pop)`,
  insets `shadow-(--inset-well|--inset-meter)`, glows `shadow-(--glow-accent|--glow-xp)`.
- **Motion** — `duration-(--dur-instant|--dur-fast|--dur-base|--dur-slow)`. `ease-out`,
  `ease-in-out` and `ease-linear` already resolve to the design system's curves, so use them
  bare; `ease-(--ease-snap)` for the collect/level-up pop only.

## When applying

- **Writing new styles** → look for a token first, then the numeric scale, then brackets.
  If you find yourself typing a px value you have seen elsewhere in the codebase, it is
  almost certainly a token already.
- **Editing or reviewing** → convert any bracket that has a token or clean scale equivalent.
  Leave genuinely one-off values alone (a `1px` hairline reads better as `[1px]` than as
  `0.25`), and don't churn a working numeric class into a token that means something else.
- **A value the design system has no name for** is worth a second look: if it's a real part
  of the visual language, add the token upstream (see the sync note in
  `src/lib/styles/openidle/index.css`) rather than hard-coding it in markup.
