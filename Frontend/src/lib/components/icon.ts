import type { Component } from 'svelte';

/**
 * Every glyph in the app is a Lucide icon component, and the only prop any of
 * them is ever handed is `size`. Declared once here because a component that
 * spells the shape inline accepts a subtly different type from its neighbours,
 * and widening the contract later would mean finding every copy.
 */
export type IconComponent = Component<{ size?: number | string }>;
