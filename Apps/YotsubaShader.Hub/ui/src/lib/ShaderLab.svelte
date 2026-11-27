<script lang="ts">
  import { onMount, untrack } from 'svelte';
  import { EditorState } from '@codemirror/state';
  import { EditorView, keymap, lineNumbers, highlightActiveLine, drawSelection } from '@codemirror/view';
  import { defaultKeymap, history, historyKeymap, indentWithTab } from '@codemirror/commands';
  import { StreamLanguage, syntaxHighlighting, HighlightStyle, bracketMatching, indentOnInput } from '@codemirror/language';
  import { csharp } from '@codemirror/legacy-modes/mode/clike';
  import { tags as t } from '@lezer/highlight';
  import { compileShader } from './api';
  import { ShaderRenderer } from './webgpu';

  type Shader = { id: string; title: string; style: string; label: string; code: string };
  let { shader, back }: { shader: Shader; back: () => void } = $props();

  const es = false; // the Hub UI is English only
  const T = {
    back: es ? 'Shaders' : 'Shaders', reset: es ? 'Restaurar' : 'Reset', restart: es ? 'Reiniciar tiempo' : 'Restart time',
    compiling: es ? 'Compilando…' : 'Compiling…', running: es ? 'En ejecución' : 'Running', error: es ? 'Error' : 'Error',
    first: es ? 'Primera compilación: se descargan los paquetes, puede tardar un poco.' : 'First compile: packages are being downloaded, this can take a moment.',
  };

  // The gallery remounts this view for each shader ({#key}), so the first values are the ones that matter.
  const key = untrack(() => `lab.code.${shader.id}`);
  const saved = (() => { try { return localStorage.getItem(key); } catch { return null; } })();

  let host: HTMLElement;
  let canvas: HTMLCanvasElement;
  let view: EditorView;
  let renderer: ShaderRenderer | null = null;
  let status = $state<'compiling' | 'ok' | 'error'>('compiling');
  let log = $state('');
  let gpuProblem = $state<string | null>(null);
  let slow = $state(false);
  let timer: ReturnType<typeof setTimeout>;
  let dirty = false, busy = false, everCompiled = false;

  const highlight = HighlightStyle.define([
    { tag: [t.keyword, t.modifier, t.controlKeyword], color: 'light-dark(#a626a4, #c678dd)' },
    { tag: [t.typeName, t.className, t.namespace], color: 'light-dark(#986801, #e5c07b)' },
    { tag: [t.string, t.character], color: 'light-dark(#50a14f, #98c379)' },
    { tag: [t.number, t.bool, t.atom], color: 'light-dark(#b76b01, #d19a66)' },
    { tag: [t.lineComment, t.blockComment], color: 'light-dark(#8e8e93, #7f848e)', fontStyle: 'italic' },
    { tag: [t.function(t.variableName), t.propertyName], color: 'light-dark(#4078f2, #61afef)' },
    { tag: t.meta, color: 'light-dark(#0184bc, #56b6c2)' },
  ]);

  const theme = EditorView.theme({
    '&': { height: '100%', fontSize: '13px', background: 'transparent', color: 'var(--text)' },
    '.cm-scroller': { fontFamily: "ui-monospace, 'SF Mono', Menlo, monospace", lineHeight: '1.55' },
    '.cm-gutters': { background: 'transparent', border: 'none', color: 'var(--muted)' },
    '.cm-activeLine, .cm-activeLineGutter': { background: 'color-mix(in srgb, var(--text) 5%, transparent)' },
    '&.cm-focused': { outline: 'none' },
    '.cm-cursor': { borderLeftColor: 'var(--accent)' },
    '.cm-selectionBackground, &.cm-focused .cm-selectionBackground': { background: 'color-mix(in srgb, var(--accent) 30%, transparent) !important' },
  });

  function schedule(delay = 650) {
    clearTimeout(timer);
    timer = setTimeout(pump, delay);
  }

  // One compile at a time; edits made meanwhile are compiled right after, with the latest text only.
  async function pump() {
    dirty = true;
    if (busy) return;
    busy = true;
    try {
      while (dirty) {
        dirty = false;
        const code = view.state.doc.toString();
        try { localStorage.setItem(key, code); } catch { /* not persisted */ }
        status = 'compiling';
        slow = false;
        const wait = !everCompiled ? setTimeout(() => (slow = true), 6000) : undefined;
        try {
          const r = await compileShader(code);
          everCompiled = true;
          if (!r.ok || !r.wgsl) { status = 'error'; log = r.log; continue; }
          if (!renderer) { status = 'error'; log = gpuProblem ?? ''; continue; }
          const problem = await renderer.setShader(r.wgsl);
          status = problem ? 'error' : 'ok';
          log = problem ?? '';
        } catch (e) {
          status = 'error'; log = (e as Error).message;
        } finally { clearTimeout(wait); slow = false; }
      }
    } finally { busy = false; }
  }

  function reset() {
    try { localStorage.removeItem(key); } catch { /* nothing to remove */ }
    view.dispatch({ changes: { from: 0, to: view.state.doc.length, insert: shader.code } });
    renderer?.restart();
  }

  onMount(() => {
    view = new EditorView({
      parent: host,
      state: EditorState.create({
        doc: saved ?? shader.code,
        extensions: [
          lineNumbers(), highlightActiveLine(), drawSelection(), history(), indentOnInput(), bracketMatching(),
          StreamLanguage.define(csharp), syntaxHighlighting(highlight), theme,
          keymap.of([...defaultKeymap, ...historyKeymap, indentWithTab]),
          EditorView.updateListener.of((u) => { if (u.docChanged) schedule(); }),
        ],
      }),
    });
    (async () => {
      const r = await ShaderRenderer.create(canvas);
      if (typeof r === 'string') gpuProblem = r; else renderer = r;
      pump();
    })();
    return () => { clearTimeout(timer); view.destroy(); renderer?.destroy(); };
  });
