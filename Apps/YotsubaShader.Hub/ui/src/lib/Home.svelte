<script lang="ts">
  import { onMount } from 'svelte';

  type Shader = { id: string; style: string; label: string; title: string[]; description: string[]; tags: string[]; code: string };
  let { open }: { open: (s: { id: string; title: string; style: string; label: string; code: string }) => void } = $props();

  const styles = [
    { id: 'hlsl', label: 'C# HLSL Style' }, { id: 'glsl', label: 'C# GLSL Style' }, { id: 'msl', label: 'C# MSL Style' },
    { id: 'vulkan', label: 'C# SPIR-V Style' }, { id: 'wgsl', label: 'C# WGSL Style' },
  ];
  const es = false; // the Hub UI is English only
  const text = (a: string[]) => (es && a[1]) || a[0];

  let all = $state<Shader[]>([]);
  let style = $state(localStorage.getItem('home.style') ?? 'hlsl');
  let failed = $state(false);
  const shown = $derived(all.filter((s) => s.style === style));

  onMount(async () => {
    try { all = await (await fetch('/shaders/catalog.json')).json(); } catch { failed = true; }
  });
  const pick = (id: string) => { style = id; try { localStorage.setItem('home.style', id); } catch { /* not persisted */ } };
</script>

<div class="home">
  <header>
    <h1>{es ? 'Shaders de ejemplo' : 'Example shaders'}</h1>
    <p>{es ? 'Abre cualquiera, edítalo y mira el cambio en tiempo real.' : 'Open any of them, edit it and watch the change live.'}</p>
    <div class="styles" role="tablist">
      {#each styles as s}
        <button role="tab" aria-selected={style === s.id} class:on={style === s.id} onclick={() => pick(s.id)}>{s.label}</button>
      {/each}
    </div>
  </header>

  {#if failed}
    <p class="msg">{es ? 'No se pudo cargar el catálogo de shaders.' : 'The shader catalog could not be loaded.'}</p>
  {:else}
    <ul>
      {#each shown as s (s.id)}
        <li>
          <button class="card" onclick={() => open({ id: s.id, title: text(s.title), style: s.style, label: styles.find((x) => x.id === s.style)?.label ?? s.label, code: s.code })}>
            <span class="thumb">
              <img src={`/shaders/thumbs/${s.id}.webp`} alt="" loading="lazy" width="320" height="200"
                onerror={(e) => ((e.currentTarget as HTMLImageElement).style.visibility = 'hidden')} />
            </span>
            <span class="title">{text(s.title)}</span>
            <span class="desc">{text(s.description)}</span>
            <span class="tags">{#each s.tags.filter((t) => t !== 'playground').slice(0, 3) as t}<span>{t}</span>{/each}</span>
          </button>
        </li>
      {/each}
    </ul>
  {/if}
</div>

<style>
  .home { overflow: auto; padding: 56px 48px 48px; }
  header { max-width: 1200px; margin: 0 auto 28px; }
  h1 { font-size: 40px; letter-spacing: -.02em; margin: 0 0 6px; }
  header p { margin: 0 0 22px; color: var(--muted); font-size: 18px; }
  .styles { display: inline-flex; padding: 3px; border-radius: 10px; background: color-mix(in srgb, var(--text) 8%, transparent); }
  .styles button { padding: 6px 14px; white-space: nowrap; border: 0; border-radius: 8px; background: none; font: inherit; font-size: 13.5px; cursor: pointer; }
  .styles button.on { background: var(--bg); box-shadow: 0 1px 3px rgba(0, 0, 0, .3); }
  ul { list-style: none; margin: 0 auto; padding: 0; max-width: 1200px; display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 20px; }
  .card { display: grid; gap: 6px; width: 100%; text-align: left; padding: 0 0 14px; border: 1px solid var(--line); border-radius: 16px; background: var(--panel); color: inherit; font: inherit; cursor: pointer; overflow: hidden; transition: transform .18s, border-color .18s; align-content: start; }
  .card:hover { transform: translateY(-3px); border-color: var(--accent); }
  .card:focus-visible { outline: 2px solid var(--accent); }
  .thumb { display: block; aspect-ratio: 8 / 5; background: color-mix(in srgb, var(--text) 8%, transparent); }
  .thumb img { width: 100%; height: 100%; object-fit: cover; display: block; }
  .title, .desc, .tags { padding: 0 14px; }
  .title { margin-top: 6px; font-weight: 600; font-size: 15px; }
  .desc { font-size: 12.5px; color: var(--muted); line-height: 1.4; display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
  .tags { display: flex; gap: 6px; flex-wrap: wrap; margin-top: 4px; }
  .tags span { font-size: 11px; padding: 1px 8px; border-radius: 99px; background: color-mix(in srgb, var(--text) 8%, transparent); color: var(--muted); }
  .msg { color: var(--muted); }
</style>
