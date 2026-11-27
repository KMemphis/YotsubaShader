<script lang="ts">
  import { parseBody } from './format';
  let { text }: { text: string } = $props();
  const blocks = $derived(parseBody(text));
</script>

{#each blocks as b}
  {#if b.kind === 'code'}
    <pre><code>{b.text}</code></pre>
  {:else}
    {#each b.paragraphs as p}
      <p class:quote={p.quote}>{#each p.spans as s}{#if s.code}<code>{s.text}</code>{:else if s.bold}<b>{s.text}</b>{:else if s.italic}<i>{s.text}</i>{:else if s.strike}<s>{s.text}</s>{:else if s.underline}<u>{s.text}</u>{:else}{s.text}{/if}{/each}</p>
    {/each}
  {/if}
{/each}

<style>
  p { margin: 0 0 10px; line-height: 1.55; font-size: 14.5px; white-space: pre-wrap; }
  p.quote { padding-left: 12px; border-left: 3px solid var(--line); color: var(--muted); }
  p:last-child { margin-bottom: 0; }
  code { font: 12.5px ui-monospace, 'SF Mono', Menlo, monospace; }
  p code { padding: 1px 6px; border-radius: 6px; background: color-mix(in srgb, var(--text) 9%, transparent); }
  pre { margin: 0 0 10px; padding: 12px 14px; border-radius: 10px; overflow: auto; background: color-mix(in srgb, var(--text) 6%, transparent); line-height: 1.5; }
</style>
