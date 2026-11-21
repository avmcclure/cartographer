// Selection and panning for a plan. Everything here is an addition: the page reads without it.
import { standsFor, reach, marks } from '../lib/reach.js';
import { STEP, MARGIN, px, pathOf, arrowOf } from '../lib/draw.js';
import { carry, current } from './carry.js';

const data = JSON.parse(document.getElementById('sheet-data').textContent);
const base = document.body.dataset.base;
const view = document.querySelector('.plan-view');
const svg = view.querySelector('svg.plan');
const statement = document.getElementById('statement');
const layer = svg?.querySelector('.selection');
let { sel, down } = current();
let graph;
const loadGraph = () => (graph ??= fetch(base + 'graph.json').then((r) => r.json()));

const NS = 'http://www.w3.org/2000/svg';
const el = (name, attrs, text) => {
  const node = document.createElementNS(NS, name);
  for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, v);
  if (text) node.textContent = text;
  return node;
};
const html = (tag, props, ...children) => {
  const node = Object.assign(document.createElement(tag), props);
  node.append(...children);
  return node;
};
const count = (n, one, many = one + 's') => `${n} ${n === 1 ? one : many}`;
const byPlot = (id) => document.querySelectorAll(`[data-plot="${CSS.escape(id)}"]`);

function clear() {
  for (const node of document.querySelectorAll('.is-origin, .is-reached, .is-on')) node.classList.remove('is-origin', 'is-reached', 'is-on');
  for (const cell of document.querySelectorAll('[data-selcell]')) cell.textContent = '';
  layer?.replaceChildren();
}

function drawRoutes(routes) {
  for (const r of routes) {
    const g = el('g', { class: `line is-on b-${r.basis}` });
    g.append(el('path', { class: 'stroke', d: pathOf(r.points) }), el('path', { class: 'head', d: arrowOf(r.points) }));
    layer.append(g);
  }
  for (const r of routes.filter((r) => r.label)) {
    const [x, y, w, h] = r.label.map(px);
    const g = el('g', { class: `label is-on b-${r.basis}` });
    g.append(el('rect', { x: x + 1, y: y + 2, width: w - 2, height: h - 4 }), el('text', { x: x + w / 2, y: y + h / 2 + 4, 'text-anchor': 'middle' }, r.text));
    layer.append(g);
  }
}

async function apply(scrollTo) {
  clear();
  const url = new URL(location.href);
  sel ? url.searchParams.set('sel', sel) : url.searchParams.delete('sel');
  sel && down ? url.searchParams.set('down', '1') : url.searchParams.delete('down');
  history.replaceState(null, '', url);
  carry({ sel, down });
  if (!sel) {
    statement.replaceChildren(html('p', { className: 'statement-text' }, 'Nothing is selected. Select a plot on the plan, or a row of the schedule, to see what it uses and what uses it.'));
    return;
  }
  const g = await loadGraph();
  const known = g.entries[sel] ?? (g.groups[sel] && [data.plots[sel]?.name ?? sel.split('/').pop(), 'group of parts']);
  if (!known) {
    statement.replaceChildren(html('p', { className: 'statement-text' }, `The selection "${sel}" is not in this atlas. `, button('Clear selection', () => select(null))));
    return;
  }
  const origin = standsFor(g, sel);
  const r = reach(g, origin, down);
  const m = marks(data, r);

  // A part's own links are drawn in full where routes were prepared for it; summarised lines then light up only for what those do not show.
  const routes = !down && layer ? data.routes[sel] : null;
  const routed = new Set((routes ?? []).map((x) => x.from + '>' + x.to));
  const first = (id) => data.where[id]?.[0];
  if (routes) drawRoutes(routes);
  for (const line of data.lines) {
    const mine = line.pairs.filter(([f, t]) => r.pairs.has(f + '>' + t));
    if (mine.some(([f, t]) => !routed.has(first(f) + '>' + first(t))))
      for (const node of document.querySelectorAll(`[data-line="${CSS.escape(line.id)}"]`)) node.classList.add('is-on');
  }
  for (const id of m.origin) {
    for (const node of byPlot(id)) node.classList.add('is-origin');
    for (const cell of document.querySelectorAll(`tr[data-plot="${CSS.escape(id)}"] [data-selcell]`)) cell.textContent = 'Selected';
  }
  const reachedNames = [];
  for (const [id, entries] of m.reached) {
    for (const node of byPlot(id)) node.classList.add('is-reached');
    const n = (set) => [...entries].filter((e) => set.has(e)).length;
    const [uses, usedBy, further] = [n(r.uses), n(r.usedBy), n(r.further)];
    const single = entries.size === 1 && data.plots[id]?.entry === [...entries][0];
    const words = [
      uses && (single ? 'the selection uses it' : `the selection uses ${count(uses, 'part')}`),
      usedBy && (single ? 'uses the selection' : `${count(usedBy, 'part')} use${usedBy === 1 ? 's' : ''} the selection`),
      further && (single ? 'affected further downstream' : `${count(further, 'part')} affected further downstream`),
    ].filter(Boolean).join('; ');
    for (const cell of document.querySelectorAll(`tr[data-plot="${CSS.escape(id)}"] [data-selcell]`)) cell.textContent = words;
    reachedNames.push(`${data.plots[id].number} ${data.plots[id].name}${single ? '' : ` (${entries.size})`}`);
  }

  const all = new Set([...r.uses, ...r.usedBy, ...r.further]);
  const here = [...all].filter((id) => data.where[id]).length;
  const text = html('p', { className: 'statement-text' });
  text.append(html('strong', {}, known[0]), ` (${known[1]}${origin.size > 1 ? `, ${count(origin.size, 'entry', 'entries')}` : ''}) is selected. `);
  text.append(`It uses ${count(r.uses.size, 'entry', 'entries')}, and ${count(r.usedBy.size, 'entry', 'entries')} use${r.usedBy.size === 1 ? 's' : ''} it, one step away. `);
  if (down) text.append(r.further.size ? `Following what uses it, ${count(r.further.size, 'more entry', 'more entries')} could be affected by a change to it: ${r.usedBy.size + r.further.size} downstream in all. ` : 'Nothing further downstream uses those. ');
  text.append(all.size ? `${here} of these ${all.size} ${all.size === 1 ? 'is' : 'are'} on this sheet` : 'Nothing in the atlas links to or from it');
  const beside = [...all].filter((id) => (data.where[id] ?? []).some((p) => m.origin.has(p))).length;
  const listed = [...reachedNames.slice(0, 12), ...(reachedNames.length > 12 ? [`${reachedNames.length - 12} more plots`] : []), ...(beside ? [`${beside} inside the selected plot`] : [])];
  text.append(listed.length ? `: ${listed.join(', ')}.` : '.');
  if (!m.origin.size) text.append(' The selection itself is not drawn on this sheet.');
  if (routes) text.append(` Its own ${count(routes.length, 'line is', 'lines are')} drawn in full.`);
  const plot = data.plots[sel];
  const controls = html('p', { className: 'statement-controls' },
    button(down ? 'Show one step only' : 'Show everything downstream', () => { down = !down; apply(); }, down),
    ...(g.entries[sel] ? [html('a', { href: `${base}entry/${sel}/`, textContent: 'Open its entry' })] : []),
    ...(plot?.opens ? [html('a', { href: plot.opens, textContent: 'Open its sheet' })] : []),
    button('Clear selection', () => select(null)));
  statement.replaceChildren(text, controls);
  carry({ sel, down });
  // Bring the selection into the view by panning the plan only; the page itself stays where it is.
  const at = scrollTo && svg?.querySelector('.plot.is-origin .outline');
  if (at) {
    const [a, b] = [at.getBoundingClientRect(), view.getBoundingClientRect()];
    view.scrollBy(a.left + a.width / 2 - b.left - b.width / 2, a.top + a.height / 2 - b.top - b.height / 2);
  }
}

