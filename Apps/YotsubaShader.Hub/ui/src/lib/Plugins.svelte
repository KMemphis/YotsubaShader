<script lang="ts">
  import { onMount } from 'svelte';
  import { getState, getInstalledPlugins, startInstall, watchInstall, type Component, type Env, type InstalledPlugin } from './api';

  let plugins = $state<Component[]>([]);
  let env = $state<Env | null>(null);
  let installed = $state<InstalledPlugin[]>([]);
  let busy = $state<string | null>(null);
  let error = $state<string | null>(null);
  let copied = $state('');
  let tab = $state<Record<string, 'install' | 'add' | 'compile'>>({});

  // Each engine gets its logo, its colour and the file it produces.
  const look: Record<string, { hue: string; logo: string; file: string }> = {
    monogame: { hue: '#e7472b', logo: '/engines/monogame.png', file: '.mgfx' },
    kni: { hue: '#2f7bff', logo: '/engines/kni.png', file: '.knifx' },
    fna: { hue: '#9a5cff', logo: '/engines/fna.png', file: '.fxb' },
    godot: { hue: '#37a2e0', logo: '/engines/godot.svg', file: '.gdshader' },
  };

  // Graphics APIs ship inside the compiler: they are shown for reference and cannot be installed or updated here.
  const apis = [
    { id: 'spirv', name: 'Vulkan', lang: 'SPIR-V', file: '.spv', hue: '#ac162c', logo: '/engines/vulkan.svg', note: 'Versions 1.3 to 1.6', ver: 'spirv:1.3' },
    { id: 'hlsl', name: 'Direct3D', lang: 'HLSL', file: '.hlsl', hue: '#16a34a', logo: '/engines/directx.svg', note: 'Shader models 5.0 and 6.x', ver: 'hlsl:6.0' },
    { id: 'glsl', name: 'OpenGL', lang: 'GLSL', file: '.glsl', hue: '#5586a4', logo: '/engines/opengl.svg', note: 'Versions 330 to 460', ver: 'glsl:430' },
    { id: 'gles', name: 'OpenGL ES', lang: 'GLSL ES', file: '.glsl', hue: '#5586a4', logo: '/engines/opengl.svg', note: 'Versions 300, 310 and 320', ver: 'gles:300' },
    { id: 'msl', name: 'Metal', lang: 'MSL', file: '.metal', hue: '#8e8e93', logo: '/engines/metal.svg', note: 'Versions 2.0 and later', ver: 'msl:2.4' },
    { id: 'wgsl', name: 'WebGPU', lang: 'WGSL', file: '.wgsl', hue: '#005a9c', logo: '/engines/webgpu.svg', note: 'Converted with naga', ver: 'wgsl' },
  ];
  let apiTab = $state<Record<string, 'compile' | 'add'>>({});
  const apiCommand = (id: string, t: 'compile' | 'add') =>
    t === 'add' ? `yotsuba add MyShader.csproj --target ${id}` : `yotsuba compile MyShader.csproj --target ${id} --output out`;

  async function load() {
    const s = await getState();
    plugins = s.components.filter((c) => c.kind === 'Plugin');
    env = s.env;
    installed = await getInstalledPlugins();
  }
  onMount(load);

  const core = (v: string | null | undefined) => v?.split(/[-+]/)[0] ?? null;
  type Status = 'missing' | 'current' | 'outdated';
  function statusOf(id: string): Status {
    const p = installed.find((i) => i.id === id);
    if (!p) return 'missing';
    // The compiler only uses a plugin built for its own version; anything else is stale.
    return env?.cliVersion && core(p.version) !== core(env.cliVersion) ? 'outdated' : 'current';
  }
  const versionOf = (id: string) => installed.find((i) => i.id === id)?.version ?? null;
  const outdated = $derived(plugins.filter((p) => statusOf(p.id) === 'outdated'));

  async function run(ids: string[]) {
    if (!env) return;
    error = null; busy = ids.length === 1 ? ids[0] : 'all';
    const problem = await startInstall(ids, env.defaultHome);
    if (problem) { error = problem; busy = null; return; }
    const stop = watchInstall(async (e) => {
      if (e.type === 'step-fail' && e.message) error = e.message;
      if (e.type === 'done') { stop(); busy = null; await load(); }
    });
  }

  async function copy(text: string) {
    try { await navigator.clipboard.writeText(text); copied = text; setTimeout(() => (copied = ''), 1400); } catch { /* clipboard unavailable */ }
  }

  const command = (id: string, t: 'install' | 'add' | 'compile') =>
    t === 'install' ? `yotsuba plugin install ${id}`
    : t === 'add' ? `yotsuba add MyShader.csproj --target ${id}`
    : `yotsuba compile MyShader.csproj --target ${id} --output out`;
  const tabs = [['install', 'Install'], ['add', 'Add to project'], ['compile', 'Compile']] as const;
</script>

