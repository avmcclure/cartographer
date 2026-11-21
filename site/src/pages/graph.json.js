// The link graph, for selection in the browser.
import { graph } from '../lib/data.js';
export const GET = () => new Response(JSON.stringify(graph), { headers: { 'content-type': 'application/json' } });
