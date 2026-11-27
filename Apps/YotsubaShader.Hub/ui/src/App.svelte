<script lang="ts">
  import Wizard from './lib/Wizard.svelte';
  import Forum from './lib/Forum.svelte';
  import Updates from './lib/Updates.svelte';
  import Home from './lib/Home.svelte';
  import ShaderLab from './lib/ShaderLab.svelte';
  import Plugins from './lib/Plugins.svelte';
  import Mcp from './lib/Mcp.svelte';
  import { onMount } from 'svelte';
  import { getState } from './lib/api';
  import { updates, refresh, dismiss, bannerVisible } from './lib/updateState.svelte';
  type View = 'home' | 'docs' | 'plugins' | 'mcp' | 'forum' | 'news' | 'install';

  const q = new URLSearchParams(location.search);
  const docsPort = q.get('docs');

  const items: { id: View; label: string }[] = [
    { id: 'home', label: 'Home' },
    { id: 'docs', label: 'Documentation' },
    { id: 'plugins', label: 'Plugins' },
    { id: 'mcp', label: 'MCP' },
    { id: 'forum', label: 'Forum' },
    { id: 'news', label: "What's new" },
    { id: 'install', label: 'Install' },
  ];

  let view = $state<View>('home');

  // Opening a shader from Home shows the Hub's own editor and WebGPU preview in place of the gallery.
  type Open = { id: string; title: string; style: string; label: string; code: string };
  let lab = $state<Open | null>(null);
  function go(v: View) { lab = null; view = v; }

  // The Hub checks GitHub on its own every few hours; the UI just re-reads that answer.
  onMount(() => {
    // First run: without the compiler nothing in the Hub can build a shader, so start at the installer.
    getState().then((s) => { if (!s.env.cliVersion && view === 'home' && !lab) view = 'install'; }).catch(() => {});
    refresh();
    const t = setInterval(refresh, 10 * 60 * 1000);
    return () => clearInterval(t);
  });
</script>

<div class="shell">
  <nav>
    <div class="brand"><img src="/icon.png" alt="" />Yotsuba Shader Hub</div>
    {#each items as it}
      <button class:active={view === it.id} onclick={() => go(it.id)}>
        {it.label}{#if it.id === 'news' && updates.status?.hubUpdateAvailable}<i class="dot" aria-label="Update available"></i>{/if}
      </button>
    {/each}
  </nav>

  <main>
    {#if bannerVisible() && view !== 'news' && view !== 'install'}
      <div class="banner">
        <span>Yotsuba Shader {updates.status?.latestVersion} is available.</span>
        <button onclick={() => (view = 'news')}>See what's new</button>
        <button class="x" aria-label="Dismiss" onclick={() => dismiss(updates.status!.latestVersion!)}>✕</button>
      </div>
    {/if}
    {#if view === 'docs'}
      <iframe title="Docs" src={`http://127.0.0.1:${docsPort}/`}></iframe>
    {:else if view === 'mcp'}
      <Mcp setup={() => (view = 'install')} />
    {:else if view === 'plugins'}
      <Plugins />
    {:else if view === 'news'}
      <Updates go={(v) => (view = v)} />
    {:else if view === 'forum'}
      <Forum />
    {:else if view === 'install'}
      <Wizard />
    {:else}
      {#if lab}
        {#key lab.id}<ShaderLab shader={lab} back={() => (lab = null)} />{/key}
      {:else}
        <Home open={(s) => (lab = s)} />
      {/if}
    {/if}
  </main>
</div>

<style>
  .shell { display: grid; grid-template-columns: 232px 1fr; grid-template-rows: minmax(0, 1fr); height: 100%; }
  nav {
    display: flex; flex-direction: column; gap: 2px; padding: 44px 12px 12px;
    background: var(--panel); backdrop-filter: blur(30px); border-right: 1px solid var(--line);
  }
  .brand { display: flex; align-items: center; gap: 10px; font-weight: 600; font-size: 15px; padding: 0 12px 18px; }
  .brand img { width: 30px; height: 30px; }
  nav button {
    text-align: left; padding: 8px 12px; border: 0; border-radius: 8px; background: none;
    font-size: 14px; cursor: pointer; transition: background .15s;
  }
  nav button:hover { background: color-mix(in srgb, var(--text) 7%, transparent); }
  nav button.active { background: var(--accent); color: #fff; }
  main { min-width: 0; min-height: 0; overflow: hidden; display: flex; flex-direction: column; }
  main > :global(*:not(.banner)) { flex: 1; min-height: 0; }
  iframe { width: 100%; border: 0; display: block; }
  .dot { display: inline-block; width: 7px; height: 7px; margin-left: 8px; border-radius: 50%; background: #ff453a; vertical-align: middle; }
  nav button.active .dot { background: #fff; }
  .banner { flex: none !important; display: flex; align-items: center; gap: 14px; padding: 9px 20px; font-size: 13.5px; background: var(--accent); color: #fff; }
  .banner button { border: 0; background: rgba(255, 255, 255, .22); color: #fff; padding: 4px 12px; border-radius: 99px; cursor: pointer; font-size: 12.5px; }
  .banner .x { margin-left: auto; background: none; padding: 4px 8px; }
</style>
