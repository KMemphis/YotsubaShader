<script lang="ts">
  import { onMount } from 'svelte';
  import { getState, type Env } from './api';

  let { setup }: { setup: () => void } = $props();

  let env = $state<Env | null>(null);
  let client = $state<'claude-code' | 'claude-desktop' | 'cursor' | 'vscode' | 'codex'>('claude-code');
  let fullPath = $state(true);
  let copied = $state('');

  onMount(async () => { env = (await getState()).env; });

  const installed = $derived(!!env?.cliVersion);
  // Desktop apps often start with a short PATH that does not include ~/.dotnet/tools, so the full path is the safe default.
  const exe = $derived(fullPath && env?.cliPath ? env.cliPath : 'yotsuba');

  const clients = [
    { id: 'claude-code', name: 'Claude Code', where: 'Run in a terminal' },
    { id: 'claude-desktop', name: 'Claude Desktop', where: 'claude_desktop_config.json' },
    { id: 'cursor', name: 'Cursor', where: '~/.cursor/mcp.json' },
    { id: 'vscode', name: 'VS Code', where: '.vscode/mcp.json' },
    { id: 'codex', name: 'Codex', where: '~/.codex/config.toml' },
  ] as const;

  const json = (key: string, extra = '') => JSON.stringify({ [key]: { yotsubashader: { ...(extra ? { type: 'stdio' } : {}), command: exe, args: ['mcp'] } } }, null, 2);
  const config = $derived(
    client === 'claude-code' ? `claude mcp add yotsubashader -- ${exe} mcp`
    : client === 'vscode' ? json('servers', 'stdio')
    : client === 'codex' ? `[mcp_servers.yotsubashader]\ncommand = ${JSON.stringify(exe)}\nargs = ["mcp"]`
    : json('mcpServers'),
  );
  const where = $derived(clients.find((c) => c.id === client)!.where);

  const tools = [
    ['search_docs', 'Full-text search over the reference'],
    ['list_docs · get_doc', 'List the pages and read one as Markdown'],
    ['list_diagnostics · get_diagnostic', 'The YS0000 analyzer diagnostics, with examples'],
    ['list_examples · search_examples', 'Find example shaders by style, tag or technique'],
    ['get_example', 'The full C# source of an example'],
  ];

  async function copy(text: string) {
    try { await navigator.clipboard.writeText(text); copied = text; setTimeout(() => (copied = ''), 1400); } catch { /* clipboard unavailable */ }
  }
</script>

