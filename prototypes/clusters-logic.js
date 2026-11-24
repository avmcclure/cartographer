// An example atlas, and the clustering. Kept apart from the page so it can be checked with node.
const PARTS = [
  // id, name, kind, owner (repository or system the scan found it in)
  ['site-api', 'site-api', 'service', 'core-monorepo'],
  ['site-portal', 'site-portal', 'web app', 'core-monorepo'],
  ['site-sync', 'site-sync-worker', 'worker', 'core-monorepo'],
  ['t-site-events', 'site-events', 'topic', null],
  ['t-open-hours', 'site-open-hours', 'topic', null],
  ['t-site-org', 'site-organization', 'topic', null],
  ['t-manager', 'site-manager', 'topic', null],
  ['t-manager-events', 'site-manager-events', 'topic', null],
  ['db-store', 'SiteDb', 'database', 'site-data'],

  ['order-api', 'order-api', 'service', 'ordering'],
  ['order-worker', 'order-worker', 'worker', 'ordering'],
  ['pos-gateway', 'pos-gateway', 'service', 'core-monorepo'],
  ['t-order-placed', 'order-placed', 'topic', null],
  ['t-order-status', 'order-status', 'topic', null],
  ['db-orders', 'OrdersDb', 'database', 'ordering'],
  ['x-printer', 'Receipt printer', 'external', null],

  ['kitchen-display', 'kitchen-display', 'web app', 'kitchen'],
  ['kitchen-svc', 'kitchen-service', 'service', 'core-monorepo'],
  ['t-kitchen-hours', 'kitchen-hours', 'topic', null],
  ['t-kitchen-tickets', 'kitchen-tickets', 'topic', null],

  ['device-bridge', 'device-hub-bridge', 'service', 'core-monorepo'],
  ['telemetry', 'telemetry-ingest', 'function app', 'devices'],
  ['t-iot-acks', 'device-acks', 'topic', null],
  ['t-commands', 'site-commands', 'topic', null],
  ['db-devices', 'DeviceRegistry', 'database', 'devices'],

  ['delivery', 'delivery-service', 'service', 'delivery'],
  ['vendor-adapter', 'vendor-adapter', 'function app', 'core-monorepo'],
  ['t-delivery-vendor', 'delivery-partner', 'topic', null],
  ['x-commerce', 'Commerce platform', 'external', null],

  ['identity-proxy', 'identity-proxy', 'service', 'platform'],
  ['flags', 'flags-service', 'service', 'platform'],
  ['log-shipper', 'log-shipper', 'worker', 'platform'],
  ['x-identity', 'Identity provider', 'external', null],
  ['x-flags', 'Feature flag service', 'external', null],
  ['x-monitoring', 'Monitoring service', 'external', null],

  ['reporting-etl', 'reporting-etl', 'worker', 'reporting'],
  ['db-reporting', 'ReportingDb', 'database', 'reporting'],
  ['f-blob-import', 'Nightly import files', 'file drop', null],
  ['r-insert-plan', 'load_daily_plan', 'stored procedure', 'reporting'],
].map(([id, name, kind, owner]) => ({ id, name, kind, owner }));

// from, to, verb
const LINKS_PINNED = [
  ['site-api', 't-site-events', 'publishes'], ['site-api', 't-site-org', 'publishes'], ['site-api', 't-open-hours', 'publishes'],
  ['site-api', 'db-store', 'writes'], ['site-portal', 'site-api', 'calls'], ['site-sync', 't-site-org', 'subscribes'],
  ['site-sync', 'db-store', 'writes'], ['site-api', 't-manager', 'publishes'], ['site-portal', 't-manager-events', 'publishes'],
  ['site-sync', 't-manager', 'subscribes'], ['site-sync', 't-open-hours', 'subscribes'], ['site-portal', 'db-store', 'reads'],

  ['order-api', 't-order-placed', 'publishes'], ['order-worker', 't-order-placed', 'subscribes'], ['order-worker', 'db-orders', 'writes'],
  ['order-worker', 't-order-status', 'publishes'], ['pos-gateway', 'order-api', 'calls'], ['pos-gateway', 'x-printer', 'prints to'],
  ['order-api', 'db-orders', 'reads'], ['pos-gateway', 't-order-status', 'subscribes'],

  ['kitchen-display', 't-kitchen-tickets', 'subscribes'], ['kitchen-svc', 't-kitchen-tickets', 'publishes'],
  ['kitchen-svc', 't-kitchen-hours', 'subscribes'], ['kitchen-display', 't-kitchen-hours', 'subscribes'],

  ['device-bridge', 't-iot-acks', 'subscribes'], ['device-bridge', 't-commands', 'publishes'], ['telemetry', 't-iot-acks', 'publishes'],
  ['telemetry', 'db-devices', 'writes'], ['device-bridge', 'db-devices', 'reads'],

  ['delivery', 't-delivery-vendor', 'publishes'], ['vendor-adapter', 't-delivery-vendor', 'subscribes'], ['vendor-adapter', 'x-commerce', 'calls'],
  ['delivery', 'x-commerce', 'calls'],

  ['identity-proxy', 'x-identity', 'signs in with'], ['flags', 'x-flags', 'reads flags from'], ['log-shipper', 'x-monitoring', 'sends logs to'],

  ['reporting-etl', 'db-reporting', 'writes'], ['reporting-etl', 'f-blob-import', 'reads'], ['reporting-etl', 'r-insert-plan', 'runs'],
  ['r-insert-plan', 'db-reporting', 'writes'],

  // the few links between groups
  ['site-api', 'x-identity', 'signs in with'], ['site-portal', 'identity-proxy', 'calls'], ['order-api', 'flags', 'calls'],
  ['kitchen-svc', 't-open-hours', 'subscribes'], ['order-worker', 't-site-org', 'subscribes'], ['device-bridge', 't-site-org', 'subscribes'],
  ['delivery', 't-order-status', 'subscribes'], ['reporting-etl', 'db-orders', 'reads'], ['reporting-etl', 'db-store', 'reads'],
  ['telemetry', 'x-monitoring', 'sends logs to'], ['site-sync', 'f-blob-import', 'writes'],
];

