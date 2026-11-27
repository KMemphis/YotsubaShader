#!/usr/bin/env node
// Builds the Hub's shader gallery data from the MCP examples (Web/mcp/data/examples.json).
//   node scripts/hub/gen_shaders.mjs [thumbsDir]
// Writes Apps/YotsubaShader.Hub/ui/public/shaders/catalog.json and, when a folder of <id>.webp thumbnails is given,
// copies them next to it. The thumbnails are renders of each example in the playground (see scripts/hub/README.md).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const src = path.join(root, 'Web/mcp/data/examples.json');
const out = path.join(root, 'Apps/YotsubaShader.Hub/ui/public/shaders');
const thumbsIn = process.argv[2];

const examples = JSON.parse(fs.readFileSync(src, 'utf8'));
fs.mkdirSync(path.join(out, 'thumbs'), { recursive: true });

const catalog = examples.map((e) => ({
  id: e.id, style: e.style, label: e.label, title: e.title, description: e.description, tags: e.tags, code: e.code,
}));
fs.writeFileSync(path.join(out, 'catalog.json'), JSON.stringify(catalog));

let copied = 0;
if (thumbsIn) for (const f of fs.readdirSync(thumbsIn)) if (f.endsWith('.webp')) { fs.copyFileSync(path.join(thumbsIn, f), path.join(out, 'thumbs', f)); copied++; }
const missing = examples.filter((e) => !fs.existsSync(path.join(out, 'thumbs', `${e.id}.webp`))).map((e) => e.id);
console.log(`${catalog.length} shaders, ${copied} thumbnails copied${missing.length ? `, missing: ${missing.join(', ')}` : ''}`);
