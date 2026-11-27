<script lang="ts">
  import { parseNotes, type Span } from './format';
  let { md }: { md: string } = $props();
  const notes = $derived(parseNotes(md));
</script>

{#snippet inline(ss: Span[])}{#each ss as s}{#if s.kind === 'code'}<code>{s.text}</code>{:else if s.kind === 'bold'}<strong>{s.text}</strong>{:else}{s.text}{/if}{/each}{/snippet}

{#each notes as n}
  {#if n.kind === 'heading'}<h4>{@render inline(n.spans)}</h4>
  {:else if n.kind === 'list'}<ul>{#each n.items as it}<li>{@render inline(it)}</li>{/each}</ul>
  {:else if n.kind === 'code'}<pre><code>{n.text}</code></pre>
  {:else}<p>{@render inline(n.spans)}</p>{/if}
{/each}

<style>
  h4 { margin: 14px 0 6px; font-size: 13px; letter-spacing: .02em; text-transform: uppercase; color: var(--muted); }
  p, li { font-size: 14.5px; line-height: 1.55; margin: 0 0 8px; }
  ul { margin: 0 0 8px; padding-left: 20px; }
  code { font: 12.5px ui-monospace, 'SF Mono', Menlo, monospace; padding: 1px 6px; border-radius: 6px; background: color-mix(in srgb, var(--text) 9%, transparent); }
  pre { margin: 0 0 10px; padding: 12px 14px; border-radius: 10px; overflow: auto; background: color-mix(in srgb, var(--text) 6%, transparent); }
  pre code { padding: 0; background: none; }
</style>
