import Root, {
	type ButtonProps,
	type ButtonSize,
	type ButtonVariant,
	buttonVariants
} from './button.svelte';

export {
	Root,
	Root as Button,
	buttonVariants,
	type ButtonProps,
	// The generator's name is `ButtonProps`; `Props` is the shorter alias the
	// rest of the vendored components use for their own props type.
	type ButtonProps as Props,
	type ButtonSize,
	type ButtonVariant
};
