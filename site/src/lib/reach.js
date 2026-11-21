// What a selection reaches. Shared by the pages (to mark a data-tag plan at build time) and the browser.

/** The entries a selection key stands for: a group's, everything under a repository or database, or the one entry. */
export function standsFor(graph, key) {
  if (graph.groups[key]) return new Set(graph.groups[key]);
  const area = /^((repositories|databases)\/[^/]+)\/(repository|database)$/.exec(key);
  if (area) return new Set(Object.keys(graph.entries).filter((id) => id.startsWith(area[1] + '/')));
  return new Set([key]);
}

/**
 * One step from the origin in both directions: what it uses and what uses it.
 * With downstream, also everything that uses it through others: what a change to the origin could affect.
 */
export function reach(graph, origin, downstream = false) {
  const uses = new Set(), usedBy = new Set(), pairs = new Set();
  for (const [from, to] of graph.links) {
    if (origin.has(from) && !origin.has(to)) { uses.add(to); pairs.add(from + '>' + to); }
    if (origin.has(to) && !origin.has(from)) { usedBy.add(from); pairs.add(from + '>' + to); }
  }
  const further = new Set();
  if (downstream) {
    let frontier = new Set(usedBy);
    while (frontier.size) {
      const next = new Set();
      for (const [from, to] of graph.links) {
        if (!frontier.has(to) || origin.has(from)) continue;
        pairs.add(from + '>' + to);
        if (!usedBy.has(from) && !further.has(from)) { further.add(from); next.add(from); }
      }
      frontier = next;
    }
  }
  return { origin, uses, usedBy, further, pairs };
}

/** Which plots and lines of one sheet a reach lights up. */
export function marks(sheet, r) {
  const origin = new Set(), reached = new Map(), lines = new Set();
  const plotsOf = (id) => sheet.where[id] ?? [];
  for (const id of r.origin) for (const p of plotsOf(id)) origin.add(p);
  for (const set of [r.uses, r.usedBy, r.further])
    for (const id of set)
      for (const p of plotsOf(id)) if (!origin.has(p)) reached.set(p, (reached.get(p) ?? new Set()).add(id));
  for (const line of sheet.lines) if (line.pairs.some(([f, t]) => r.pairs.has(f + '>' + t))) lines.add(line.id);
  return { origin, reached, lines };
}
