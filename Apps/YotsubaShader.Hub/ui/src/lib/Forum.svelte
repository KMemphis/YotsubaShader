<script lang="ts">
  import { onMount } from 'svelte';
  import { forum, type Author, type ForumInfo, type ForumSession, type Message, type Thread, type ThreadSummary } from './api';
  import { ago, initials } from './format';
  import Body from './Body.svelte';

  const quick = ['👍', '❤️', '🔥', '🎉'];

  let info = $state<ForumInfo | null>(null);
  let session = $state<ForumSession | null>(null);
  let list = $state<ThreadSummary[]>([]);
  let tag = $state('All');
  let query = $state('');
  let open = $state<Thread | null>(null);
  let composing = $state(false);
  let reply = $state('');
  let draft = $state({ title: '', tag: 'Help', body: '' });
  let error = $state<string | null>(null);
  let scroller: HTMLElement | undefined = $state();
  let menu = $state<string | null>(null);
  let confirming = $state<string | null>(null);
  let editing = $state<{ id: string; text: string } | null>(null);
  let replyTo = $state<{ name: string; excerpt: string } | null>(null);
  let copied = $state<string | null>(null);
  let saved = $state<string[]>(readSaved());

  function readSaved(): string[] { try { return JSON.parse(localStorage.getItem('forum.saved') ?? '[]'); } catch { return []; } }
  function toggleSaved(key: string) {
    saved = saved.includes(key) ? saved.filter((k) => k !== key) : [...saved, key];
    try { localStorage.setItem('forum.saved', JSON.stringify(saved)); } catch { /* not persisted */ }
  }
  const isMine = (m: Message) => !!session?.user && m.author.id === session.user.id;
  const isStaff = $derived(session?.user?.role === 'Maintainer');
  const canSolve = $derived(!!open && !!session?.signedIn && (isStaff || (open.messages[0] ? isMine(open.messages[0]) : false)));

  async function copy(what: string, text: string) {
    try { await navigator.clipboard.writeText(text); copied = what; setTimeout(() => (copied = null), 1500); }
    catch { error = 'Could not copy to the clipboard.'; }
    menu = null;
  }
  function speak(text: string) {
    menu = null;
    if (!('speechSynthesis' in window)) { error = 'Reading aloud is not available here.'; return; }
    speechSynthesis.cancel(); speechSynthesis.speak(new SpeechSynthesisUtterance(text));
  }
  function startReply(m: Message) {
    replyTo = { name: m.author.name, excerpt: m.body.replace(/^>.*\n?/gm, '').replace(/\s+/g, ' ').trim().slice(0, 120) };
    menu = null;
  }
  async function act(fn: () => Promise<void>) {
    try { await fn(); error = null; } catch (e) { error = (e as Error).message; }
    menu = null; confirming = null;
  }
  const saveEdit = () => act(async () => {
    if (!open || !editing || !editing.text.trim()) return;
    const m = await forum.edit(open.id, editing.id, editing.text);
    open.messages = open.messages.map((x) => (x.id === m.id ? m : x));
    editing = null;
  });
  const remove = (m: Message) => act(async () => {
    if (!open) return;
    const wholePost = open.messages[0]?.id === m.id;
    await forum.remove(open.id, m.id);
    if (wholePost) open = null; else open = await forum.get(open.id);
    await refresh();
  });
  const pin = (m: Message) => act(async () => {
    if (!open) return;
    const r = await forum.pin(open.id, m.id, !m.pinned);
    open.messages = open.messages.map((x) => (x.id === r.id ? r : x));
  });
  const solve = () => act(async () => { if (open) { open = await forum.solve(open.id, !open.solved); await refresh(); } });

  async function refresh() { list = await forum.list(tag === 'All' ? '' : tag, query); }
  const tags = $derived(['All', ...(info?.tags ?? [])]);
  const canPost = $derived(!!session?.signedIn);

  async function loadSession() { try { session = await forum.session(); } catch { /* keep the last known state */ } }

  onMount(() => {
    (async () => {
      try { info = await forum.info(); await refresh(); } catch (e) { error = (e as Error).message; }
      await loadSession();
    })();
    // A live forum changes without us: re-read the list (and the open thread) now and then.
    const live = setInterval(async () => {
      if (!info?.live || document.hidden) return;
      try { await refresh(); if (open) open = await forum.get(open.id); } catch { /* try again next time */ }
    }, 15000);
    // While a sign-in is open in the browser, watch for it to finish.
    const signing = setInterval(() => { if (session?.signingIn) loadSession(); }, 2000);
    return () => { clearInterval(live); clearInterval(signing); };
  });

  async function signIn() {
    error = null;
    try { await forum.signIn(); await loadSession(); } catch (e) { error = (e as Error).message; }
  }
  async function signOut() { await forum.signOut(); await loadSession(); }

  let timer: ReturnType<typeof setTimeout>;
  const onSearch = () => { clearTimeout(timer); timer = setTimeout(refresh, 200); };
  const pick = async (t: string) => { tag = t; await refresh(); };

  async function show(id: string) {
    composing = false; error = null;
    open = await forum.get(id);
    queueMicrotask(() => scroller?.scrollTo({ top: 0 }));
  }

  async function send() {
    if (!open || !reply.trim()) return;
    try {
      const quote = replyTo ? `> **${replyTo.name}**: ${replyTo.excerpt}\n` : '';
      await forum.reply(open.id, quote + reply);
      reply = ''; replyTo = null; error = null;
      open = await forum.get(open.id);
      await refresh();
      queueMicrotask(() => scroller?.scrollTo({ top: scroller.scrollHeight, behavior: 'smooth' }));
    } catch (e) { error = (e as Error).message; }
  }

  async function post() {
    try {
      const t = await forum.create(draft.title, draft.tag, draft.body);
      draft = { title: '', tag: 'Help', body: '' };
      await refresh();
      await show(t.id);
    } catch (e) { error = (e as Error).message; }
  }

  async function react(mid: string, emoji: string) {
    if (!open || !info?.canReact) return;
    const m = await forum.react(open.id, mid, emoji);
    open.messages = open.messages.map((x) => (x.id === mid ? m : x));
  }

  const onKey = (e: KeyboardEvent, fn: () => void) => { if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) fn(); };
