// The map: one lane per cluster, data flowing left to right. A lane is either open, with a box per part, or
// closed, with one box per column standing for the parts in it. Lookups, read from but never fed, sit in a band at
// the top of their lane; their links show only when one is selected. Pass-throughs sit in the flow and can be hidden,
// joining what goes into one straight to where it forwards. Selecting a part follows its data upstream and downstream
// across every lane.
import { select } from 'd3-selection';
import { zoom, zoomIdentity, zoomTransform } from 'd3-zoom';

const SVG = 'http://www.w3.org/2000/svg';
const COL = 210, W = 176, H = 40, GAP = 14, HEAD = 36, LANE_GAP = 12, BAND = H + 22;
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

/** Data read from a lookup by another part, as opposed to what the lookup itself writes or publishes. */
export const readFromLookup = (l, part) => !l.call && part.get(l.from)?.role === 'lookup' && l.verbs.every((v) => v.verb === 'reads' || v.verb === 'receives from');

/** A link's transports, or [''] when none is recorded. */
export const transportsOf = (l) => (l.over?.length ? l.over.map((o) => o.how) : ['']);

/** Links with pass-throughs taken out: what went into one joins each part it forwards to. */
export function bypass(links, parts) {
  const through = new Set(parts.filter((p) => p.role === 'pass-through').map((p) => p.id));
  if (!through.size) return links;
  const kept = links.filter((l) => !through.has(l.from) && !through.has(l.to));
  const joined = new Map();
  for (const a of links.filter((l) => !l.call && through.has(l.to) && !through.has(l.from)))
    for (const b of links.filter((l) => !l.call && l.from === a.to && !through.has(l.to))) {
      const key = `${a.from}>${b.to}`;
      const j = joined.get(key) ?? joined.set(key, { from: a.from, to: b.to, count: 0, basis: 'extracted', verbs: [], over: [], ops: [], via: [] }).get(key);
      j.count += Math.min(a.count, b.count);
      if (a.basis === 'inferred' || b.basis === 'inferred') j.basis = 'inferred';
      // Say what the caller does, not that the pass-through forwards it.
      const said = a.verbs.some((v) => v.verb === 'forwards to') ? b.verbs : a.verbs;
      for (const v of said) if (!j.verbs.some((x) => x.verb === v.verb)) j.verbs.push({ ...v });
      for (const o of [...(a.over ?? []), ...(b.over ?? [])]) if (!j.over.some((x) => x.how === o.how)) j.over.push({ ...o });
      for (const o of [...(a.ops ?? []), ...(b.ops ?? [])]) if (!j.ops.includes(o)) j.ops.push(o);
      if (!j.via.includes(a.to)) j.via.push(a.to);
    }
  return [...kept, ...joined.values()];
}

