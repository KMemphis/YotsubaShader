export type Inline = { text: string; code: boolean; bold?: boolean; italic?: boolean; strike?: boolean; underline?: boolean };
export type Para = { spans: Inline[]; quote: boolean };
export type Block = { kind: 'code'; lang: string; text: string } | { kind: 'text'; paragraphs: Para[] };

/** Splits a message into fenced code blocks, quotes and paragraphs with Discord's inline styles. Never produces HTML. */
export function parseBody(body: string): Block[] {
  const blocks: Block[] = [];
  const fence = /```(\w*)\n([\s\S]*?)```/g;
  let last = 0;
  for (let m: RegExpExecArray | null; (m = fence.exec(body)); ) {
    if (m.index > last) pushText(blocks, body.slice(last, m.index));
    blocks.push({ kind: 'code', lang: m[1], text: m[2].replace(/\n$/, '') });
    last = fence.lastIndex;
  }
  if (last < body.length) pushText(blocks, body.slice(last));
  return blocks;
}

function pushText(blocks: Block[], text: string) {
  const paragraphs = text.split(/\n{2,}/).map((p) => p.trim()).filter(Boolean).map((p): Para => {
    const lines = p.split('\n');
    const quote = lines.every((l) => /^>\s?/.test(l));
    return { quote, spans: inline(quote ? lines.map((l) => l.replace(/^>\s?/, '')).join('\n') : p) };
  });
  if (paragraphs.length) blocks.push({ kind: 'text', paragraphs });
}

function inline(p: string): Inline[] {
  return p.split(/(`[^`]+`|\*\*[^*]+\*\*|__[^_]+__|~~[^~]+~~|\*[^*\n]+\*|_[^_\n]+_)/).filter(Boolean).map((s): Inline => {
    if (s.length > 2 && s.startsWith('`') && s.endsWith('`')) return { text: s.slice(1, -1), code: true };
    if (s.length > 4 && s.startsWith('**') && s.endsWith('**')) return { text: s.slice(2, -2), code: false, bold: true };
    if (s.length > 4 && s.startsWith('__') && s.endsWith('__')) return { text: s.slice(2, -2), code: false, underline: true };
    if (s.length > 4 && s.startsWith('~~') && s.endsWith('~~')) return { text: s.slice(2, -2), code: false, strike: true };
    if (s.length > 2 && (s[0] === '*' || s[0] === '_') && s[s.length - 1] === s[0]) return { text: s.slice(1, -1), code: false, italic: true };
    return { text: s, code: false };
  });
}

const rtf = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });
export function ago(iso: string): string {
  const s = (new Date(iso).getTime() - Date.now()) / 1000;
  const units: [Intl.RelativeTimeFormatUnit, number][] = [['day', 86400], ['hour', 3600], ['minute', 60]];
  for (const [u, n] of units) if (Math.abs(s) >= n) return rtf.format(Math.round(s / n), u);
  return 'just now';
}

export const initials = (name: string) => name.trim().slice(0, 1).toUpperCase();

// ---- Release notes (the Markdown subset GitHub release notes actually use) ----
export type Span = { text: string; kind: 'text' | 'code' | 'bold' };
export type Note =
  | { kind: 'heading'; spans: Span[] }
  | { kind: 'list'; items: Span[][] }
  | { kind: 'para'; spans: Span[] }
  | { kind: 'code'; text: string };

function spans(line: string): Span[] {
  const clean = line
    .replace(/\s+by @\S+ in https:\/\/github\.com\/[^\s]+\/pull\/(\d+)/g, ' (#$1)') // "by @x in <url>" → "(#75)"
    .replace(/\[([^\]]+)\]\([^)]+\)/g, '$1'); // links keep their text only
  return clean.split(/(`[^`]+`|\*\*[^*]+\*\*)/).filter(Boolean).map((s): Span =>
    s.startsWith('`') && s.length > 2 ? { text: s.slice(1, -1), kind: 'code' }
    : s.startsWith('**') && s.length > 4 ? { text: s.slice(2, -2), kind: 'bold' }
    : { text: s, kind: 'text' });
}

export function parseNotes(md: string): Note[] {
  const out: Note[] = [];
  const lines = md.replace(/\r/g, '').split('\n');
  let para: string[] = [];
  let list: Span[][] | null = null;
  const flush = () => {
    if (para.length) out.push({ kind: 'para', spans: spans(para.join(' ')) });
    if (list) out.push({ kind: 'list', items: list });
    para = []; list = null;
  };
  for (let i = 0; i < lines.length; i++) {
    const l = lines[i];
    if (l.trimStart().startsWith('```')) {
      flush();
      const code: string[] = [];
      for (i++; i < lines.length && !lines[i].trimStart().startsWith('```'); i++) code.push(lines[i]);
      out.push({ kind: 'code', text: code.join('\n') });
    } else if (/^\*\*Full Changelog\*\*/.test(l.trim())) {
      continue; // a bare compare URL adds nothing in the app
    } else if (/^#{1,6}\s/.test(l)) {
      flush(); out.push({ kind: 'heading', spans: spans(l.replace(/^#{1,6}\s+/, '')) });
    } else if (/^\s*[-*]\s+/.test(l)) {
      if (para.length) { out.push({ kind: 'para', spans: spans(para.join(' ')) }); para = []; }
      (list ??= []).push(spans(l.replace(/^\s*[-*]\s+/, '')));
    } else if (!l.trim()) {
      flush();
    } else {
      if (list) { out.push({ kind: 'list', items: list }); list = null; }
      para.push(l.trim());
    }
  }
  flush();
  return out;
}

export function shortDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}
