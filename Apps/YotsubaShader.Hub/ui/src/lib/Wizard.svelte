<script lang="ts">
  import { onMount } from 'svelte';
  import { refresh as refreshUpdates } from './updateState.svelte';
  import { getState, getLicense, startInstall, watchInstall, type Component, type Env, type InstallEvent } from './api';

  const steps = ['Welcome', 'License', 'Components', 'Location', 'Summary', 'Installing', 'Finish'] as const;

  let step = $state(0);
  let components = $state<Component[]>([]);
  let env = $state<Env | null>(null);
  let license = $state('');
  let accepted = $state(false);
  let picked = $state<Record<string, boolean>>({});
  let home = $state('');
  let error = $state<string | null>(null);

  let status = $state<Record<string, 'running' | 'ok' | 'fail'>>({});
  type Line = { step: string | null; text: string; key?: string; pct?: number; detail?: string };
  let log = $state<Line[]>([]);
  let result = $state<'ok' | 'failed' | null>(null);
  let logEl: HTMLElement | undefined = $state();

  onMount(async () => {
    const s = await getState();
    components = s.components;
    env = s.env;
    home = s.env.defaultHome;
    picked = Object.fromEntries(s.components.map((c) => [c.id, c.locked || c.default]));
    license = await getLicense();
  });

  const groups = $derived.by(() => {
    const map = new Map<string, Component[]>();
    for (const c of components) {
      const g = c.group ?? '';
      map.set(g, [...(map.get(g) ?? []), c]);
    }
    return [...map];
  });
  const chosen = $derived(components.filter((c) => picked[c.id]));
  const nameOf = (id: string | null) => components.find((c) => c.id === id)?.name ?? '';

  function groupState(items: Component[]): 'all' | 'some' | 'none' {
    const n = items.filter((c) => picked[c.id]).length;
    return n === items.length ? 'all' : n === 0 ? 'none' : 'some';
  }
  function toggleGroup(items: Component[]) {
    const on = groupState(items) !== 'all';
    for (const c of items) if (!c.locked) picked[c.id] = on;
  }

  const canNext = $derived(
    step === 1 ? accepted : step === 3 ? home.trim().length > 0 : step === 5 ? result !== null : true,
  );

  function onEvent(e: InstallEvent) {
    if (e.type === 'step-start' && e.step) status[e.step] = 'running';
    else if (e.type === 'step-done' && e.step) status[e.step] = 'ok';
    else if (e.type === 'step-fail') {
      if (e.step) status[e.step] = 'fail';
      if (e.message) log.push({ step: e.step, text: e.message });
    } else if (e.type === 'log' && e.message) addLog(e.step, e.message);
    else if (e.type === 'done') {
      result = e.message === 'ok' ? 'ok' : 'failed';
      refreshUpdates(); // the compiler version shown in What's new and the banner
      step = 6;
    }
    queueMicrotask(() => logEl?.scrollTo({ top: logEl.scrollHeight }));
  }

  // The CLI prints a line every 10% of every download. Fold them into one updating row per file.
  function addLog(stepId: string | null, text: string) {
    const m = text.match(/^\s+(.+?):\s+(\d+) % \((.*)\)$/);
    if (!m) { log.push({ step: stepId, text }); return; }
    const [, key, pct, detail] = m;
    const line: Line = { step: stepId, text: key, key, pct: +pct, detail };
    const at = log.findIndex((l) => l.key === key);
    if (at >= 0) { log[at] = line; return; }
    const header = log.findIndex((l) => l.text.startsWith(`Descargando ${key}`));
    if (header >= 0) log.splice(header, 1);
    log.push(line);
  }

  async function install() {
    status = {}; log = []; result = null; error = null;
    const err = await startInstall(chosen.map((c) => c.id), home.trim());
    if (err) { error = err; return; }
    step = 5;
    watchInstall(onEvent);
  }

  function next() { if (step === 4) install(); else step += 1; }
</script>

