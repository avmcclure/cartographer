// Geometry shared by the pages and the browser. Layout numbers are grid steps; STEP is how many pixels one is.
export const STEP = 10;
export const MARGIN = { left: 30, top: 26, right: 14, bottom: 14 };

export const px = (n) => n * STEP;

/** The SVG path of a route: its corners, source end first. */
export const pathOf = (points) => points.map(([x, y], i) => `${i ? 'L' : 'M'}${px(x)} ${px(y)}`).join(' ');

/** A filled arrowhead at the last point of a route, pointing the way the line arrives. */
export function arrowOf(points) {
  if (points.length < 2) return '';
  const [x, y] = points[points.length - 1].map(px);
  const [fx, fy] = points[points.length - 2].map(px);
  const [dx, dy] = [Math.sign(x - fx), Math.sign(y - fy)];
  const [l, w] = [9, 4.5];
  return `M${x} ${y} L${x - dx * l - dy * w} ${y - dy * l - dx * w} L${x - dx * l + dy * w} ${y - dy * l + dx * w} Z`;
}

/** The letter of a grid column: A to Z, then AA and on. */
export function columnLetter(c) {
  let s = '';
  for (; c >= 0; c = Math.floor(c / 26) - 1) s = String.fromCharCode(65 + (c % 26)) + s;
  return s;
}

export function fit(text, pixels, perChar = 7.3) {
  const max = Math.max(3, Math.floor(pixels / perChar));
  return text.length <= max ? text : text.slice(0, max - 1) + '…';
}
