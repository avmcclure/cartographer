// Keeps the selection (?sel=, ?down=) on every link inside the site, so it follows the reader from sheet to sheet.
const base = document.body.dataset.base;

export function current() {
  const p = new URLSearchParams(location.search);
  return { sel: p.get('sel'), down: p.get('down') === '1' };
}

export function carry({ sel, down }) {
  for (const a of document.querySelectorAll('a[href]:not([data-select])')) {
    const url = new URL(a.getAttribute('href'), location.href);   // an SVG link's href property is not a string
    if (url.origin !== location.origin || !url.pathname.startsWith(base) || a.hasAttribute('data-keep')) continue;
    sel ? url.searchParams.set('sel', sel) : url.searchParams.delete('sel');
    sel && down ? url.searchParams.set('down', '1') : url.searchParams.delete('down');
    a.setAttribute('href', url.pathname + url.search + url.hash);
  }
}

const now = current();
carry(now);

// On pages without a plan, say that a selection is being carried and offer to drop it.
const bar = document.getElementById('carried');
if (bar && now.sel && !document.getElementById('sheet-data')) {
  bar.hidden = false;
  bar.textContent = `Selection: ${now.sel} · `;
  const clear = Object.assign(document.createElement('a'), { href: location.pathname, textContent: 'clear' });
  clear.setAttribute('data-keep', '');
  bar.append(clear);
  fetch(base + 'graph.json')
    .then((r) => r.json())
    .then((g) => { if (g.entries[now.sel]) bar.firstChild.textContent = `Selection: ${g.entries[now.sel][0]} (${g.entries[now.sel][1]}) · `; })
    .catch(() => {});
}
