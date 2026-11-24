// The search box on every page: finds entries by name, kind or ID from everything.json, fetched on first use.
const base = document.body.dataset.base;
const input = document.getElementById('find');
const list = document.getElementById('found-list');
let all = null, hits = [], cursor = 0;

// One fetch, shared by every keystroke that arrives before it finishes.
const load = () => (all ??= fetch(base + 'everything.json').then((r) => r.json()).catch(() => []));

function show() {
  list.hidden = hits.length === 0;
  input.setAttribute('aria-expanded', String(hits.length > 0));
  list.replaceChildren(...hits.map((e, i) => {
    const a = document.createElement('a');
    a.href = `${base}entry/${e.id}/`;
    a.setAttribute('role', 'option');
    a.className = i === cursor ? 'on' : '';
    const name = document.createElement('span');
    name.textContent = e.name;
    const kind = document.createElement('span');
    kind.className = 'kind';
    kind.textContent = e.type && e.type !== e.kind ? `${e.kind} · ${e.type}` : e.kind;
    a.append(name, kind);
    return a;
  }));
}

input.addEventListener('focus', load, { once: true });
input.addEventListener('input', async () => {
  const typed = input.value;
  const words = typed.toLowerCase().split(/\s+/).filter(Boolean);
  const entries = await load();
  if (input.value !== typed) return;   // a later keystroke has its own answer
  hits = words.length ? entries.filter((e) => words.every((w) => `${e.name} ${e.kind} ${e.type ?? ''} ${e.id}`.toLowerCase().includes(w)))
    .sort((a, b) => Number(!a.name.toLowerCase().startsWith(words[0])) - Number(!b.name.toLowerCase().startsWith(words[0])) || a.name.localeCompare(b.name)).slice(0, 12) : [];
  cursor = 0;
  show();
});
input.addEventListener('keydown', (e) => {
  if (e.key === 'ArrowDown') { cursor = Math.min(cursor + 1, hits.length - 1); show(); e.preventDefault(); }
  if (e.key === 'ArrowUp') { cursor = Math.max(cursor - 1, 0); show(); e.preventDefault(); }
  if (e.key === 'Enter' && hits[cursor]) location.href = `${base}entry/${hits[cursor].id}/`;
  if (e.key === 'Escape') { input.value = ''; hits = []; show(); }
});
document.addEventListener('click', (e) => { if (!e.target.closest('.search')) { hits = []; show(); } });
