// Where each part sits on the map, worked out at build time so every reader sees the same picture.
// Lanes are clusters; columns follow the data: each part one column right of the furthest part in its own lane that
// feeds it. Counting within a lane keeps a long chain across many lanes from stretching the whole map; links between
// lanes still show which way the data goes. Lookups sit in a band beside the flow, so they take no column and their
// links do not push other parts along.

/** Lanes top to bottom: the biggest first, then whichever has the most links to the lanes already placed. */
export function laneOrder(map) {
  const laneOf = new Map(map.clusters.flatMap((c) => c.members.map((m) => [m.id, c.key])));
  // Lookups sit beside the flow: being read from everywhere should not pull lanes towards theirs.
  const weight = new Map(), twin = loopsBack(map);
  const lookup = new Set(map.clusters.flatMap((c) => c.members.filter((m) => m.role === 'lookup').map((m) => m.id)));
  for (const l of map.parts.filter((l) => !twin(l) && !lookup.has(l.from) && !lookup.has(l.to))) {
    const [a, b] = [laneOf.get(l.from), laneOf.get(l.to)];
    if (a === b) continue;
    for (const [x, y] of [[a, b], [b, a]]) weight.set(`${x}|${y}`, (weight.get(`${x}|${y}`) ?? 0) + l.count);
  }
  // Parts with no links go last: they have no flow to show.
  const last = map.clusters.filter((c) => c.unlinked);
  const left = map.clusters.filter((c) => !last.includes(c)).sort((a, b) => b.members.length - a.members.length || a.key.localeCompare(b.key));
  const placed = [left.shift()].filter(Boolean);
  while (left.length) {
    // Pull towards recent lanes, so lanes that trade most end up next to each other.
    const score = (c) => placed.reduce((s, p, i) => s + (weight.get(`${c.key}|${p.key}`) ?? 0) * (i + 1), 0);
    left.sort((a, b) => score(b) - score(a) || b.members.length - a.members.length || a.key.localeCompare(b.key));
    placed.push(left.shift());
  }
  return [...placed, ...last].map((c) => c.key);
}

/**
 * Data read back through a pass-through runs from its target to it. When the same forward also carries writes onward,
 * placing by both would loop the two, so the back link is left out of placement and of counts.
 */
export function loopsBack(map) {
  const onward = new Set(map.parts.filter((l) => l.forwards && !l.back).map((l) => `${l.from}>${l.to}`));
  return (l) => !!l.back && onward.has(`${l.to}>${l.from}`);
}

/** Part ID to column. Loops are broken where they close, walking parts in ID order. */
export function columns(map) {
  const lookup = new Set(map.clusters.flatMap((c) => c.members.filter((m) => m.role === 'lookup').map((m) => m.id)));
  const ids = map.clusters.flatMap((c) => c.members.map((m) => m.id)).filter((i) => !lookup.has(i)).sort();
  const laneOf = new Map(map.clusters.flatMap((c) => c.members.map((m) => [m.id, c.key])));
  const back = loopsBack(map);
  const inFlow = (l) => !lookup.has(l.from) && !lookup.has(l.to) && !back(l);
  const data = map.parts.filter((l) => !l.call && inFlow(l) && laneOf.get(l.from) === laneOf.get(l.to));
  const out = new Map(ids.map((i) => [i, []]));
  for (const l of data) out.get(l.from)?.push(l.to);
  for (const list of out.values()) list.sort();

  // Depth-first, iteratively (an atlas can be deep), keeping only links that do not point back up the walk.
  const state = new Map(), keep = [];
  for (const root of ids) {
    if (state.has(root)) continue;
    const stack = [[root, 0]];
    state.set(root, 1);
    while (stack.length) {
      const top = stack[stack.length - 1];
      const next = out.get(top[0])[top[1]++];
      if (next === undefined) { state.set(top[0], 2); stack.pop(); continue; }
      if (state.get(next) === 1) continue;
      keep.push([top[0], next]);
      if (!state.has(next)) { state.set(next, 1); stack.push([next, 0]); }
    }
  }

  // Longest path over what is left, in topological order.
  const ins = new Map(ids.map((i) => [i, 0])), after = new Map(ids.map((i) => [i, []])), before = new Map(ids.map((i) => [i, []]));
  for (const [a, b] of keep) { ins.set(b, ins.get(b) + 1); after.get(a).push(b); before.get(b).push(a); }
  const col = new Map(ids.map((i) => [i, 0]));
  const queue = ids.filter((i) => ins.get(i) === 0), order = [];
  while (queue.length) {
    const v = queue.shift();
    order.push(v);
    for (const w of after.get(v)) {
      col.set(w, Math.max(col.get(w), col.get(v) + 1));
      ins.set(w, ins.get(w) - 1);
      if (ins.get(w) === 0) queue.push(w);
    }
  }
  // A part that only feeds others sits just before the earliest part it feeds, not out at the far left.
  for (const v of [...order].reverse())
    if (before.get(v).length === 0 && after.get(v).length) col.set(v, Math.min(...after.get(v).map((w) => col.get(w))) - 1);
  // A part with only calls sits beside what it calls, or what calls it.
  const fed = new Set(keep.flat());
  const callsOf = new Map();
  for (const l of map.parts)
    if (l.call && inFlow(l) && laneOf.get(l.from) === laneOf.get(l.to)) for (const v of [l.from, l.to]) (callsOf.get(v) ?? callsOf.set(v, []).get(v)).push(l);
  for (const v of ids) {
    if (fed.has(v)) continue;
    const calls = callsOf.get(v) ?? [];
    const peers = calls.map((l) => (l.from === v ? l.to : l.from)).filter((p) => fed.has(p));
    if (peers.length) col.set(v, Math.max(...peers.map((p) => col.get(p))) + (calls.some((l) => l.to === v) ? 1 : -1));
  }
  // Each lane starts at column 0.
  const start = new Map();
  for (const v of ids) start.set(laneOf.get(v), Math.min(start.get(laneOf.get(v)) ?? Infinity, col.get(v)));
  for (const v of ids) col.set(v, col.get(v) - start.get(laneOf.get(v)));
  for (const v of lookup) col.set(v, 0);
  return col;
}
