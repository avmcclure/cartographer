// Every entry, for the search box.
import { everything } from '../lib/data.js';
export const GET = () => new Response(JSON.stringify(everything.map(({ id, name, kind, type }) => ({ id, name, kind, type }))), { headers: { 'content-type': 'application/json' } });