export function createMap(svg, data, { onSelect }) {
  const lanes = data.lanes, laneOf = new Map(data.parts.map((p) => [p.id, p.lane])), part = new Map(data.parts.map((p) => [p.id, p]));
  let throughShown = true, hidden = new Set(), links = data.links;
  // Small atlases open every lane; large ones open only small lanes, and the rest open on demand.
  const open = new Set(data.parts.length <= 80 ? lanes.map((l) => l.key) : lanes.filter((l) => l.count <= 6).map((l) => l.key));
  // Following data all the way can cover most of an estate (a shared database feeds everything that reads it), so a
  // trace stops after a few steps unless the reader asks for more.
  let picked = null, boxes = [], drawn = [], view, steps = 2, tagLayer;

  svg.replaceChildren();
  el('defs', {}, svg).innerHTML = ['line', 'accent', 'up'].map((c) => `<marker id="a-${c}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M0 0 L10 5 L0 10 z" fill="var(--${c})"/></marker>`).join('');
  const layer = el('g', {}, svg);
  const z = zoom().scaleExtent([0.2, 2.5]).on('zoom', (e) => layer.setAttribute('transform', e.transform));
  select(svg).call(z).on('dblclick.zoom', null);

  // What is drawn for a part right now: its own box, or its column's box in a closed lane (one box for its lookups).
  const nodeOf = (id) => (open.has(laneOf.get(id)) ? id : `${laneOf.get(id)}@${part.get(id).role === 'lookup' ? 'look' : part.get(id).col}`);
  const shown = (p) => throughShown || p.role !== 'pass-through';

  function layout() {
    const nodes = new Map(), at = new Map();
    let y = 0, right = W;
    for (const lane of lanes) {
      const members = data.parts.filter((p) => p.lane === lane.key && shown(p));
      const band = members.some((p) => p.role === 'lookup') ? BAND : 0;
      const stacks = new Map();
      let looks = 0;
      for (const p of members) {
        const key = nodeOf(p.id);
        if (!nodes.has(key)) {
          if (p.role === 'lookup') nodes.set(key, { id: key, lane, parts: [], look: true, x: looks++ * COL, y: y + HEAD + 4 });
          else {
            const k = stacks.get(p.col) ?? 0;
            stacks.set(p.col, k + 1);
            nodes.set(key, { id: key, lane, parts: [], x: p.col * COL, y: y + HEAD + band + k * (H + GAP) });
          }
        }
        const n = nodes.get(key);
        n.parts.push(p);
        right = Math.max(right, n.x + W);
      }
      const rows = Math.max(1, ...stacks.values());
      at.set(lane.key, { y, h: HEAD + band + rows * (H + GAP) + 6, band });
      y += at.get(lane.key).h + LANE_GAP;
    }
    // Links between what is drawn, added up where a closed lane stands several parts in one box.
    const edges = new Map();
    for (const l of links) {
      const [a, b] = [nodeOf(l.from), nodeOf(l.to)];
      if (a === b || !nodes.has(a) || !nodes.has(b)) continue;
      // Data read from a lookup: drawn only while the lookup is selected; its readers show a dot each instead.
      const look = readFromLookup(l, part);
      const key = `${a}>${b}>${l.call ? 1 : 0}`;
      if (!edges.has(key)) edges.set(key, { from: a, to: b, call: !!l.call, look, count: 0, links: [] });
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
      if (a.band) {
        el('rect', { class: 'band', x: -4, y: a.y + HEAD - 4, width: view.width + 8, height: a.band - 6, rx: 6 }, g);
        el('text', { class: 'band-name', x: view.width, y: a.y + HEAD + 10, 'text-anchor': 'end' }, g).textContent = 'Lookups, beside the flow';
      }
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
      const p = el('path', { class: `edge${e.call ? ' call' : ''}${e.look ? ' look' : ''}${e.links.some((l) => l.via) ? ' via' : ''}${a.lane !== b.lane ? ' cross' : ''}${e.links.every((l) => l.basis === 'inferred') ? ' inferred' : ''}`, d: route(a, b), 'stroke-width': 1.2 + Math.min(e.count, 10) * 0.25, 'marker-end': e.call ? null : 'url(#a-line)' }, edgeLayer);
      return { e, p, mid: [(a.x + W + b.x) / 2, (a.y + b.y + H) / 2] };
    });
    tagLayer = el('g', { class: 'tags' }, layer);
    const reads = new Map();
    for (const e of view.edges) if (e.look) reads.set(e.to, (reads.get(e.to) ?? 0) + 1);
    boxes = [...view.nodes.values()].map((n) => {
      const g = el('g', { class: `node${n.parts.length > 1 ? ' group' : ''}`, transform: `translate(${n.x},${n.y})` }, layer);
      const role = n.parts.length === 1 ? n.parts[0].role : n.look ? 'lookup' : null;
      if (role) g.classList.add(role);
      if (role === 'pass-through') el('polygon', { points: `12,0 ${W - 12},0 ${W},${H / 2} ${W - 12},${H} 12,${H} 0,${H / 2}`, stroke: `var(--c${n.lane.color % 8})` }, g);
      else el('rect', { width: W, height: H, rx: role === 'lookup' ? H / 2 : 6, stroke: `var(--c${n.lane.color % 8})` }, g);
      const pad = role ? 16 : 10;
      if (n.parts.length === 1) {
        const p = n.parts[0];
        const label = role === 'pass-through' ? 'pass-through' : role === 'lookup' ? `lookup · ${p.kind}` : p.type && p.type !== p.kind ? `${p.kind} · ${p.type}` : p.kind;
        activate(g, `${p.name}, ${label}`, () => pick(p.id));
        text(g, { x: pad, y: 17 }, p.name, W - pad - 18, 12);
        text(g, { class: 'kind', x: pad, y: 31 }, label, W - pad - 8, 10);
        // One dot per lookup this part reads, rather than a line to each across the map.
        const k = reads.get(n.id) ?? 0;
        for (let i = 0; i < Math.min(k, 4); i++) el('circle', { class: 'pip', cx: W - 10 - i * 8, cy: 9, r: 2.6 }, g);
        if (k) el('title', {}, g).textContent = `Reads ${k} lookup${k === 1 ? '' : 's'}`;
      } else {
        const what = n.look ? 'lookups' : 'parts';
        activate(g, `${n.parts.length} ${what} in ${n.lane.name}. Open the lane`, () => toggle(n.lane.key));
        text(g, { x: pad, y: 17 }, `${n.parts.length} ${what}`, W - pad - 18, 12);
        text(g, { class: 'kind', x: pad, y: 31 }, n.parts.map((p) => p.name).join(', '), W - pad - 8, 10);
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
    const live = links.filter((l) => transportsOf(l).some((t) => !hidden.has(t)));
    const down = picked ? reach(live, picked, 1, steps) : null, up = picked ? reach(live, picked, -1, steps) : null;
    const near = new Set(picked ? live.filter((l) => l.call && (l.from === picked || l.to === picked)).flatMap((l) => [l.from, l.to]) : []);
    tagLayer.replaceChildren();
    for (const { n, g } of boxes) {
      const ids = n.parts.map((p) => p.id);
      const isDown = !!down && ids.some((i) => down.parts.has(i)), isUp = !!up && ids.some((i) => up.parts.has(i));
      g.classList.toggle('sel', ids.includes(picked));
      g.classList.toggle('down', isDown);
      g.classList.toggle('up', isUp && !isDown);
      g.classList.toggle('dim', !!picked && !ids.includes(picked) && !isDown && !isUp && !ids.some((i) => near.has(i)));
    }
    for (const { e, p, mid } of drawn) {
      const isDown = !!down && e.links.some((l) => down.links.has(l)), isUp = !!up && e.links.some((l) => up.links.has(l));
      const isCall = !!picked && e.call && e.links.some((l) => l.from === picked || l.to === picked);
      const touches = e.links.filter((l) => l.from === picked || l.to === picked);
      p.classList.toggle('off', !e.links.some((l) => transportsOf(l).some((t) => !hidden.has(t))) || (e.look && !e.links.some((l) => l.from === picked)));
      const over = [...new Set(touches.flatMap(transportsOf))].filter(Boolean);
      if (over.length && !p.classList.contains('off')) el('text', { class: 'tag', x: mid[0], y: mid[1] - 4, 'text-anchor': 'middle' }, tagLayer).textContent = over.join(', ');
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
    /** Show or hide pass-throughs; hidden, what went into one joins each part it forwards to. */
    passThroughs(on) {
      throughShown = on;
      links = on ? data.links : bypass(data.links, data.parts);
      if (!on && part.get(picked)?.role === 'pass-through') pick(picked);
      draw();
    },
    /** Hide links over these transports ('' for links with none recorded). */
    transports(off) { hidden = new Set(off); mark(true); },
    get links() { return links; },
    steps(n) { steps = n; history.replaceState(null, '', location.pathname + location.search + (picked ? `#p=${encodeURIComponent(picked)}${n === 2 ? '' : `&d=${n === Infinity ? 'all' : n}`}` : '')); mark(true); },
    get depth() { return steps; },
  };
}
