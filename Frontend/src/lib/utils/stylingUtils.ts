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