</script>

{#snippet avatar(a: Author)}
  {#if a.avatarUrl}<img class="av" src={a.avatarUrl} alt="" referrerpolicy="no-referrer" />
  {:else}<span class="av" style:background={a.color}>{initials(a.name)}</span>{/if}
{/snippet}

<svelte:window onclick={() => { menu = null; confirming = null; }} />
{#if copied}<div class="toast">Copied {copied}</div>{/if}
<div class="forum">
  <div class="list">
    <header>
      <div class="channel">
        <strong>{info?.channel ?? ''}</strong>
        <span>{info?.server}</span>
      </div>
      <button class="primary" disabled={!canPost} title={canPost ? '' : 'Sign in with Discord to post'} onclick={() => { composing = true; open = null; error = null; }}>New post</button>
    </header>
    {#if info}<p class="stats">{info.members.toLocaleString()} members · {info.online} online</p>{/if}
    {#if session?.mode === 'remote'}
      <div class="account">
        {#if session.signedIn && session.user}
          {@render avatar(session.user)}<span class="me">{session.user.name}</span>
          <button class="link" onclick={signOut}>Sign out</button>
        {:else if session.signingIn}
          <span class="wait">Confirm the code <b>{session.signInCode}</b> in your browser.
            {#if session.signInUrl}<a href={session.signInUrl} target="_blank" rel="noreferrer">Open the page again</a>{/if}</span>
        {:else}
          <button class="discord" onclick={signIn}>Sign in with Discord</button>
        {/if}
      </div>
    {/if}
    <input class="search" placeholder="Search posts" bind:value={query} oninput={onSearch} />
    <div class="tags">
      {#each tags as t}<button class:on={tag === t} onclick={() => pick(t)}>{t}</button>{/each}
    </div>
    <ul>
      {#each list as t (t.id)}
        <li>
          <button class="item" class:sel={open?.id === t.id} onclick={() => show(t.id)}>
            {@render avatar(t.author)}
            <span class="meta">
              <span class="title">{t.title}</span>
              <span class="preview">{t.preview}</span>
              <span class="foot">
                {#if t.pinned}<b>Pinned</b>{/if}
                {#if t.solved}<b class="ok">Solved</b>{/if}
                <span>{t.tag}</span><span>{t.replies} {t.replies === 1 ? 'reply' : 'replies'}</span><span>{ago(t.lastActivity)}</span>
              </span>
            </span>
          </button>
        </li>
      {:else}
        <li class="empty">No posts match.</li>
      {/each}
    </ul>
  </div>

  <div class="pane">
    {#if info && !info.live}<div class="notice">{info.notice}</div>{/if}

    {#if composing}
      <div class="scroll">
        <div class="thread">
          <h1>New post</h1>
          <input class="field" placeholder="Title" maxlength="120" bind:value={draft.title} />
          <select class="field" bind:value={draft.tag}>{#each tags.slice(1) as t}<option>{t}</option>{/each}</select>
          <textarea class="field" rows="10" placeholder="What is going on? Wrap code in ``` fences." bind:value={draft.body} onkeydown={(e) => onKey(e, post)}></textarea>
          {#if error}<p class="err">{error}</p>{/if}
          <div class="actions">
            <button class="ghost" onclick={() => (composing = false)}>Cancel</button>
            <button class="primary" disabled={!draft.title.trim() || !draft.body.trim()} onclick={post}>Post</button>
          </div>
        </div>
      </div>
    {:else if open}
      <div class="scroll" bind:this={scroller}>
        <div class="thread">
          <div class="chips"><span>{open.tag}</span>{#if open.solved}<span class="ok">Solved</span>{/if}
            {#if canSolve}<button class="link" onclick={solve}>{open.solved ? 'Reopen' : 'Mark as solved'}</button>{/if}</div>
          <h1>{open.title}</h1>
          {#each open.messages as m (m.id)}
            <article>
              {@render avatar(m.author)}
              <div class="content">
                <div class="who"><strong>{m.author.name}</strong>
                  {#if m.author.role !== 'Member'}<span class="role">{m.author.role}</span>{/if}
                  <time>{ago(m.at)}</time>
                  {#if m.edited}<time>(edited)</time>{/if}
                  {#if m.pinned}<span class="role">Pinned</span>{/if}
                  {#if saved.includes(`${open.id}:${m.id}`)}<span class="star" title="Saved">★</span>{/if}
                  <span class="more">
                    <button class="dots" aria-label="Message actions" onclick={(e) => { e.stopPropagation(); menu = menu === m.id ? null : m.id; confirming = null; }}>⋯</button>
                    {#if menu === m.id}
                      <div class="menu" role="menu" tabindex="-1" onclick={(e) => e.stopPropagation()} onkeydown={(e) => e.key === 'Escape' && (menu = null)}>
                        {#if canPost}<button onclick={() => startReply(m)}>Reply</button>{/if}
                        <button onclick={() => copy('text', m.body)}>Copy text</button>
                        {#if m.url}<button onclick={() => copy('link', m.url!)}>Copy message link</button>{/if}
                        <button onclick={() => copy('id', m.id)}>Copy message ID</button>
                        <button onclick={() => { toggleSaved(`${open!.id}:${m.id}`); menu = null; }}>{saved.includes(`${open.id}:${m.id}`) ? 'Remove bookmark' : 'Bookmark'}</button>
                        <button onclick={() => speak(m.body)}>Read aloud</button>
                        {#if isMine(m)}<button onclick={() => { editing = { id: m.id, text: m.body }; menu = null; }}>Edit</button>{/if}
                        {#if isStaff}<button onclick={() => pin(m)}>{m.pinned ? 'Unpin' : 'Pin'}</button>{/if}
                        {#if isMine(m) || isStaff}
                          <button class="danger" onclick={() => (confirming === m.id ? remove(m) : (confirming = m.id))}>
                            {confirming === m.id ? (open.messages[0]?.id === m.id ? 'Click again: delete the whole post' : 'Click again to delete') : 'Delete'}
                          </button>
                        {/if}
                      </div>
                    {/if}
                  </span></div>
                {#if editing?.id === m.id}
                  <textarea class="field" rows="4" bind:value={editing.text} onkeydown={(e) => onKey(e, saveEdit)}></textarea>
                  <div class="actions"><button class="ghost" onclick={() => (editing = null)}>Cancel</button>
                    <button class="primary" disabled={!editing.text.trim()} onclick={saveEdit}>Save</button></div>
                {:else}
                  <Body text={m.body} />
                {/if}
                <div class="reacts">
                  {#each m.reactions as r}
                    <button class:mine={r.mine} disabled={!info?.canReact} onclick={() => react(m.id, r.emoji)}>{r.emoji} {r.count}</button>
                  {/each}
                  {#if info?.canReact}<span class="add">
                    {#each quick.filter((q) => !m.reactions.some((r) => r.emoji === q)) as q}
                      <button onclick={() => react(m.id, q)} aria-label={`React ${q}`}>{q}</button>
                    {/each}
                  </span>{/if}
                </div>
              </div>
            </article>
          {/each}
        </div>
      </div>
      {#if canPost}
        {#if replyTo}<div class="replying">Replying to <b>{replyTo.name}</b>: {replyTo.excerpt}
          <button class="link" onclick={() => (replyTo = null)}>Cancel</button></div>{/if}
        <div class="composer">
          <textarea rows="2" placeholder="Reply" bind:value={reply} onkeydown={(e) => onKey(e, send)}></textarea>
          <button class="primary" disabled={!reply.trim()} onclick={send}>Send</button>
        </div>
      {:else}
        <div class="composer gate"><span>Sign in with Discord to reply.</span>
          <button class="primary" disabled={session?.signingIn} onclick={signIn}>Sign in</button></div>
      {/if}
      {#if error}<p class="err pad">{error}</p>{/if}
    {:else}
      <div class="blank"><h2>Pick a post</h2><p>Or start a new one.</p></div>
    {/if}
  </div>
</div>

<style>
  .forum { display: grid; grid-template-columns: 360px 1fr; height: 100%; }
  .list { display: flex; flex-direction: column; min-height: 0; border-right: 1px solid var(--line); padding-top: 38px; }
  header { display: flex; justify-content: space-between; align-items: center; padding: 0 16px; }
  .channel { display: grid; font-size: 15px; } .channel span { font-size: 12px; color: var(--muted); }
  .stats { margin: 6px 16px 12px; font-size: 12px; color: var(--muted); }
  .search, .field { width: 100%; padding: 9px 12px; border-radius: 10px; border: 1px solid var(--line); background: var(--panel); color: var(--text); font: inherit; font-size: 14px; }
  .search { width: calc(100% - 32px); margin: 0 16px 10px; }
  .search:focus, .field:focus, .composer textarea:focus { outline: 2px solid var(--accent); border-color: transparent; }
  .tags { display: flex; gap: 6px; padding: 0 16px 12px; flex-wrap: wrap; }
  .tags button { padding: 4px 11px; border-radius: 99px; border: 1px solid var(--line); background: none; font-size: 12.5px; cursor: pointer; }
  .tags button.on { background: var(--text); color: var(--bg); border-color: var(--text); }
  ul { list-style: none; margin: 0; padding: 0; overflow: auto; flex: 1; border-top: 1px solid var(--line); }
  .empty { padding: 24px 16px; color: var(--muted); font-size: 14px; }
  .item { display: flex; gap: 12px; width: 100%; text-align: left; padding: 14px 16px; border: 0; border-bottom: 1px solid var(--line); background: none; cursor: pointer; }
  .item:hover { background: color-mix(in srgb, var(--text) 4%, transparent); }
  .item.sel { background: color-mix(in srgb, var(--accent) 14%, transparent); }
  img.av { object-fit: cover; }
  .account { display: flex; align-items: center; gap: 8px; padding: 0 16px 12px; font-size: 13px; }
  .account .av { width: 22px; height: 22px; font-size: 11px; } .me { font-weight: 600; }
  .account .link { margin-left: auto; background: none; border: 0; color: var(--accent); cursor: pointer; font-size: 12.5px; }
  .wait { color: var(--muted); line-height: 1.5; } .wait b { color: var(--text); letter-spacing: .06em; } .wait a { color: var(--accent); }
  .discord { width: 100%; padding: 8px 14px; border: 0; border-radius: 10px; background: #5865f2; color: #fff; font-size: 13.5px; cursor: pointer; }
  .gate { align-items: center; justify-content: space-between; color: var(--muted); font-size: 14px; }
  .reacts button:disabled { cursor: default; }
  .av { flex: none; width: 34px; height: 34px; border-radius: 50%; display: grid; place-items: center; color: #fff; font-weight: 600; font-size: 14px; }
  .meta { display: grid; gap: 3px; min-width: 0; }
  .title { font-weight: 600; font-size: 14px; line-height: 1.3; }
  .preview { font-size: 12.5px; color: var(--muted); overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; }
  .foot { display: flex; gap: 8px; flex-wrap: wrap; font-size: 11.5px; color: var(--muted); }
  .foot b { font-weight: 600; color: var(--text); } .foot b.ok, .chips .ok { color: #30d158; }
  .pane { display: flex; flex-direction: column; min-width: 0; min-height: 0; }
  .notice { padding: 9px 20px; font-size: 12.5px; color: var(--muted); border-bottom: 1px solid var(--line); background: color-mix(in srgb, #ff9f0a 10%, transparent); }
  .scroll { flex: 1; overflow: auto; padding: 36px 40px; }
  .thread { max-width: 720px; display: grid; gap: 14px; }
  .thread h1 { font-size: 26px; letter-spacing: -.015em; margin: 0 0 8px; line-height: 1.2; }
  .chips { display: flex; gap: 8px; font-size: 12px; color: var(--muted); }
  article { display: flex; gap: 14px; padding: 16px 0; border-top: 1px solid var(--line); }
  .content { min-width: 0; flex: 1; }
  .who { display: flex; align-items: baseline; gap: 8px; margin-bottom: 6px; font-size: 14px; }
  .role { font-size: 11px; padding: 1px 7px; border-radius: 99px; background: color-mix(in srgb, var(--accent) 18%, transparent); color: var(--accent); }
  time { font-size: 12px; color: var(--muted); }
  .reacts { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 12px; align-items: center; }
  .reacts button { padding: 3px 10px; border-radius: 99px; border: 1px solid var(--line); background: none; font-size: 13px; cursor: pointer; }
  .reacts button.mine { border-color: var(--accent); background: color-mix(in srgb, var(--accent) 14%, transparent); }
  .add { display: flex; gap: 4px; opacity: 0; transition: opacity .15s; }
  article:hover .add, .add:focus-within { opacity: 1; }
  .composer { display: flex; gap: 10px; padding: 14px 40px 22px; border-top: 1px solid var(--line); align-items: flex-end; }
  .composer textarea { flex: 1; resize: none; padding: 10px 12px; border-radius: 12px; border: 1px solid var(--line); background: var(--panel); color: var(--text); font: inherit; font-size: 14px; }
  textarea.field { resize: vertical; font-family: inherit; }
  .actions { display: flex; justify-content: flex-end; gap: 10px; }
  .err { color: #ff453a; font-size: 13px; margin: 0; } .pad { padding: 0 40px 12px; }
  .blank { margin: auto; text-align: center; } .blank h2 { margin: 0 0 4px; font-size: 22px; } .blank p { margin: 0; color: var(--muted); }
  button { font: inherit; }
  .more { position: relative; margin-left: auto; }
  .dots { background: none; border: 0; cursor: pointer; font-size: 18px; line-height: 1; padding: 0 6px; color: var(--muted); opacity: 0; }
  article:hover .dots, .dots:focus-visible { opacity: 1; }
  .menu { position: absolute; right: 0; top: 24px; z-index: 5; min-width: 190px; display: grid; padding: 6px; border-radius: 12px; border: 1px solid var(--line); background: var(--panel); box-shadow: 0 10px 30px rgba(0,0,0,.25); }
  .menu button { text-align: left; padding: 7px 10px; border: 0; border-radius: 8px; background: none; color: var(--text); font-size: 13.5px; cursor: pointer; }
  .menu button:hover { background: color-mix(in srgb, var(--text) 8%, transparent); }
  .menu .danger { color: #ff453a; }
  .star { color: #ff9f0a; }
  .chips .link { margin-left: auto; background: none; border: 0; color: var(--accent); cursor: pointer; font-size: 12.5px; }
  .replying { display: flex; gap: 8px; padding: 8px 40px 0; font-size: 12.5px; color: var(--muted); border-top: 1px solid var(--line); }
  .replying .link { margin-left: auto; background: none; border: 0; color: var(--accent); cursor: pointer; }
  .toast { position: fixed; bottom: 22px; left: 50%; transform: translateX(-50%); padding: 8px 16px; border-radius: 99px; background: var(--text); color: var(--bg); font-size: 13px; z-index: 20; }
  .primary, .ghost { padding: 8px 18px; border-radius: 99px; border: 0; font-size: 13.5px; cursor: pointer; }
  .primary { background: var(--accent); color: #fff; } .ghost { background: color-mix(in srgb, var(--text) 8%, transparent); color: var(--text); }
  .primary:disabled { opacity: .35; cursor: default; }
</style>
