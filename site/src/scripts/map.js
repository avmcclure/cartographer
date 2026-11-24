// Draws the map of clusters and the inside of one cluster. Layout is a force simulation run to rest before
// anything is shown, from data in the order prepare wrote it, so the same atlas draws the same picture.
import { forceSimulation, forceLink, forceManyBody, forceCollide, forceX, forceY, forceRadial } from 'd3-force';
import { select } from 'd3-selection';
import { zoom, zoomIdentity } from 'd3-zoom';

const SVG = 'http://www.w3.org/2000/svg';
const color = (i) => `var(--c${i % 8})`;
const width = (s, px) => s.length * px * 0.56;

function el(name, attrs = {}, parent) {
  const e = document.createElementNS(SVG, name);
  for (const [k, v] of Object.entries(attrs)) e.setAttribute(k, v);
  parent?.append(e);
  return e;
}

function clipped(parent, attrs, text, max, px) {
  const t = el('text', attrs, parent);
  let s = text;
  if (width(s, px) > max) {
    while (s.length > 3 && width(s + '…', px) > max) s = s.slice(0, -1);
    s += '…';
    el('title', {}, t).textContent = text;
  }
  t.append(s);
  return t;
}

function stage(svg, minScale = 0.8) {
  svg.replaceChildren();
  el('defs', {}, svg).innerHTML =
    '<marker id="arr" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path class="arrow" d="M0 0 L10 5 L0 10 z"/></marker>' +
    '<marker id="arr-on" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path class="arrow-on" d="M0 0 L10 5 L0 10 z"/></marker>';
  const layer = el('g', {}, svg);
  const z = zoom().scaleExtent([0.25, 3]).on('zoom', (e) => layer.setAttribute('transform', e.transform));
  select(svg).call(z).on('dblclick.zoom', null);
  const fit = (box) => {
    const w = svg.clientWidth, h = svg.clientHeight, pad = 40;
    // Never shrink text below about 10px: a big drawing is panned, not squeezed.
    const k = Math.max(minScale, Math.min(1.3, (w - 2 * pad) / Math.max(box.w, 1), (h - 2 * pad - 40) / Math.max(box.h, 1)));
    select(svg).call(z.transform, zoomIdentity.translate(w / 2 - k * (box.x + box.w / 2), h / 2 + 10 - k * (box.y + box.h / 2)).scale(k));
  };
  return { layer, fit };
}

function bounds(items) {
  if (items.length === 0) return { x: 0, y: 0, w: 0, h: 0 };
  const xs = items.flatMap((d) => [d.x - d.w / 2, d.x + d.w / 2]), ys = items.flatMap((d) => [d.y - d.h / 2, d.y + d.h / 2]);
  return { x: Math.min(...xs), y: Math.min(...ys), w: Math.max(...xs) - Math.min(...xs), h: Math.max(...ys) - Math.min(...ys) };
}

function activate(g, label, go) {
  g.setAttribute('tabindex', '0');
  g.setAttribute('role', 'link');
  g.setAttribute('aria-label', label);
  g.addEventListener('click', go);
  g.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); go(); } });
}

/** Clusters as cards, joined by one line per pair with its link count. */
export function drawMap(svg, data) {
  const { layer, fit } = stage(svg, 0.6);
  const line = 18, W = 230, shown = 6;
  const cards = data.clusters.map((c) => ({ c, w: W, h: 62 + Math.min(c.members.length, shown) * line + (c.members.length > shown ? line : 0) }));
  const at = new Map(cards.map((d) => [d.c.key, d]));
  const sim = forceSimulation(cards)
    .force('link', forceLink(data.between.map((e) => ({ source: at.get(e.a), target: at.get(e.b), count: e.count }))).distance(300).strength((e) => Math.min(1, 0.15 + e.count * 0.05)))
    .force('charge', forceManyBody().strength(-900))
    .force('collide', forceCollide((d) => Math.hypot(d.w, d.h) / 2 + 24))
    .force('x', forceX(0).strength(0.04)).force('y', forceY(0).strength(0.07)).stop();
  for (let i = 0; i < 400; i++) sim.tick();

  const most = Math.max(1, ...data.between.map((e) => e.count));
  const edges = el('g', {}, layer);
  for (const e of data.between) {
    const [a, b] = [at.get(e.a), at.get(e.b)];
    el('line', { class: 'edge', x1: a.x, y1: a.y, x2: b.x, y2: b.y, 'stroke-width': 1 + (e.count / most) * 9 }, edges);
    const t = el('g', { class: 'count', transform: `translate(${(a.x + b.x) / 2},${(a.y + b.y) / 2})` }, edges);
    const text = `${e.count} link${e.count === 1 ? '' : 's'}`;
    el('rect', { x: -width(text, 11) / 2 - 7, y: -10, width: width(text, 11) + 14, height: 20, rx: 10 }, t);
    el('text', { 'text-anchor': 'middle', y: 4 }, t).textContent = text;
  }
  for (const d of cards) {
    const c = d.c;
    const g = el('g', { class: 'card', transform: `translate(${d.x - d.w / 2},${d.y - d.h / 2})` }, layer);
    activate(g, `${c.name}, ${c.members.length} parts. Open`, () => (location.href = c.href));
    el('rect', { class: 'body', width: d.w, height: d.h, rx: 8 }, g);
    el('rect', { width: d.w, height: 5, rx: 2, fill: color(c.color) }, g);
    clipped(g, { class: 'title', x: 14, y: 28 }, c.name, d.w - 28, 14);
    el('text', { class: 'meta', x: 14, y: 46 }, g).textContent = `${c.members.length} part${c.members.length === 1 ? '' : 's'} · ${c.inside} links inside${c.pinned ? ' · pinned' : ''}`;
    c.members.slice(0, shown).forEach((m, i) => {
      el('circle', { cx: 18, cy: 64 + i * line, r: 3.5, fill: color(c.color) }, g);
      clipped(g, { class: 'member', x: 28, y: 68 + i * line }, m.name, d.w - 44, 12);
    });
    if (c.members.length > shown) el('text', { class: 'meta', x: 28, y: 68 + shown * line }, g).textContent = `and ${c.members.length - shown} more`;
  }
  fit(bounds(cards));
}