</script>

<div class="lab">
  <header>
    <button class="link" onclick={back}>‹ {T.back}</button>
    <h2>{shader.title}</h2>
    <span class="chip">{shader.label}</span>
    <span class="grow"></span>
    <button class="ghost" onclick={() => renderer?.restart()}>{T.restart}</button>
    <button class="ghost" onclick={reset}>{T.reset}</button>
    <span class="state" class:ok={status === 'ok'} class:bad={status === 'error'}>
      {status === 'compiling' ? T.compiling : status === 'ok' ? T.running : T.error}
    </span>
  </header>

  <div class="body">
    <div class="editor" bind:this={host}></div>
    <div class="side">
      <div class="stage">
        <canvas bind:this={canvas}></canvas>
        {#if gpuProblem}<p class="over">{gpuProblem}</p>{/if}
      </div>
      {#if slow}<p class="note">{T.first}</p>{/if}
      {#if log}<pre class="log">{log}</pre>{/if}
    </div>
  </div>
</div>

<style>
  .lab { display: flex; flex-direction: column; min-height: 0; padding-top: 34px; }
  header { display: flex; align-items: center; gap: 12px; padding: 0 20px 12px; }
  h2 { margin: 0; font-size: 18px; }
  .grow { flex: 1; }
  .chip { font-size: 11.5px; padding: 2px 9px; border-radius: 99px; background: color-mix(in srgb, var(--accent) 18%, transparent); color: var(--accent); }
  .link { background: none; border: 0; color: var(--accent); cursor: pointer; font: inherit; font-size: 14px; padding: 0; }
  .ghost { padding: 5px 13px; border-radius: 99px; border: 0; background: color-mix(in srgb, var(--text) 8%, transparent); color: var(--text); font: inherit; font-size: 12.5px; cursor: pointer; }
  .state { font-size: 12.5px; color: var(--muted); min-width: 92px; text-align: right; }
  .state.ok { color: #30d158; } .state.bad { color: #ff453a; }
  .body { flex: 1; min-height: 0; display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); border-top: 1px solid var(--line); }
  .editor { min-height: 0; overflow: hidden; border-right: 1px solid var(--line); padding: 8px 0; }
  .editor :global(.cm-editor) { height: 100%; }
  .side { display: flex; flex-direction: column; min-height: 0; min-width: 0; }
  .stage { position: relative; flex: 1; min-height: 160px; background: #000; }
  canvas { position: absolute; inset: 0; width: 100%; height: 100%; display: block; }
  .over { position: absolute; inset: 0; margin: 0; display: grid; place-items: center; padding: 24px; text-align: center; color: #fff; font-size: 14px; }
  .note { margin: 0; padding: 8px 16px; font-size: 12.5px; color: var(--muted); border-top: 1px solid var(--line); }
  .log { margin: 0; max-height: 38%; overflow: auto; padding: 12px 16px; border-top: 1px solid var(--line); font: 12px/1.5 ui-monospace, 'SF Mono', Menlo, monospace; color: #ff453a; white-space: pre-wrap; background: color-mix(in srgb, #ff453a 7%, transparent); }
</style>
