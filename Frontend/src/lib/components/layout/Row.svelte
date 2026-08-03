<script lang="ts">
    import type {SvelteHTMLElements} from "svelte/elements";

    let {children, ...rest}: SvelteHTMLElements["div"] = $props();
</script>

<div {...rest} class={`flex flex-row ${rest.class}`}>
    {@render children?.()}
</div>