function button(text, action, pressed) {
  const b = html('button', { type: 'button', textContent: text });
  if (pressed !== undefined) b.setAttribute('aria-pressed', String(pressed));
  b.addEventListener('click', action);
  return b;
}

function select(id) {
  sel = id && id !== sel ? id : null;
  down = false;
  apply();
}

// Plots and schedule rows make the same selection. A click that ends a drag is not a selection.
let dragged = false;
document.addEventListener('click', (e) => {
  const target = e.target.closest('[data-select]');
  if (!target || e.metaKey || e.ctrlKey || e.shiftKey || e.button !== 0) return;
  e.preventDefault();
  if (dragged) return;
  sel = target.dataset.select !== sel ? target.dataset.select : null;
  down = false;
  apply(Boolean(target.closest('table')));   // a schedule row may be far from its plot: pan to it
});

// Panning: the view scrolls natively; dragging the sheet scrolls it too.
let start = null;
view.addEventListener('pointerdown', (e) => {
  if (e.button === 0 && e.pointerType === 'mouse') start = { x: e.clientX, y: e.clientY, left: view.scrollLeft, top: view.scrollTop };
  dragged = false;
});
window.addEventListener('pointermove', (e) => {
  if (!start) return;
  const [dx, dy] = [e.clientX - start.x, e.clientY - start.y];
  if (!dragged && Math.hypot(dx, dy) < 5) return;
  dragged = true;
  view.classList.add('dragging');
  view.scrollTo(start.left - dx, start.top - dy);
});
window.addEventListener('pointerup', () => { start = null; view.classList.remove('dragging'); });
view.addEventListener('dragstart', (e) => e.preventDefault());

// The locator shows which part of the sheet is in view; clicking it moves the view there.
const locator = document.querySelector('.plan-locator svg');
if (locator && svg) {
  const win = locator.querySelector('.window');
  const [sw, sh] = locator.dataset.steps.split(' ').map(Number);
  const update = () => {
    const x = Math.max(0, (view.scrollLeft - MARGIN.left) / STEP), y = Math.max(0, (view.scrollTop - MARGIN.top) / STEP);
    const fits = view.clientWidth >= view.scrollWidth && view.clientHeight >= view.scrollHeight;
    locator.parentElement.hidden = fits;
    win.setAttribute('x', x); win.setAttribute('y', y);
    win.setAttribute('width', Math.min(sw - x, view.clientWidth / STEP)); win.setAttribute('height', Math.min(sh - y, view.clientHeight / STEP));
  };
  view.addEventListener('scroll', update, { passive: true });
  window.addEventListener('resize', update);
  update();
  const go = (e) => {
    const box = locator.getBoundingClientRect();
    view.scrollTo(((e.clientX - box.left) / box.width) * sw * STEP + MARGIN.left - view.clientWidth / 2, ((e.clientY - box.top) / box.height) * sh * STEP + MARGIN.top - view.clientHeight / 2);
  };
  locator.addEventListener('pointerdown', (e) => { locator.setPointerCapture(e.pointerId); go(e); });
  locator.addEventListener('pointermove', (e) => { if (e.buttons) go(e); });
}

document.documentElement.classList.add('js');
apply(true);
