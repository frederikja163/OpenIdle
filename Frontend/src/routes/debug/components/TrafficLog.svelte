<script lang="ts">
	import Column from '$lib/components/layout/Column.svelte';
	import Row from '$lib/components/layout/Row.svelte';
	import { Badge, badgeVariants, type BadgeVariant } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import {
		FILTERABLE_KINDS,
		isVisible,
		preferences,
		toggleKind
	} from '$lib/debug/preferences.svelte';
	import {
		clearTraffic,
		partnerOf,
		trafficState,
		type Frame,
		type FrameKind
	} from '$lib/debug/traffic.svelte';
	import { cn } from '$lib/utils/stylingUtils';

	/*
	 * Every frame the socket carried, newest first — requests, responses, errors
	 * and server-push events in one list rather than split by kind, because their
	 * order relative to each other is the interesting part. ProfilesChangedEvent,
	 * for instance, arrives *before* the response to the request that caused it.
	 *
	 * That ordering is also why a request and its answer are linked by highlight
	 * rather than by being drawn together: on a busy socket the two halves are far
	 * apart, and moving them next to each other would cost the very ordering the
	 * log is kept for.
	 */

	// One map feeds both the row chip and the legend, so a kind's identity (what
	// it is called, how it is coloured) cannot drift between the two.
	const KIND: Record<FrameKind, { label: string; variant: BadgeVariant; edge: string }> = {
		request: { label: 'request', variant: 'accent', edge: '' },
		response: { label: 'response', variant: 'neutral', edge: '' },
		event: { label: 'event', variant: 'info', edge: 'border-l-(--azure-500)' },
		unknown: { label: 'unknown', variant: 'rare', edge: 'border-l-(--amethyst-500)' }
	};

	const visible = $derived(trafficState.frames.filter((frame) => isVisible(frame.kind)));

	let expanded = $state<number | null>(null);
	/** The requestId under the pointer, and the one clicked to hold it there. */
	let hoveredId = $state<number | null>(null);
	let pinnedId = $state<number | null>(null);
	// Pinning wins, so the highlight survives scrolling away to find the other
	// half — which is exactly when the pointer has to leave the row.
	const linkedId = $derived(pinnedId ?? hoveredId);

	function summary(frame: Frame): string {
		if (frame.type) {
			return frame.type;
		}
		// Nothing this side could name it — worth showing rather than hiding.
		return frame.raw.slice(0, 60);
	}

	function isLinked(frame: Frame): boolean {
		return frame.requestId !== null && frame.requestId === linkedId;
	}

	function jumpTo(frame: Frame): void {
		document.getElementById(`frame-${frame.id}`)?.scrollIntoView({ block: 'nearest' });
	}
</script>

