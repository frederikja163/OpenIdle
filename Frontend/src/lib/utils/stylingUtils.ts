import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * Join class names and resolve conflicting Tailwind utilities, last one winning.
 *
 * Plain interpolation cannot do this: `class="p-4 p-8"` leaves both utilities in
 * the attribute, and which applies is decided by their order in the generated
 * stylesheet rather than by the caller. Falsy values are dropped, so an absent
 * `class` prop does not render as "undefined".
 */
export function cn(...inputs: ClassValue[]) {
	return twMerge(clsx(inputs));
}

/**
 * Add a bindable `ref` to a component's props so it can expose its underlying
 * DOM node.
 *
 * Mirrors the `WithElementRef` from shadcn-svelte's own `utils` registry item.
 * Every vendored component imports it from this module by the `utils` alias in
 * components.json, so the name and shape are fixed by the generator rather than
 * chosen here. The CLI skips this file because it already exists, which is why
 * the type is maintained by hand.
 */
export type WithElementRef<T, U extends HTMLElement = HTMLElement> = T & { ref?: U | null };
