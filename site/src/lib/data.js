// Reads what `cartographer site prepare` wrote. The site reads nothing else.
import fs from 'node:fs';
import path from 'node:path';
import { marked } from 'marked';

const root = path.resolve(process.env.CARTOGRAPHER_DATA ?? 'data');
if (!fs.existsSync(path.join(root, 'site.json')))
  throw new Error(`No site data at ${root}. Run: cartographer site prepare --atlas <atlas> --out ${root}`);

const cache = new Map();
export function read(name) {
  if (!cache.has(name)) cache.set(name, JSON.parse(fs.readFileSync(path.join(root, name), 'utf8')));
  return cache.get(name);
}

export const site = read('site.json');
export const gazetteer = read('gazetteer.json');
export const graph = read('graph.json');
export const questions = read('questions.json');
export const changes = read('changes.json');
export const tags = read('tags.json');
export const sheet = (id) => read(`sheets/${id}.json`);
export const entry = (id) => read(`entries/${id}.json`);

const base = import.meta.env.BASE_URL.replace(/\/$/, '');
export const href = (p) => `${base}${p}`;
export const sheetHref = (id) => (id === 'organisation' ? href('/') : href(`/sheet/${id}/`));
export const entryHref = (id) => href(`/entry/${id}/`);

export const sheetNumber = (id) => site.sheets.findIndex((s) => s.id === id) + 1;

const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
export function date(iso) {
  const [y, m, d] = (iso ?? '').split('-').map(Number);
  return y ? `${d} ${months[m - 1]} ${y}` : 'never';
}

export function kindLabel(e) {
  if (e.kind === 'external') return 'external system';
  return e.type && e.type !== e.kind ? `${e.kind} · ${e.type}` : e.kind;
}

// Atlas text is data: any HTML in it is shown as text, never run.
const escape = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
marked.use({ renderer: { html: ({ text }) => escape(text) } });

// Passages the CLI wrote and passages a person owns are marked in entry bodies with comments; show whose they are.
const owners = { extracted: 'Extracted by the scan', person: 'Written by a person' };
export function markdown(text) {
  const marked_ = text.replace(/<!-- (\/?)(extracted|person) -->/g, (_, close, who) => `\n\n@@${close ? 'end' : who}@@\n\n`);
  return marked
    .parse(marked_)
    .replace(/<p>@@(extracted|person)@@<\/p>/g, (_, who) => `<div class="passage ${who}"><p class="passage-by">${owners[who]}</p>`)
    .replace(/<p>@@end@@<\/p>/g, '</div>');
}
