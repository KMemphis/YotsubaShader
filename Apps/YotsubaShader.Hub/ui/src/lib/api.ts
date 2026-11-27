export type Component = {
  id: string; name: string; description: string; kind: string;
  group: string | null; locked: boolean; default: boolean;
};
export type Env = { dotnetPath: string | null; sdks: string[]; cliVersion: string | null; defaultHome: string; os: string; cliPath: string };
export type InstallEvent = { type: 'step-start' | 'step-done' | 'step-fail' | 'log' | 'done'; step: string | null; message: string | null };

const token = new URLSearchParams(location.search).get('t') ?? '';
const url = (path: string) => `/api/${path}${path.includes('?') ? '&' : '?'}t=${token}`;

export const getState = async (): Promise<{ components: Component[]; env: Env }> => (await fetch(url('state'))).json();
export type InstalledPlugin = { id: string; version: string | null };
export const getInstalledPlugins = async (): Promise<InstalledPlugin[]> => (await fetch(url('plugins'))).json();
export const getLicense = async (): Promise<string> => (await fetch(url('license'))).text();

export async function startInstall(ids: string[], home: string): Promise<string | null> {
  const r = await fetch(url('install'), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ids, home }) });
  return r.ok ? null : await r.text();
}

export function watchInstall(onEvent: (e: InstallEvent) => void): () => void {
  const es = new EventSource(url('install/events'));
  es.onmessage = (m) => {
    const e: InstallEvent = JSON.parse(m.data);
    onEvent(e);
    if (e.type === 'done') es.close();
  };
  es.onerror = () => es.close();
  return () => es.close();
}

// ---- Forum ----
export type Author = { id: string; name: string; role: string; color: string; avatarUrl: string | null };
export type Reaction = { emoji: string; count: number; mine: boolean };
export type Message = { id: string; author: Author; body: string; at: string; reactions: Reaction[]; edited: boolean; pinned: boolean; url: string | null };
export type ThreadSummary = {
  id: string; title: string; tag: string; pinned: boolean; solved: boolean;
  author: Author; replies: number; lastActivity: string; preview: string;
};
export type Thread = { id: string; title: string; tag: string; pinned: boolean; solved: boolean; messages: Message[] };
export type ForumInfo = {
  channel: string; server: string; members: number; online: number; live: boolean; canReact: boolean; notice: string; tags: string[];
};
export type ForumSession = {
  mode: 'local' | 'remote'; signedIn: boolean; user: Author | null; signingIn: boolean; signInUrl: string | null; signInCode: string | null;
};

async function call<T>(path: string, body?: unknown, method = 'POST'): Promise<T> {
  const r = await fetch(url(path), body === undefined && method === 'POST' ? undefined
    : { method, headers: { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
  if (!r.ok) throw new Error((await r.text()) || r.statusText);
  return r.status === 204 ? (undefined as T) : r.json();
}

export const forum = {
  info: () => call<ForumInfo>('forum/info'),
  session: () => call<ForumSession>('forum/session'),
  signIn: () => post('forum/signin'),
  signOut: () => post('forum/signout'),
  list: (tag: string, q: string) => call<ThreadSummary[]>(`forum/threads?tag=${encodeURIComponent(tag)}&q=${encodeURIComponent(q)}`),
  get: (id: string) => call<Thread>(`forum/threads/${id}`),
  create: (title: string, tag: string, body: string) => call<Thread>('forum/threads', { title, tag, body }),
  reply: (id: string, body: string) => call<Message>(`forum/threads/${id}/messages`, { body }),
  react: (id: string, mid: string, emoji: string) => call<Message>(`forum/threads/${id}/messages/${mid}/react`, { emoji }),
  edit: (id: string, mid: string, body: string) => call<Message>(`forum/threads/${id}/messages/${mid}`, { body }, 'PATCH'),
  remove: (id: string, mid: string) => call<void>(`forum/threads/${id}/messages/${mid}`, undefined, 'DELETE'),
  pin: (id: string, mid: string, pinned: boolean) => call<Message>(`forum/threads/${id}/messages/${mid}/pin`, { pinned }),
  solve: (id: string, solved: boolean) => call<Thread>(`forum/threads/${id}/solve`, { solved }),
};

// ---- Updates ----
export type Release = { tag: string; version: string; name: string; notes: string; publishedAt: string; url: string; prerelease: boolean };
export type UpdateStatus = {
  hubVersion: string; latestVersion: string | null; hubUpdateAvailable: boolean;
  compilerVersion: string | null; compilerUpdateAvailable: boolean;
  canSelfUpdate: boolean; checkedAt: string | null; error: string | null; releases: Release[];
};

async function post(path: string, body?: unknown): Promise<void> {
  const r = await fetch(url(path), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  if (!r.ok) throw new Error((await r.text()) || r.statusText);
}

export const updatesApi = {
  status: () => call<UpdateStatus>('updates'),
  check: () => call<UpdateStatus>('updates/check', {}),
  apply: () => post('updates/apply'),
  download: () => call<{ path: string }>('updates/download', {}),
  open: (u: string) => post('open', { url: u }),
};

// ---- Shader gallery ----
export type CompileResult = { ok: boolean; wgsl: string | null; log: string };
export const compileShader = (code: string) => call<CompileResult>('shader/compile', { code });
