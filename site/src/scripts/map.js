// The map: one lane per cluster, data flowing left to right. A lane is either open, with a box per part, or
// closed, with one box per column standing for the parts in it. Selecting a part follows its data upstream and
// downstream across every lane.
import { select } from 'd3-selection';
import { zoom, zoomIdentity, zoomTransform } from 'd3-zoom';

const SVG = 'http://www.w3.org/2000/svg';
const COL = 210, W = 176, H = 40, GAP = 14, HEAD = 36, LANE_GAP = 12;
const width = (s, px) => s.length * px * 0.56;

function el(name, attrs = {}, parent) {
  const e = document.createElementNS(SVG, name);
  for (const [k, v] of Object.entries(attrs)) if (v !== undefined && v !== null) e.setAttribute(k, v);
  parent?.append(e);
  return e;
}

function text(parent, attrs, value, max, px) {
  const t = el('text', attrs, parent);
  let s = value;
  if (width(s, px) > max) {
    while (s.length > 3 && width(s + '…', px) > max) s = s.slice(0, -1);
    s += '…';
    el('title', {}, t).textContent = value;
  }
  t.append(s);
  return t;
}

function activate(g, label, go) {
  g.setAttribute('tabindex', '0');
  g.setAttribute('role', 'button');
  g.setAttribute('aria-label', label);
  g.addEventListener('click', go);
  g.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); go(); } });
}

/** What a part's data reaches (or what feeds it), up to a number of steps away, and the links walked. */
export function reach(links, id, dir, steps = Infinity) {
  const next = new Map();
  for (const l of links) {
    if (l.call) continue;
    const a = dir > 0 ? l.from : l.to;
    if (!next.has(a)) next.set(a, []);
    next.get(a).push(l);
  }
  const parts = new Set([id]), used = new Set();
  let frontier = [id];
  for (let step = 0; step < steps && frontier.length; step++) {
    const found = [];
    for (const v of frontier)
      for (const l of next.get(v) ?? []) {
        used.add(l);
        const b = dir > 0 ? l.to : l.from;
        if (!parts.has(b)) { parts.add(b); found.push(b); }
      }
    frontier = found;
  }
  parts.delete(id);
  return { parts, links: used };
}