/**
 * One cluster's parts, and the parts in other clusters they link to, set round the outside.
 * Returns a function that picks out one part and what it links to (or clears with null).
 */
export function drawCluster(svg, data, onSelect) {
  const { layer, fit } = stage(svg);
  const nodes = data.nodes.map((n) => ({ ...n, w: Math.min(250, Math.max(width(n.name, 12), width(n.sub, 10)) + 24), h: 40 }));
  const at = new Map(nodes.map((n) => [n.id, n]));
  const sim = forceSimulation(nodes)
    .force('link', forceLink(data.links.map((l) => ({ source: at.get(l.from), target: at.get(l.to) }))).distance(160).strength(0.5))
    .force('charge', forceManyBody().strength(-500))
    .force('collide', forceCollide((d) => d.w / 2 + 14).strength(1))
    .force('r', forceRadial((d) => (d.away ? 280 : 0), 0, 0).strength((d) => (d.away ? 0.35 : 0.02)))
    .force('y', forceY(0).strength(0.05)).stop();
  for (let i = 0; i < 500; i++) sim.tick();

  // From the edge of one box to the edge of the other, bowed a little so two-way links stay apart.
  const curve = (a, b) => {
    const edge = (n, tx, ty) => { const dx = tx - n.x, dy = ty - n.y, s = Math.min(n.w / 2 / Math.abs(dx || 1e-6), n.h / 2 / Math.abs(dy || 1e-6)); return [n.x + dx * s, n.y + dy * s]; };
    const [x1, y1] = edge(a, b.x, b.y), [x2, y2] = edge(b, a.x, a.y);
    return `M${x1},${y1} Q${(x1 + x2) / 2 - (y2 - y1) * 0.12},${(y1 + y2) / 2 + (x2 - x1) * 0.12} ${x2},${y2}`;
  };
  const edges = el('g', {}, layer);
  const paths = data.links.map((l) => ({ l, p: el('path', { class: `edge${l.basis === 'inferred' ? ' inferred' : ''}`, d: curve(at.get(l.from), at.get(l.to)), 'stroke-width': 1.2 + Math.min(l.count, 8) * 0.35, 'marker-end': 'url(#arr)' }, edges) }));
  const boxes = nodes.map((n) => {
    const g = el('g', { class: `node${n.away ? ' away' : ''}`, transform: `translate(${n.x - n.w / 2},${n.y - n.h / 2})` }, layer);
    activate(g, `${n.name}, ${n.sub}`, () => onSelect(n.id));
    el('rect', { width: n.w, height: n.h, rx: 6, stroke: color(n.color) }, g);
    clipped(g, { x: 12, y: 17 }, n.name, n.w - 20, 12);
    clipped(g, { class: 'kind', x: 12, y: 31 }, n.sub, n.w - 20, 10);
    return { n, g };
  });

  fit(bounds(nodes));
  return (id) => {
    const near = new Set(id ? data.links.filter((l) => l.from === id || l.to === id).flatMap((l) => [l.from, l.to]) : []);
    for (const { n, g } of boxes) { g.classList.toggle('sel', n.id === id); g.classList.toggle('dim', !!id && !near.has(n.id)); }
    for (const { l, p } of paths) {
      const on = !!id && (l.from === id || l.to === id);
      p.classList.toggle('on', on); p.classList.toggle('dim', !!id && !on);
      p.setAttribute('marker-end', on ? 'url(#arr-on)' : 'url(#arr)');
    }
  };
}