<div class="page">
  <header>
    <div>
      <h1>Plugins</h1>
      <p class="lead">One plugin per game engine. Install it once and the compiler can produce that engine's effects.</p>
    </div>
    {#if env?.cliVersion}<span class="ver">Compiler {env.cliVersion}</span>{/if}
  </header>

  {#if outdated.length}
    <div class="notice">
      <span class="pulse"></span>
      <div>
        <b>{outdated.length === 1 ? `${plugins.find((p) => p.id === outdated[0].id)?.name} is out of date` : `${outdated.length} plugins are out of date`}</b>
        <small>They were built for an older compiler, so the effects they generate may be wrong or fail.</small>
      </div>
      <button class="primary" disabled={busy !== null} onclick={() => run(outdated.map((p) => p.id))}>{busy === 'all' ? 'Updating…' : 'Update all'}</button>
    </div>
  {/if}

  <section class="cards">
    {#each plugins as p}
      {@const st = statusOf(p.id)}
      {@const l = look[p.id] ?? { hue: '#8e8e93', logo: '', file: '' }}
      {@const t = tab[p.id] ?? 'install'}
      <article class="card" style={`--hue:${l.hue}`}>
        <div class="head">
          <div class="logo"><img src={l.logo} alt="" /></div>
          <div class="who">
            <h3>{p.name}</h3>
            <span class="file">{l.file}</span>
          </div>
          <span class="badge {st}">
            {st === 'missing' ? 'Not installed' : st === 'outdated' ? 'Update available' : 'Up to date'}
          </span>
        </div>

        <p class="desc">{p.description}</p>

        {#if versionOf(p.id)}
          <div class="meta">
            <span>Installed <b>{versionOf(p.id)}</b></span>
            {#if st === 'outdated' && env?.cliVersion}<span class="arrow">→</span><span>Needs <b>{core(env.cliVersion)}</b></span>{/if}
          </div>
        {/if}

        <div class="seg" role="tablist">
          {#each tabs as [key, label]}
            <button role="tab" aria-selected={t === key} class:on={t === key} onclick={() => (tab[p.id] = key)}>{label}</button>
          {/each}
        </div>
        <button class="cmd" onclick={() => copy(command(p.id, t))} title="Copy">
          <code>{command(p.id, t)}</code>
          <span>{copied === command(p.id, t) ? 'Copied' : 'Copy'}</span>
        </button>

        <button class="action {st}" disabled={busy !== null || st === 'current'} onclick={() => run([p.id])}>
          {#if busy === p.id || busy === 'all'}Working…
          {:else if st === 'missing'}Install {p.name}
          {:else if st === 'outdated'}Update {p.name}
          {:else}✓ Up to date{/if}
        </button>
      </article>
    {/each}
  </section>
  {#if error}<p class="err">{error}</p>{/if}

  <h2>Graphics APIs</h2>
  <p class="lead sub">These come with the compiler, so there is nothing to install or update. Use them as <code>--target</code>.</p>
  <section class="cards">
    {#each apis as a}
      {@const t = apiTab[a.id] ?? 'compile'}
      <article class="card" style={`--hue:${a.hue}`}>
        <div class="head">
          <div class="logo wide"><img src={a.logo} alt="" /></div>
          <div class="who">
            <h3>{a.name}</h3>
            <span class="file">{a.lang} · {a.file}</span>
          </div>
          <span class="badge current">Installed</span>
        </div>
        <p class="desc">{a.note}.</p>
        <div class="seg two" role="tablist">
          {#each [['compile', 'Compile'], ['add', 'Add to project']] as [key, label]}
            <button role="tab" aria-selected={t === key} class:on={t === key} onclick={() => (apiTab[a.id] = key as 'compile' | 'add')}>{label}</button>
          {/each}
        </div>
        <button class="cmd" onclick={() => copy(apiCommand(a.id, t))} title="Copy">
          <code>{apiCommand(a.id, t)}</code>
          <span>{copied === apiCommand(a.id, t) ? 'Copied' : 'Copy'}</span>
        </button>
        <small class="tip">Pick a version with <code>--target {a.ver}</code></small>
        <button class="action current" disabled>✓ Installed with the compiler</button>
      </article>
    {/each}
  </section>
</div>

<style>
  .page { overflow: auto; padding: 44px 40px 60px; }
  header { display: flex; justify-content: space-between; align-items: flex-start; gap: 20px; max-width: 1100px; }
  h1 { font-size: 32px; letter-spacing: -.025em; margin: 0 0 8px; }
  .lead { color: var(--muted, #8e8e93); line-height: 1.5; margin: 0 0 26px; max-width: 560px; }
  .ver { font: 12px ui-monospace, Menlo, monospace; padding: 5px 11px; border-radius: 99px; background: color-mix(in srgb, var(--text) 8%, transparent); margin-top: 8px; }

  .notice {
    display: flex; align-items: center; gap: 14px; max-width: 1100px; margin: 0 0 20px; padding: 14px 18px; border-radius: 14px;
    background: linear-gradient(100deg, color-mix(in srgb, #ff9f0a 26%, transparent), color-mix(in srgb, #ff453a 16%, transparent));
    border: 1px solid color-mix(in srgb, #ff9f0a 45%, transparent);
  }
  .notice div { flex: 1; display: flex; flex-direction: column; gap: 2px; }
  .notice small { color: var(--muted, #8e8e93); font-size: 12.5px; }
  .pulse { width: 10px; height: 10px; border-radius: 50%; background: #ff9f0a; animation: pulse 1.8s infinite; }
  @keyframes pulse { 0% { box-shadow: 0 0 0 0 #ff9f0a99; } 70%, 100% { box-shadow: 0 0 0 10px #ff9f0a00; } }

  .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 16px; max-width: 1100px; }
  .card {
    display: flex; flex-direction: column; gap: 12px; padding: 18px; border-radius: 18px; overflow: hidden;
    background: radial-gradient(120% 70% at 0% 0%, color-mix(in srgb, var(--hue) 22%, transparent), transparent 60%), var(--panel);
    border: 1px solid color-mix(in srgb, var(--hue) 30%, var(--line));
    transition: transform .18s ease, box-shadow .18s ease;
  }
  .card:hover { transform: translateY(-3px); box-shadow: 0 14px 34px -14px color-mix(in srgb, var(--hue) 60%, transparent); }

  .head { display: flex; align-items: center; gap: 12px; }
  .logo {
    width: 48px; height: 48px; border-radius: 13px; display: grid; place-items: center; flex: none;
    background: #fff; box-shadow: 0 6px 16px -6px var(--hue);
  }
  .logo.wide { width: 86px; }
  .logo.wide img { width: 70px; height: 36px; }
  .logo img { width: 32px; height: 32px; object-fit: contain; }
  .who { flex: 1; min-width: 0; }
  .who h3 { margin: 0; font-size: 17px; letter-spacing: -.01em; }
  .file { font: 12px ui-monospace, Menlo, monospace; color: var(--muted, #8e8e93); }

  .badge { font-size: 11.5px; font-weight: 600; padding: 4px 10px; border-radius: 99px; white-space: nowrap; }
  .badge.current { background: color-mix(in srgb, #30d158 22%, transparent); color: #30d158; }
  .badge.outdated { background: color-mix(in srgb, #ff9f0a 26%, transparent); color: #ff9f0a; }
  .badge.builtin { background: color-mix(in srgb, var(--text) 10%, transparent); color: var(--text); }
  .badge.missing { background: color-mix(in srgb, var(--text) 10%, transparent); color: var(--muted, #8e8e93); }

  .desc { margin: 0; font-size: 13.5px; line-height: 1.45; color: var(--muted, #8e8e93); }
  .meta { display: flex; gap: 8px; align-items: center; font-size: 12.5px; color: var(--muted, #8e8e93); }
  .meta b { color: var(--text); font-family: ui-monospace, Menlo, monospace; font-weight: 500; }
  .arrow { color: #ff9f0a; }

  .seg { display: grid; grid-template-columns: repeat(3, 1fr); padding: 3px; border-radius: 10px; background: color-mix(in srgb, var(--text) 8%, transparent); }
  .seg.two { grid-template-columns: repeat(2, 1fr); }
  h2 { font-size: 22px; letter-spacing: -.02em; margin: 44px 0 6px; max-width: 1100px; }
  .lead.sub { margin-bottom: 18px; }
  .tip { font-size: 12px; color: var(--muted, #8e8e93); }
  .tip code, .lead code { font: 12px ui-monospace, Menlo, monospace; }
  .seg button { border: 0; background: none; font: inherit; font-size: 12px; padding: 5px 4px; border-radius: 8px; cursor: pointer; color: var(--muted, #8e8e93); transition: background .15s, color .15s; }
  .seg button.on { background: color-mix(in srgb, var(--text) 16%, transparent); color: var(--text); }

  .cmd {
    display: flex; align-items: center; gap: 10px; text-align: left; border: 0; padding: 10px 12px; border-radius: 10px; cursor: pointer; color: inherit;
    background: color-mix(in srgb, #000 35%, transparent); font: inherit; width: 100%;
  }
  .cmd code { flex: 1; font: 12px/1.45 ui-monospace, Menlo, monospace; overflow-wrap: anywhere; background: none; padding: 0; }
  .cmd span { font-size: 11.5px; color: var(--hue); font-weight: 600; flex: none; }
  .cmd:hover { background: color-mix(in srgb, #000 50%, transparent); }

  .action { margin-top: auto; border: 0; border-radius: 12px; padding: 11px; font: inherit; font-weight: 600; font-size: 14px; cursor: pointer; color: #fff; background: var(--hue); transition: filter .15s; }
  .action:hover:not(:disabled) { filter: brightness(1.12); }
  .action.outdated { background: #ff9f0a; color: #1c1c1e; }
  .action.current { background: color-mix(in srgb, var(--text) 9%, transparent); color: #30d158; cursor: default; }
  .action:disabled:not(.current) { opacity: .55; cursor: default; }

  .primary { border: 0; border-radius: 99px; padding: 8px 18px; font: inherit; font-size: 13px; font-weight: 600; background: #ff9f0a; color: #1c1c1e; cursor: pointer; }
  .primary:disabled { opacity: .55; }
  .err { color: #ff453a; font-size: 13px; white-space: pre-wrap; max-width: 1100px; }
</style>