<div class="page">
  <header>
    <div class="mark"><svg viewBox="0 0 24 24" width="30" height="30" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M9 3v5M15 3v5M6 8h12v4a6 6 0 0 1-12 0V8zM12 18v3"/></svg></div>
    <div>
      <h1>MCP server</h1>
      <p class="lead">Give an AI assistant the whole reference and 100 working example shaders, so it writes Yotsuba Shader code from real examples instead of guessing.</p>
    </div>
  </header>

  <div class="status" class:ok={installed}>
    <span class="dot"></span>
    {#if !env}Checking…
    {:else if installed}<div><b>Ready</b><small>It comes with the compiler ({env.cliVersion}). Nothing else to install.</small></div>
    {:else}<div><b>The compiler is not installed</b><small>The MCP server is part of the <code>yotsuba</code> tool.</small></div><button class="primary" onclick={setup}>Open Install</button>{/if}
  </div>

  <h2>Connect your assistant</h2>
  <div class="seg" role="tablist">
    {#each clients as c}
      <button role="tab" aria-selected={client === c.id} class:on={client === c.id} onclick={() => (client = c.id)}>{c.name}</button>
    {/each}
  </div>

  <div class="where">
    <span>{client === 'claude-code' ? 'Run in a terminal' : `Add to ${where}`}</span>
    <label><input type="checkbox" bind:checked={fullPath} /> Use the full path</label>
  </div>
  <button class="code" onclick={() => copy(config)} title="Copy">
    <pre>{config}</pre>
    <span>{copied === config ? 'Copied' : 'Copy'}</span>
  </button>
  <p class="hint">
    {#if client === 'claude-desktop' || client === 'cursor' || client === 'vscode'}Restart {clients.find((c) => c.id === client)?.name} after saving.{:else if client === 'codex'}Codex reads this file when it starts.{:else}Then ask Claude to write a shader and it will read the examples first.{/if}
    {#if fullPath}The full path is used because desktop apps often do not see <code>~/.dotnet/tools</code> in their PATH.{/if}
  </p>

  <h2>What the assistant can use</h2>
  <div class="tools">
    {#each tools as [name, what]}
      <div class="tool"><code>{name}</code><span>{what}</span></div>
    {/each}
  </div>
  <p class="hint">It also exposes every page and example as a resource, and a <code>write_shader</code> prompt that starts from the closest example. Everything is read-only and runs on your computer.</p>

  <div class="stats">
    <div><b>100</b><span>example shaders</span></div>
    <div><b>5</b><span>syntaxes: HLSL, GLSL, MSL, SPIR-V, WGSL</span></div>
    <div><b>2</b><span>languages: English and Spanish</span></div>
  </div>
</div>

<style>
  .page { overflow: auto; padding: 44px 40px 60px; }
  .page > * { max-width: 860px; }
  header { display: flex; gap: 18px; align-items: flex-start; }
  .mark {
    width: 56px; height: 56px; border-radius: 16px; display: grid; place-items: center; flex: none; color: #fff;
    background: linear-gradient(145deg, #ff6aa8, #8f6bff 55%, #3fd4c3); box-shadow: 0 10px 26px -10px #8f6bff;
  }
  h1 { font-size: 32px; letter-spacing: -.025em; margin: 0 0 8px; }
  h2 { font-size: 20px; letter-spacing: -.015em; margin: 36px 0 14px; }
  .lead { color: var(--muted, #8e8e93); line-height: 1.5; margin: 0 0 24px; max-width: 600px; }
  code { font: 12.5px ui-monospace, Menlo, monospace; background: color-mix(in srgb, var(--text) 8%, transparent); padding: 2px 6px; border-radius: 5px; }

  .status { display: flex; align-items: center; gap: 14px; padding: 14px 18px; border-radius: 14px; background: color-mix(in srgb, #ff9f0a 16%, transparent); border: 1px solid color-mix(in srgb, #ff9f0a 40%, transparent); }
  .status.ok { background: color-mix(in srgb, #30d158 14%, transparent); border-color: color-mix(in srgb, #30d158 40%, transparent); }
  .status div { flex: 1; display: flex; flex-direction: column; gap: 2px; }
  .status small { color: var(--muted, #8e8e93); font-size: 12.5px; }
  .dot { width: 10px; height: 10px; border-radius: 50%; background: #ff9f0a; flex: none; }
  .status.ok .dot { background: #30d158; }
  .primary { border: 0; border-radius: 99px; padding: 8px 18px; font: inherit; font-size: 13px; font-weight: 600; background: var(--accent); color: #fff; cursor: pointer; }

  .seg { display: grid; grid-template-columns: repeat(5, 1fr); padding: 3px; border-radius: 12px; background: color-mix(in srgb, var(--text) 8%, transparent); }
  .seg button { border: 0; background: none; font: inherit; font-size: 13px; padding: 8px 4px; border-radius: 9px; cursor: pointer; color: var(--muted, #8e8e93); }
  .seg button.on { background: color-mix(in srgb, var(--text) 16%, transparent); color: var(--text); }

  .where { display: flex; justify-content: space-between; align-items: center; margin: 16px 0 8px; font-size: 13px; color: var(--muted, #8e8e93); }
  .where label { display: flex; gap: 6px; align-items: center; cursor: pointer; }
  .code { position: relative; display: block; width: 100%; text-align: left; border: 0; padding: 16px 18px; border-radius: 14px; cursor: pointer; color: inherit; background: color-mix(in srgb, #000 40%, transparent); font: inherit; }
  .code:hover { background: color-mix(in srgb, #000 55%, transparent); }
  .code pre { margin: 0; font: 13px/1.55 ui-monospace, Menlo, monospace; white-space: pre-wrap; overflow-wrap: anywhere; padding-right: 60px; }
  .code span { position: absolute; top: 14px; right: 16px; font-size: 12px; font-weight: 600; color: var(--accent); }
  .hint { color: var(--muted, #8e8e93); font-size: 13px; line-height: 1.5; margin: 10px 0 0; }

  .tools { display: grid; gap: 8px; }
  .tool { display: flex; gap: 14px; align-items: center; padding: 11px 14px; border-radius: 12px; background: var(--panel); border: 1px solid var(--line); font-size: 13.5px; }
  .tool code { flex: none; min-width: 250px; }
  .tool span { color: var(--muted, #8e8e93); }

  .stats { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; margin-top: 30px; }
  .stats div { padding: 16px; border-radius: 14px; background: var(--panel); border: 1px solid var(--line); display: flex; flex-direction: column; gap: 4px; }
  .stats b { font-size: 28px; letter-spacing: -.02em; background: linear-gradient(120deg, #ff6aa8, #8f6bff, #3fd4c3); -webkit-background-clip: text; background-clip: text; color: transparent; }
  .stats span { font-size: 12.5px; color: var(--muted, #8e8e93); }
</style>