export function createMap(svg, data, { onSelect }) {
  const lanes = data.lanes, laneOf = new Map(data.parts.map((p) => [p.id, p.lane])), part = new Map(data.parts.map((p) => [p.id, p]));
  // Small atlases open every lane; large ones open only small lanes, and the rest open on demand.
  const open = new Set(data.parts.length <= 80 ? lanes.map((l) => l.key) : lanes.filter((l) => l.count <= 6).map((l) => l.key));
  // Following data all the way can cover most of an estate (a shared database feeds everything that reads it), so a
  // trace stops after a few steps unless the reader asks for more.
  let picked = null, boxes = [], drawn = [], view, steps = 2;

  svg.replaceChildren();
  el('defs', {}, svg).innerHTML = ['line', 'accent', 'up'].map((c) => `<marker id="a-${c}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M0 0 L10 5 L0 10 z" fill="var(--${c})"/></marker>`).join('');
  const layer = el('g', {}, svg);
  const z = zoom().scaleExtent([0.2, 2.5]).on('zoom', (e) => layer.setAttribute('transform', e.transform));
  select(svg).call(z).on('dblclick.zoom', null);

  // What is drawn for a part right now: its own box, or its column's box in a closed lane.
  const nodeOf = (id) => (open.has(laneOf.get(id)) ? id : `${laneOf.get(id)}@${part.get(id).col}`);

  function layout() {
    const nodes = new Map(), at = new Map();
    let y = 0, right = W;
    for (const lane of lanes) {
      const members = data.parts.filter((p) => p.lane === lane.key);
      const stacks = new Map();
      for (const p of members) {
        const key = nodeOf(p.id);
        if (!nodes.has(key)) {
          const k = stacks.get(p.col) ?? 0;
          stacks.set(p.col, k + 1);
          nodes.set(key, { id: key, lane, parts: [], x: p.col * COL, y: y + HEAD + k * (H + GAP) });
        }
        nodes.get(key).parts.push(p);
        right = Math.max(right, p.col * COL + W);
      }
      const rows = Math.max(1, ...stacks.values());
      at.set(lane.key, { y, h: HEAD + rows * (H + GAP) + 6 });
      y += at.get(lane.key).h + LANE_GAP;
    }
    // Links between what is drawn, added up where a closed lane stands several parts in one box.
    const edges = new Map();
    for (const l of data.links) {
      const [a, b] = [nodeOf(l.from), nodeOf(l.to)];
      if (a === b) continue;
      const key = `${a}>${b}>${l.call ? 1 : 0}`;
      if (!edges.has(key)) edges.set(key, { from: a, to: b, call: !!l.call, count: 0, links: [] });
      edges.get(key).count += l.count;
      edges.get(key).links.push(l);
    }
    return { nodes, edges: [...edges.values()], at, width: right, height: y };
  }

  function draw() {
    view = layout();
    layer.replaceChildren();
    for (const lane of lanes) {
      const a = view.at.get(lane.key), g = el('g', { class: 'lane' }, layer);
      el('rect', { x: -16, y: a.y, width: view.width + 32, height: a.h, rx: 10 }, g);
      el('rect', { x: -16, y: a.y, width: 5, height: a.h, rx: 2, fill: `var(--c${lane.color % 8})` }, g);
      const head = el('g', { class: 'lane-head', 'data-lane': lane.key }, g);
      activate(head, `${lane.name}, ${lane.count} parts. ${open.has(lane.key) ? 'Close' : 'Open'} this lane`, () => toggle(lane.key));
      const label = `${open.has(lane.key) ? '▾' : '▸'} ${lane.name}`;
      const sub = `${lane.count} part${lane.count === 1 ? '' : 's'}${lane.pinned ? ' · pinned' : ''}`;
      el('rect', { class: 'hit', x: -6, y: a.y + 5, width: width(label, 13) + width(sub, 12) + 30, height: 26, rx: 6 }, head);
      const t = el('text', { x: 0, y: a.y + 23 }, head);
      t.append(label);
      el('tspan', { class: 'sub', dx: 8 }, t).textContent = sub;
    }
    const edgeLayer = el('g', {}, layer);
    drawn = view.edges.map((e) => {
      const a = view.nodes.get(e.from), b = view.nodes.get(e.to);
      const p = el('path', { class: `edge${e.call ? ' call' : ''}${a.lane !== b.lane ? ' cross' : ''}${e.links.every((l) => l.basis === 'inferred') ? ' inferred' : ''}`, d: route(a, b), 'stroke-width': 1.2 + Math.min(e.count, 10) * 0.25, 'marker-end': e.call ? null : 'url(#a-line)' }, edgeLayer);
      return { e, p };
    });
    boxes = [...view.nodes.values()].map((n) => {
      const g = el('g', { class: `node${n.parts.length > 1 ? ' group' : ''}`, transform: `translate(${n.x},${n.y})` }, layer);
      el('rect', { width: W, height: H, rx: 6, stroke: `var(--c${n.lane.color % 8})` }, g);
      if (n.parts.length === 1) {
        const p = n.parts[0];
        activate(g, `${p.name}, ${p.kind}`, () => pick(p.id));
        text(g, { x: 10, y: 17 }, p.name, W - 18, 12);
        text(g, { class: 'kind', x: 10, y: 31 }, p.type && p.type !== p.kind ? `${p.kind} · ${p.type}` : p.kind, W - 18, 10);
      } else {
        activate(g, `${n.parts.length} parts in ${n.lane.name}. Open the lane`, () => toggle(n.lane.key));
        text(g, { x: 10, y: 17 }, `${n.parts.length} parts`, W - 18, 12);
        text(g, { class: 'kind', x: 10, y: 31 }, n.parts.map((p) => p.name).join(', '), W - 18, 10);
      }
      return { n, g };
    });
    mark();
  }

  // Forward links leave the right side and enter the left. Others run between facing edges: bottom to top when the
  // target is lower, top to bottom when it is higher, and under both boxes when they share a row.
  function route(a, b) {
    if (b.x > a.x) {
      const [x1, y1, x2, y2] = [a.x + W, a.y + H / 2, b.x, b.y + H / 2], dx = Math.max(40, (x2 - x1) / 2);
      return `M${x1},${y1} C${x1 + dx},${y1} ${x2 - dx},${y2} ${x2},${y2}`;
    }
    const [x1, x2] = [a.x + W / 2, b.x + W / 2];
    if (b.y > a.y) return `M${x1},${a.y + H} C${x1},${a.y + H + 60} ${x2},${b.y - 60} ${x2},${b.y}`;
    if (b.y < a.y) return `M${x1},${a.y} C${x1},${a.y - 60} ${x2},${b.y + H + 60} ${x2},${b.y + H}`;
    return `M${x1},${a.y + H} C${x1},${a.y + H + 34} ${x2},${b.y + H + 34} ${x2},${b.y + H}`;
  }

  function mark(notify = false) {
    const down = picked ? reach(data.links, picked, 1, steps) : null, up = picked ? reach(data.links, picked, -1, steps) : null;
    const near = new Set(picked ? data.links.filter((l) => l.call && (l.from === picked || l.to === picked)).flatMap((l) => [l.from, l.to]) : []);
    for (const { n, g } of boxes) {
      const ids = n.parts.map((p) => p.id);
      const isDown = !!down && ids.some((i) => down.parts.has(i)), isUp = !!up && ids.some((i) => up.parts.has(i));
      g.classList.toggle('sel', ids.includes(picked));
      g.classList.toggle('down', isDown);
      g.classList.toggle('up', isUp && !isDown);
      g.classList.toggle('dim', !!picked && !ids.includes(picked) && !isDown && !isUp && !ids.some((i) => near.has(i)));
    }
    for (const { e, p } of drawn) {
      const isDown = !!down && e.links.some((l) => down.links.has(l)), isUp = !!up && e.links.some((l) => up.links.has(l));
      const isCall = !!picked && e.call && e.links.some((l) => l.from === picked || l.to === picked);
      p.classList.toggle('down', isDown);
      p.classList.toggle('up', isUp && !isDown);
      p.classList.toggle('near', isCall);
      p.classList.toggle('dim', !!picked && !isDown && !isUp && !isCall);
      if (!e.call) p.setAttribute('marker-end', isDown ? 'url(#a-accent)' : isUp ? 'url(#a-up)' : 'url(#a-line)');
    }
    if (notify) onSelect(picked, down, up);
  }

  function toggle(key) {
    open.has(key) ? open.delete(key) : open.add(key);
    draw();
    // Redrawing replaces the focused element; give focus back to the lane's name.
    [...layer.querySelectorAll('.lane-head')].find((h) => h.dataset.lane === key)?.focus({ preventScroll: true });
  }

  function pick(id) {
    picked = picked === id || !part.has(id) ? null : id;
    history.replaceState(null, '', location.pathname + location.search + (picked ? `#p=${encodeURIComponent(picked)}${steps === 2 ? '' : `&d=${steps === Infinity ? 'all' : steps}`}` : ''));
    mark(true);
  }

  // Bring a lane, or a part, into view at the current zoom, opening its lane first if it is closed.
  function show(lane, id) {
    if (!open.has(lane)) { open.add(lane); draw(); }
    const k = zoomTransform(svg).k;
    const box = id ? view.nodes.get(id) : null;
    const [x, y] = box ? [box.x - svg.clientWidth / (3 * k), box.y - svg.clientHeight / (3 * k)] : [-30 / k, view.at.get(lane).y - 20 / k];
    select(svg).call(z.transform, zoomIdentity.translate(-x * k, -y * k).scale(k));
  }

  draw();
  // Readable first: fit the height if it can be done at 80% or more, then pan along the flow for the rest.
  const k = Math.max(0.8, Math.min(1, (svg.clientHeight - 60) / Math.max(view.height, 1)));
  select(svg).call(z.transform, zoomIdentity.translate(30, 20).scale(k));

  return {
    lane(key) { if (lanes.some((l) => l.key === key)) show(key); },
    focus(id, depth) { if (!part.has(id)) return; if (depth) steps = depth; show(laneOf.get(id), id); if (picked !== id) pick(id); else mark(true); },
    clear() { if (picked) pick(picked); },
    steps(n) { steps = n; history.replaceState(null, '', location.pathname + location.search + (picked ? `#p=${encodeURIComponent(picked)}${n === 2 ? '' : `&d=${n === Infinity ? 'all' : n}`}` : '')); mark(true); },
    get depth() { return steps; },
  };
}