// What later scans found: the kitchen now runs off orders, not its own tickets topic.
const LINKS_NEW = [
  ['kitchen-display', 't-order-placed', 'subscribes'], ['kitchen-display', 't-order-status', 'publishes'],
  ['kitchen-svc', 't-order-placed', 'subscribes'], ['kitchen-svc', 'db-orders', 'reads'],
  ['order-worker', 't-kitchen-tickets', 'publishes'], ['kitchen-display', 'order-api', 'calls'],
  ['pos-gateway', 't-kitchen-tickets', 'subscribes'],
];

// Louvain: move each part to the neighbouring group that most raises modularity, then treat groups as parts and repeat.
function louvain(ids, links) {
  const index = new Map(ids.map((id, i) => [id, i]));
  let n = ids.length;
  let w = new Map(); // "a,b" with a<=b -> weight
  const add = (m, a, b, x) => { const k = a < b ? `${a},${b}` : `${b},${a}`; m.set(k, (m.get(k) || 0) + x); };
  for (const [a, b] of links) add(w, index.get(a), index.get(b), 1);
  let member = ids.map((_, i) => i); // original part -> current node

  for (let level = 0; level < 10; level++) {
    const adj = Array.from({ length: n }, () => new Map());
    const self = new Array(n).fill(0);
    let m2 = 0;
    for (const [k, x] of w) {
      const [a, b] = k.split(',').map(Number);
      if (a === b) { self[a] += x; m2 += 2 * x; continue; }
      adj[a].set(b, (adj[a].get(b) || 0) + x); adj[b].set(a, (adj[b].get(a) || 0) + x); m2 += 2 * x;
    }
    if (m2 === 0) break;
    const deg = adj.map((nb, i) => [...nb.values()].reduce((s, x) => s + x, 0) + 2 * self[i]);
    const com = [...Array(n).keys()];
    const tot = deg.slice();
    let moved = true, any = false;
    for (let pass = 0; moved && pass < 50; pass++) {
      moved = false;
      for (let v = 0; v < n; v++) {
        const c0 = com[v];
        const toCom = new Map();
        for (const [u, x] of adj[v]) toCom.set(com[u], (toCom.get(com[u]) || 0) + x);
        tot[c0] -= deg[v];
        let best = c0, gain = (toCom.get(c0) || 0) - tot[c0] * deg[v] / m2;
        for (const [c, x] of [...toCom].sort((p, q) => p[0] - q[0])) {
          const g = x - tot[c] * deg[v] / m2;
          if (g > gain + 1e-9) [best, gain] = [c, g];
        }
        tot[best] += deg[v];
        if (best !== c0) { com[v] = best; moved = true; any = true; }
      }
    }
    if (!any) break;
    const renum = new Map();
    for (const c of com) if (!renum.has(c)) renum.set(c, renum.size);
    const next = new Map();
    for (const [k, x] of w) { const [a, b] = k.split(',').map(Number); add(next, renum.get(com[a]), renum.get(com[b]), x); }
    member = member.map((v) => renum.get(com[v]));
    w = next; n = renum.size;
  }
  return new Map(ids.map((id, i) => [id, member[i]]));
}

function modularity(ids, links, group) {
  const deg = new Map(ids.map((id) => [id, 0]));
  for (const [a, b] of links) { deg.set(a, deg.get(a) + 1); deg.set(b, deg.get(b) + 1); }
  const m = links.length;
  const inside = new Map(), total = new Map();
  for (const [a, b] of links) if (group.get(a) === group.get(b)) inside.set(group.get(a), (inside.get(group.get(a)) || 0) + 1);
  for (const id of ids) total.set(group.get(id), (total.get(group.get(id)) || 0) + deg.get(id));
  let q = 0;
  for (const [c, t] of total) q += (inside.get(c) || 0) / m - (t / (2 * m)) ** 2;
  return q;
}

if (typeof module !== 'undefined') module.exports = { PARTS, LINKS_PINNED, LINKS_NEW, louvain, modularity };
