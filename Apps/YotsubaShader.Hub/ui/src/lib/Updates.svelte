<script lang="ts">
  import { updatesApi } from './api';
  import { ago, shortDate } from './format';
  import { onMount } from 'svelte';
  import { updates, checkNow, refresh } from './updateState.svelte';
  import Notes from './Notes.svelte';

  let { go }: { go: (view: 'install') => void } = $props();

  // The installed compiler can change in the Install tab, so re-read it every time this page opens.
  onMount(refresh);

  const s = $derived(updates.status);
  let error = $state<string | null>(null);
  let applying = $state(false);
  let downloading = $state(false);
  let saved = $state<string | null>(null);

  const latest = $derived(s?.releases.find((r) => r.version === s?.latestVersion));
  const open = (url: string) => updatesApi.open(url).catch((e) => (error = (e as Error).message));

  // Looks in the newest release for the installer of this system and architecture, saves it to Downloads and opens it.
  async function download() {
    downloading = true; error = null; saved = null;
    try { saved = (await updatesApi.download()).path; }
    catch (e) { error = (e as Error).message; }
    finally { downloading = false; }
  }

  async function apply() {
    applying = true; error = null;
    try { await updatesApi.apply(); } // on success the app restarts and this page goes away
    catch (e) { error = (e as Error).message; applying = false; }
  }
</script>

<div class="page">
  <header>
    <h1>What's new</h1>
    <div class="check">
      {#if s?.checkedAt}<span>Checked {ago(s.checkedAt)}</span>{/if}
      <button class="ghost" disabled={updates.checking} onclick={checkNow}>{updates.checking ? 'Checking…' : 'Check now'}</button>
    </div>
  </header>

  {#if s?.error}<p class="warn">{s.error} Showing the last known releases.</p>{/if}
  {#if updates.error || error}<p class="warn">{updates.error ?? error}</p>{/if}

  {#if s}
    <div class="cards">
      <section class="card">
        <span class="label">Hub</span>
        <strong>{s.hubVersion}</strong>
        {#if s.hubUpdateAvailable && latest}
          <span class="avail">{s.latestVersion} is available</span>
          <div class="row">
            {#if s.canSelfUpdate}
              <button class="primary" disabled={applying} onclick={apply}>{applying ? 'Updating…' : 'Update and restart'}</button>
            {:else}
              <button class="primary" disabled={downloading} onclick={download}>{downloading ? 'Downloading…' : `Download ${s.latestVersion}`}</button>
              <button class="ghost" onclick={() => open(latest.url)}>Release page</button>
            {/if}
          </div>
          {#if saved}<small class="saved">Saved to {saved} and opened. Close the Hub and run it to update.</small>{/if}
        {:else}<span class="ok">Up to date</span>{/if}
      </section>

      <section class="card">
        <span class="label">Compiler</span>
        <strong>{s.compilerVersion ?? 'Not installed'}</strong>
        {#if !s.compilerVersion}
          <span class="avail">Install it from Setup</span>
          <div class="row"><button class="primary" onclick={() => go('install')}>Open Setup</button></div>
        {:else if s.compilerUpdateAvailable}
          <span class="avail">{s.latestVersion} is available</span>
          <div class="row"><button class="primary" onclick={() => go('install')}>Update in Setup</button></div>
        {:else}<span class="ok">Up to date</span>{/if}
      </section>
    </div>

    <h2>Releases</h2>
    {#each s.releases as r (r.tag)}
      <article>
        <div class="head">
          <h3>{r.version}</h3>
          {#if r.version === s.latestVersion}<span class="badge">Latest</span>{/if}
          {#if r.version === s.hubVersion}<span class="badge muted">Installed</span>{/if}
          {#if r.prerelease}<span class="badge muted">Pre-release</span>{/if}
          <time>{shortDate(r.publishedAt)}</time>
          <button class="link" onclick={() => open(r.url)}>View on GitHub</button>
        </div>
        {#if r.notes.trim()}<Notes md={r.notes} />{:else}<p class="none">No release notes.</p>{/if}
      </article>
    {:else}
      <p class="none">No releases found yet.</p>
    {/each}
  {:else}
    <p class="none">Loading…</p>
  {/if}
</div>

<style>
  .page { height: 100%; overflow: auto; padding: 48px 56px; }
  header { display: flex; justify-content: space-between; align-items: baseline; max-width: 760px; }
  h1 { font-size: 34px; letter-spacing: -.02em; margin: 0 0 22px; }
  h2 { font-size: 20px; margin: 34px 0 6px; max-width: 760px; }
  .check { display: flex; gap: 12px; align-items: center; font-size: 12.5px; color: var(--muted); }
  .cards { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; max-width: 760px; }
  .card { display: grid; gap: 4px; padding: 18px 20px; border: 1px solid var(--line); border-radius: var(--radius); align-content: start; }
  .label { font-size: 12px; text-transform: uppercase; letter-spacing: .06em; color: var(--muted); }
  .card strong { font-size: 26px; letter-spacing: -.01em; }
  .ok { color: #30d158; font-size: 13.5px; } .avail { color: var(--accent); font-size: 13.5px; }
  .row { margin-top: 10px; }
  .warn { max-width: 760px; font-size: 14px; padding: 11px 14px; border-radius: 10px; background: color-mix(in srgb, #ff9f0a 14%, transparent); }
  article { max-width: 760px; padding: 20px 0; border-bottom: 1px solid var(--line); }
  .head { display: flex; gap: 10px; align-items: center; margin-bottom: 8px; }
  .head h3 { margin: 0; font-size: 18px; }
  .head time { font-size: 12.5px; color: var(--muted); margin-left: auto; }
  .badge { font-size: 11px; padding: 2px 9px; border-radius: 99px; background: var(--accent); color: #fff; }
  .badge.muted { background: color-mix(in srgb, var(--text) 10%, transparent); color: var(--text); }
  .link { background: none; border: 0; color: var(--accent); font-size: 12.5px; cursor: pointer; padding: 0; }
  .none { color: var(--muted); font-size: 14px; }
  .primary, .ghost { padding: 8px 18px; border-radius: 99px; border: 0; font-size: 13.5px; cursor: pointer; font: inherit; font-size: 13.5px; }
  .primary { background: var(--accent); color: #fff; } .ghost { background: color-mix(in srgb, var(--text) 8%, transparent); color: var(--text); }
  button:disabled { opacity: .4; cursor: default; }
  .saved { display: block; margin-top: 10px; font-size: 12.5px; color: var(--muted, #8e8e93); overflow-wrap: anywhere; }
</style>