{#snippet line(l: Line)}
  {#if l.pct !== undefined}
    <div class="dl"><span class="dn">{l.key}</span><span class="bar"><i style:width="{l.pct}%"></i></span><span class="dd">{l.pct === 100 ? l.detail?.replace(/^.* en /, 'in ') : l.detail}</span></div>
  {:else}<div>{l.text}</div>{/if}
{/snippet}

<div class="wizard">
  <aside>
    <h2>Setup</h2>
    <ol>
      {#each steps as s, i}
        <li class:done={i < step} class:current={i === step}><span class="dot">{i < step ? '✓' : i + 1}</span>{s}</li>
      {/each}
    </ol>
  </aside>

  <section>
    <div class="body">
      {#key step}
        <div class="page">
          {#if step === 0}
            <h1>Welcome to Yotsuba Shader Hub</h1>
            <p class="lead">This sets up the compiler and the engine plugins you choose. You can run it again at any time to add or update components.</p>
            {#if env}
              <ul class="checks">
                <li class:bad={!env.dotnetPath}>{env.dotnetPath ? `.NET SDK ${env.sdks.at(-1) ?? ''}` : '.NET SDK not found'}</li>
                <li class:todo={!env.cliVersion}>{env.cliVersion ? `Compiler ${env.cliVersion} already installed` : 'Compiler not installed yet'}</li>
              </ul>
              {#if !env.dotnetPath}
                <p class="warn">Install the .NET SDK from dotnet.microsoft.com/download, then reopen this setup. The compiler is a .NET tool and cannot be installed without it.</p>
              {/if}
            {/if}
          {:else if step === 1}
            <h1>License</h1>
            <pre class="license">{license}</pre>
            <label class="check"><input type="checkbox" bind:checked={accepted} /> I accept the terms of the license</label>
          {:else if step === 2}
            <h1>Choose components</h1>
            <div class="tree">
              {#each groups as [g, items]}
                {#if g}
                  <label class="row group">
                    <input type="checkbox" checked={groupState(items) === 'all'} indeterminate={groupState(items) === 'some'}
                      disabled={items.every((c) => c.locked)} onchange={() => toggleGroup(items)} />
                    <span>{g}</span>
                  </label>
                {/if}
                {#each items as c}
                  <label class="row" class:child={!!g} title={c.description}>
                    <input type="checkbox" bind:checked={picked[c.id]} disabled={c.locked} />
                    <span class="name">{c.name}</span>
                    {#if c.locked}<span class="tag">Required</span>{/if}
                    <span class="desc">{c.description}</span>
                  </label>
                {/each}
              {/each}
            </div>
          {:else if step === 3}
            <h1>Plugin location</h1>
            <p class="lead">Engine plugins are stored here. The compiler itself is installed as a global .NET tool.</p>
            <input class="path" bind:value={home} spellcheck="false" />
            {#if env && home.trim() !== env.defaultHome}
              <p class="warn">The compiler only looks in <code>{env.defaultHome}</code> unless the <code>YOTSUBA_HOME</code> environment variable points to this folder. You will need to set it yourself.</p>
            {/if}
          {:else if step === 4}
            <h1>Ready to install</h1>
            <ul class="summary">
              {#each chosen.filter((c) => c.kind !== 'Bundled') as c}<li>{c.name}</li>{/each}
            </ul>
            <p class="lead">Plugins are downloaded from NuGet, so this needs an internet connection.</p>
            {#if error}<p class="warn">{error}</p>{/if}
          {:else if step === 5}
            <h1>Installing</h1>
            <ul class="progress">
              {#each components.filter((c) => status[c.id]) as c}
                <li class={status[c.id]}><span class="ico">{status[c.id] === 'ok' ? '✓' : status[c.id] === 'fail' ? '✕' : ''}{#if status[c.id] === 'running'}<i class="spin"></i>{/if}</span>{c.name}</li>
              {/each}
            </ul>
            <div class="log" bind:this={logEl}>
              {#each log as l}{@render line(l)}{/each}
            </div>
          {:else}
            {#if result === 'ok'}
              <h1>All set</h1>
              <p class="lead">Open a terminal and run <code>dotnet yotsuba --help</code>. The documentation and playground are in the sidebar.</p>
              {#if env && home.trim() !== env.defaultHome}
                <p class="warn">Remember to set <code>YOTSUBA_HOME={home}</code>.</p>
              {/if}
            {:else}
              <h1>Installation failed</h1>
              <p class="lead">The step that failed is marked in the log. Fix the cause and try again.</p>
              <div class="log">{#each log.slice(-12) as l}{@render line(l)}{/each}</div>
            {/if}
          {/if}
        </div>
      {/key}
    </div>

    <footer>
      <button class="ghost" disabled={step === 0 || step === 5 || (step === 6 && result === 'ok')} onclick={() => (step = step === 6 ? 4 : step - 1)}>Back</button>
      {#if step === 6}
        <button class="primary" onclick={() => (step = 0)}>{result === 'ok' ? 'Done' : 'Start over'}</button>
      {:else}
        <button class="primary" disabled={!canNext || step === 5 || (step === 0 && !env?.dotnetPath)} onclick={next}>{step === 4 ? 'Install' : 'Continue'}</button>
      {/if}
    </footer>
  </section>
</div>

<style>
  .wizard { display: grid; grid-template-columns: 220px 1fr; height: 100%; }
  aside { padding: 44px 20px; border-right: 1px solid var(--line); }
  aside h2 { margin: 0 0 24px; font-size: 13px; letter-spacing: .06em; text-transform: uppercase; color: var(--muted); font-weight: 600; }
  ol { list-style: none; margin: 0; padding: 0; display: grid; gap: 14px; }
  li { display: flex; align-items: center; gap: 10px; font-size: 14px; color: var(--muted); }
  li.current { color: var(--text); font-weight: 600; }
  .dot { width: 22px; height: 22px; border-radius: 50%; display: grid; place-items: center; font-size: 11px; border: 1px solid var(--line); }
  li.current .dot { background: var(--accent); border-color: var(--accent); color: #fff; }
  li.done .dot { background: color-mix(in srgb, var(--accent) 16%, transparent); border-color: transparent; color: var(--accent); }
  section { display: flex; flex-direction: column; min-height: 0; }
  .body { flex: 1; overflow: auto; padding: 56px 56px 24px; }
  .page { max-width: 640px; animation: in .35s cubic-bezier(.2, .8, .2, 1); }
  @keyframes in { from { opacity: 0; transform: translateY(10px); } }
  h1 { font-size: 34px; letter-spacing: -.02em; margin: 0 0 14px; }
  .lead { font-size: 17px; color: var(--muted); line-height: 1.5; margin: 0 0 20px; }
  .warn { font-size: 14px; line-height: 1.45; padding: 12px 14px; border-radius: 10px; background: color-mix(in srgb, #ff9f0a 14%, transparent); }
  code { font: 13px ui-monospace, 'SF Mono', Menlo, monospace; }
  .checks { list-style: none; padding: 0; display: grid; gap: 8px; margin: 0 0 16px; }
  .checks li { color: var(--text); font-size: 15px; }
  .checks li::before { content: '✓'; color: #30d158; margin-right: 10px; }
  .checks li.todo::before { content: '•'; color: #ff9f0a; }
  .checks li.bad::before { content: '✕'; color: #ff453a; }
  .license { height: 300px; overflow: auto; padding: 14px; border: 1px solid var(--line); border-radius: var(--radius); font: 12px/1.5 ui-monospace, Menlo, monospace; white-space: pre-wrap; margin: 0 0 14px; }
  .check { display: flex; gap: 10px; font-size: 14px; align-items: center; }
  .tree { border: 1px solid var(--line); border-radius: var(--radius); overflow: hidden; }
  .row { display: grid; grid-template-columns: 20px auto 1fr; grid-template-areas: 'box name tag' '. desc desc'; gap: 2px 10px; align-items: center; padding: 11px 14px; border-bottom: 1px solid var(--line); font-size: 14px; cursor: pointer; }
  .row:last-child { border-bottom: 0; }
  .row:hover { background: color-mix(in srgb, var(--text) 4%, transparent); }
  .row.group { font-weight: 600; background: color-mix(in srgb, var(--text) 3%, transparent); grid-template-columns: 20px 1fr; }
  .row.child { padding-left: 38px; }
  .row input { grid-area: box; }
  .row .name { grid-area: name; }
  .row .tag { grid-area: tag; justify-self: start; }
  .desc { grid-area: desc; color: var(--muted); font-size: 12.5px; line-height: 1.4; }
  .tag { font-size: 11px; color: var(--muted); border: 1px solid var(--line); border-radius: 99px; padding: 1px 8px; }
  input[type='checkbox'] { accent-color: var(--accent); width: 16px; height: 16px; margin: 0; }
  .path { width: 100%; padding: 11px 14px; border-radius: 10px; border: 1px solid var(--line); background: var(--panel); color: var(--text); font: 14px ui-monospace, Menlo, monospace; margin-bottom: 16px; }
  .path:focus { outline: 2px solid var(--accent); border-color: transparent; }
  .summary { font-size: 16px; line-height: 1.9; padding-left: 20px; }
  .progress { list-style: none; padding: 0; margin: 0 0 16px; display: grid; gap: 10px; }
  .progress li { color: var(--text); font-size: 15px; }
  .ico { width: 20px; text-align: center; }
  .progress .ok .ico { color: #30d158; }
  .progress .fail .ico { color: #ff453a; }
  .spin { width: 14px; height: 14px; border: 2px solid var(--line); border-top-color: var(--accent); border-radius: 50%; display: block; animation: spin .8s linear infinite; }
  @keyframes spin { to { transform: rotate(360deg); } }
  .log { height: 220px; overflow: auto; padding: 12px 14px; border-radius: var(--radius); background: color-mix(in srgb, var(--text) 5%, transparent); font: 12px/1.55 ui-monospace, Menlo, monospace; white-space: pre-wrap; }
  .dl { display: grid; grid-template-columns: minmax(0, 1fr) 120px 150px; gap: 10px; align-items: center; margin: 3px 0; }
  .dn { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .dd { color: var(--muted); text-align: right; white-space: nowrap; }
  .bar { height: 4px; border-radius: 4px; background: var(--line); overflow: hidden; }
  .bar i { display: block; height: 100%; background: var(--accent); transition: width .25s; }
  footer { display: flex; justify-content: space-between; padding: 16px 56px 28px; border-top: 1px solid var(--line); }
  button { padding: 9px 22px; border-radius: 99px; border: 0; font-size: 14px; cursor: pointer; transition: opacity .15s, transform .1s; }
  button:active:not(:disabled) { transform: scale(.97); }
  button:disabled { opacity: .35; cursor: default; }
  .primary { background: var(--accent); color: #fff; }
  .ghost { background: color-mix(in srgb, var(--text) 8%, transparent); }
</style>
