---
name: svelte-component-conventions
description: Use when writing or reviewing Svelte components in Frontend/src/lib/components or Frontend/src/routes. Enforces two project conventions: (1) prefer the Row and Column layout components over pure span or div for flexbox layout, and (2) type component props with an `interface Props` block and destructure via `let { ... }: Props = $props()` (Svelte 5 runes), with `children?: Snippet` for slot content.
---

# Svelte Component Conventions

Two rules every Svelte component in this project must follow: prefer `Row`/`Column` over raw `span`/`div`, and type props with an `interface Props` + Svelte 5 runes pattern.

## Rule 1: Prefer Row and Column over pure span or div

For any horizontal or vertical flexbox grouping, use the layout components instead of hand-rolling `class="flex ..."` on a `<div>` or `<span>`.

- `Row` renders `flex flex-row` — `$lib/components/layout/Row.svelte`
- `Column` renders `flex flex-col` — `$lib/components/layout/Column.svelte`
- Both accept standard div attributes plus `class`, merged via `cn`, so spacing/alignment tokens just work.

```svelte
<script lang="ts">
	import Row from '$lib/components/layout/Row.svelte';
	import Column from '$lib/components/layout/Column.svelte';
</script>

<Column class="gap-(--sp-5)">
	<Row class="items-center gap-(--sp-3)">
		<span class="oi-label-sm">Label</span>
		<span class="text-text-body">Value</span>
	</Row>
</Column>
```

Fall back to a raw element only when the single flex wrapper does not fit:

- Grid layouts (`grid`, `grid-cols-*`)
- Semantic elements (`header`, `nav`, `main`, `section`, `article`, `form`, `ul`/`li`)
- Inline text spans that must not become a flex container
- Non-flex layouts (block flow, absolute positioning, table)

## Rule 2: Structure props with `interface Props` + runes destructure

Every component declares its props as an `interface Props` above the component and destructures them through `$props()` in one `let` statement. Never inline-untype props or repeat the destructure across the script.

`children` must be typed as `Snippet` (imported from `'svelte'`) and rendered with `{@render children?.()}`.

```ts
import type { Snippet } from 'svelte';

interface Props {
	variant: 'edit' | 'create';
	onSubmit?: () => void;
	onCancel?: () => void;
	open?: boolean;
	booking: IBooking;
	children?: Snippet;
}

let { variant, onSubmit, onCancel, open, booking, children }: Props = $props();
```

Then render slot content where it belongs:

```svelte
{@render children?.()}
```

Notes:

- Defaults (e.g. `open = false`) go inline in the destructure: `let { open = false, ... }: Props = $props()`.
- For `$bindable` props (rare), add the `$bindable()` marker in the destructure as well.
- `IBooking` in the example is a domain type — import the real interface where it is defined rather than redeclaring it.
- Indentation follows the repo prettier config (tabs), not the spacing in the illustrative example above.

## Verification checklist

- No `<div class="flex ...">` or `<span class="flex ...">` used where `Row`/`Column` fits.
- Every component's props declared as `interface Props` and destructured via a single `let { ... }: Props = $props();`.
- `children` typed as `Snippet` and rendered with `{@render children?.()}`.