<Column class="min-h-0 gap-(--sp-5)">
	<Row class="items-center gap-(--sp-5)">
		<span class="oi-label-md text-text-strong">Traffic</span>
		<span class="oi-body-sm text-text-faint">
			{visible.length === trafficState.frames.length
				? visible.length
				: `${visible.length} of ${trafficState.frames.length}`}
		</span>
		<Button
			size="sm"
			variant="ghost"
			onclick={() => (trafficState.paused = !trafficState.paused)}
			aria-pressed={trafficState.paused}
		>
			{trafficState.paused ? 'resume' : 'pause'}
		</Button>
		<Button size="sm" variant="ghost" onclick={clearTraffic}>clear</Button>

		<!-- The legend *is* the control: each badge filters its own kind, saved
		     per browser so the choice survives a reload. -->
		<Row class="items-center gap-(--sp-2)">
			{#each FILTERABLE_KINDS as kind (kind)}
				{@const shown = preferences.visibleKinds.includes(kind)}
				<button
					type="button"
					class={cn(
						badgeVariants({ variant: KIND[kind].variant }),
						'cursor-pointer transition-opacity duration-(--dur-fast) ease-out',
						!shown && 'opacity-42'
					)}
					aria-pressed={shown}
					title={shown ? `Hide ${KIND[kind].label} frames` : `Show ${KIND[kind].label} frames`}
					onclick={() => toggleKind(kind)}>{KIND[kind].label}</button
				>
			{/each}
		</Row>
	</Row>

	<!-- One grid for the header and every card, with the cards subgridding its
	     columns: a max-content track then fits the widest value across the whole
	     log, so a column lines up under its label whichever kinds of frame are on
	     screen — and a cell that has no value stays empty rather than letting the
	     next value slide left under the wrong heading. The message track has a
	     floor because it is the one column the log cannot do without: on a panel
	     too narrow for the rest, the log scrolls sideways rather than losing it. -->
	<div
		class="oi-scroll grid min-h-0 grow grid-cols-[max-content_max-content_minmax(10rem,1fr)_repeat(6,max-content)] content-start gap-x-(--sp-5) gap-y-(--sp-3) overflow-auto"
	>
		<!-- The transparent side border and padding mirror a card's, so the header's
		     tracks start where the cards' do. -->
		<div
			class="sticky top-0 z-10 col-span-full grid grid-cols-subgrid items-end border-l-2 border-transparent bg-surface-panel px-(--sp-5) py-(--sp-2)"
		>
			<span class="oi-label-sm text-text-faint">kind</span>
			<span class="oi-label-sm text-text-faint">time</span>
			<span class="oi-label-sm min-w-0 truncate text-text-faint">message</span>
			<span class="oi-label-sm text-right text-text-faint" title="Response minus request">
				round trip
			</span>
			<span
				class="oi-label-sm text-right text-text-faint"
				title="Arrival minus the backend's send stamp; clock skew included"
			>
				travel
			</span>
			<span
				class="oi-label-sm text-right text-text-faint"
				title="The backend's per-socket event sequence"
			>
				event id
			</span>
			<span
				class="oi-label-sm text-right text-text-faint"
				title="Correlates a response to its request"
			>
				request id
			</span>
			<span></span>
			<span class="oi-label-sm text-right text-text-faint">bytes</span>
		</div>

		{#each visible as frame (frame.id)}
			{@const partner = partnerOf(frame)}
			<!-- A role, because the hover handlers make an otherwise static element
			     interactive in the a11y checker's eyes; group is what it is. -->
			<div
				id="frame-{frame.id}"
				role="group"
				class={cn(
					'col-span-full grid grid-cols-subgrid rounded-xs border border-l-2',
					isLinked(frame)
						? 'border-line-accent bg-surface-active'
						: 'border-line-soft bg-surface-card hover:bg-surface-card-hover',
					// Side colour last: twMerge drops an earlier border-l-* colour
					// when a later all-sides border-* colour follows.
					frame.error !== null ? 'border-l-(--crimson-500)' : KIND[frame.kind].edge
				)}
				onmouseenter={() => (hoveredId = frame.requestId)}
				onmouseleave={() => (hoveredId = null)}
				onfocusin={() => (hoveredId = frame.requestId)}
				onfocusout={() => (hoveredId = null)}
			>
				<div class="col-span-full grid grid-cols-subgrid items-center px-(--sp-5) py-(--sp-4)">
					<button
						type="button"
						class="col-span-3 grid min-w-0 cursor-pointer grid-cols-subgrid items-center text-left duration-(--dur-fast) ease-out"
						onclick={() => (expanded = expanded === frame.id ? null : frame.id)}
					>
						<Badge variant={frame.error !== null ? 'danger' : KIND[frame.kind].variant}>
							{KIND[frame.kind].label}
						</Badge>
						<span class="oi-num-sm text-text-faint">{frame.time}</span>
						<Row class="min-w-0 items-center gap-(--sp-5)">
							<span class="oi-body-sm min-w-0 truncate text-text-body">{summary(frame)}</span>
							{#if frame.error !== null}
								<!-- On the line rather than inside the payload: what went wrong is
								     the reason the reader opened the log. -->
								<span class="oi-body-sm min-w-0 grow truncate text-text-danger" title={frame.error}>
									{frame.error}
								</span>
							{/if}
						</Row>
					</button>
					<span class="oi-num-sm text-right text-text-faint">
						{#if frame.elapsedMs !== null}{frame.elapsedMs}ms{/if}
					</span>
					<span class="oi-num-sm text-right text-text-faint">
						{#if frame.travelMs !== null}{frame.travelMs}ms{/if}
					</span>
					<span class="oi-num-sm text-right text-text-faint">
						{#if frame.eventId !== null}#{frame.eventId}{/if}
					</span>
					<span class="justify-self-end">
						{#if frame.requestId !== null}
							<button
								type="button"
								class="oi-num-sm cursor-pointer rounded-xs px-(--sp-2) py-(--sp-1) duration-(--dur-fast) ease-out hover:bg-action-quiet-hover {pinnedId ===
								frame.requestId
									? 'text-text-accent'
									: 'text-text-faint'}"
								aria-pressed={pinnedId === frame.requestId}
								title="Hold the highlight on requestId {frame.requestId}"
								onclick={() => (pinnedId = pinnedId === frame.requestId ? null : frame.requestId)}
							>
								#{frame.requestId}
							</button>
						{/if}
					</span>
					<span class="justify-self-end">
						{#if partner && isVisible(partner.kind)}
							<button
								type="button"
								class="oi-num-sm cursor-pointer rounded-xs px-(--sp-2) py-(--sp-1) text-text-muted duration-(--dur-fast) ease-out hover:bg-action-quiet-hover hover:text-text-body"
								aria-label={frame.direction === 'in'
									? 'Jump to the request'
									: 'Jump to the response'}
								onclick={() => jumpTo(partner)}
							>
								↕
							</button>
						{/if}
					</span>
					<span class="oi-num-sm text-right text-text-faint">{frame.bytes}B</span>
				</div>
				{#if expanded === frame.id}
					<pre
						class="oi-scroll oi-body-sm col-span-full max-h-64 overflow-auto border-t border-line-soft p-(--sp-5) font-mono text-text-body">{frame.pretty}</pre>
				{/if}
			</div>
		{:else}
			{#if trafficState.frames.length === 0}
				<span class="oi-body-sm col-span-full text-text-faint"
					>Nothing has crossed the socket yet.</span
				>
			{:else}
				<span class="oi-body-sm col-span-full text-text-faint"
					>Every frame is hidden by the filter.</span
				>
			{/if}
		{/each}
	</div>
</Column>
