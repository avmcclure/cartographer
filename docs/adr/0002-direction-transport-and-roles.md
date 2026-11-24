# 2. Links record direction and transport; parts have roles

Date: 2026-11-24. Status: accepted.

## Context

The map (ADR 1) follows data from where it is written to where it is read. Calls to another component were drawn as undirected dotted lines because a call carries a request, not data, so they took no part in columns or traces. Two kinds of API suffered:

- **A pass-through**, such as a gateway put in front of an old and a new API so callers never change address, has only calls. It was placed beside whatever it called instead of between its callers and its targets, a trace never reached it, and clustering filed it with one side.
- **A read-only API**, such as pricing or identity, is read by many parts. Drawing it upstream of all of them would pull every reader into one long chain, yet it is not part of any one flow.

Readers also wanted to know what each link travels over, to find, for example, everything still talking over COM or .NET remoting.

The model was reviewed as a prototype (`prototypes/links.src.html`, published as the "Atlas Links and Transports" artifact).

## Decision

- **A call is recorded by what it does.** Fetching data is `reads` and changing it is `writes`, now allowed to point at a component as well as a table. `forwards-to` is new, for a pass-through. `calls` stays for a call whose direction is not known; it is still drawn dotted and should become an open question.
- **Every link may say what it travels over**, in the existing `how` key, from a fixed list that `check` enforces. The operation goes in `uses`, which already lists what a link uses. Reusing both keys keeps existing atlases valid.
- **Parts have roles, worked out from their links.** A part with `forwards-to` links is a pass-through and sits in the flow. A component or external system that two or more parts read from, and whose data comes only from databases, is a lookup and sits in a band at the top of its own lane. Everything else is a stage. A person or an agent can set `role` on an entry when the links place it wrongly.
- **A forward takes the direction of what goes through it.** Data written into a pass-through flows on to its targets; data read from it flows back from them.
- **The map draws direction only.** Transport is a filter with counts, and a label on the selected part's links. Thirteen transports would need more line styles than anyone can read.

## Consequences

- Scan agents record reads and writes against components, with `how` and the operations, instead of a bare `calls`. Extraction records `sql` on database access found in code. Proxy configuration (YARP, Ocelot, nginx, API Management) is read by agents; mechanical extraction of it waits until there is a way to resolve a destination address to a component.
- One link per type and target means a part that sends a file over SFTP and calls the same company over HTTP keeps the main transport only.
- Lookups do not change clustering: a widely read service still pulls on the clusters of its readers. If that makes clusters worse on a real atlas, lookup links can be weighted down.
