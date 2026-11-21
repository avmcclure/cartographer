# Static site generators for Markdown entries with graph views

Research for [issue #5](https://github.com/avmcclure/cartographer/issues/5). Primary sources only, checked 2026-11-20. No independent benchmark at "thousands of pages" was found for any candidate; vendor claims are quoted as claims.

## Answer

**Use Astro Starlight.** It is the only candidate where all three needs are first-party: frontmatter is a typed, queryable data set (content collections with Zod schemas and `reference()` links between entries, queried with `getCollection()`), search is on by default (Pagefind), and generated pages can reuse the docs layout (`<StarlightPage>`). Astro documents build-time collections as "suitable for tens of thousands of content entries".

**Hugo** is the fallback if build time dominates: fastest by its own claim and has native frontmatter queries and taxonomies, but ships no docs theme or search, and Mermaid needs a hand-written render hook.

**Rule out MkDocs Material**: it is in maintenance mode (fixes end 5 May 2027) and its authors call MkDocs itself unmaintained. **Docusaurus** has no built-in search and no frontmatter query layer.

No candidate derives diagrams from frontmatter out of the box. In every case we generate Mermaid text ourselves, so the cheapest design is for the Cartographer CLI to emit index pages and Mermaid blocks as plain Markdown, keeping the site generator swappable.

**Main risk:** Starlight's build time and memory at our real page count is unverified; prototype with ~5,000 synthetic entries before committing.

## Comparison

| | Astro Starlight | MkDocs Material | Docusaurus | Hugo |
|---|---|---|---|---|
| Build at thousands of pages | Collections "suitable for tens of thousands" of entries, cached between builds [1]; Astro 5 claims "up to 5x faster" Markdown builds [2]. Wall-clock unverified. | Unverified. Successor Zensical targets "tens of thousands of pages" as a goal, not a result [5]. | "Faster" (Rspack) claims 2-4x faster builds on three community sites [8]; nothing on large sites. Unverified. | "Renders a large site in seconds, often less" [11]; no published figures. |
| Query frontmatter | `getCollection()` with filter on `data`; Zod validation; `reference()` between entries [1]; schema extended via `docsSchema({extend})` [3] | None built in beyond per-page meta passed to the template [6] and the tags index [7]; cross-page needs a plugin using `on_files` etc. [14] | No query layer; write a plugin (`loadContent`, `createData` + `addRoute`) [9] | `where site.RegularPages "Params.foo" "bar"` in templates [12]; taxonomies auto-generate term and list pages [13] |
| Mermaid | Not in Starlight docs; add via Markdown processor plugin or own component [4] | Native via `pymdownx.superfences` [15] | Official `@docusaurus/theme-mermaid`; `<Mermaid value=...>` component for dynamic diagrams [10] | Not built in; write a render hook and load the library [16] |
| Search | Pagefind by default, no config [17] | Built in, lunr.js, whole index shipped to browser [18] | None built in; Algolia official, local search is community [19] | None in the docs located (official search page not found; unverified) |
| Custom code | Low-medium: schema, a few `.astro` index pages, a diagram component | Medium: Python plugin/hooks for anything cross-page | High: React plugin plus search plugin | Medium-high: Go templates, theme, render hook, search wiring |

## Detail

**Astro Starlight.** Frontmatter relationships map directly onto `reference()` fields, validated at build; a broken relationship fails the build with an error [1]. Generated pages live in `src/pages/` and wrap content in `<StarlightPage>`, described as useful when "generating content dynamically" [20]. Pagefind states a 10,000-page site is searchable with "a total network payload under 300kB" [21]. Gap: no documented Mermaid support, so a plugin or a small client-side component is needed [4].

**MkDocs Material.** Best out-of-box Mermaid [15], but the vendor states "Feature development has ended", with critical fixes "through May 5, 2027", and that MkDocs "must be considered a supply chain risk, since it's unmaintained since August 2024" [5]. Zensical reads `mkdocs.yml` but supports only "a subset of plugins" [5], which is exactly where cross-page generation would live. Too unstable a base for a new build.

**Docusaurus.** Mermaid is well supported, including a component that takes generated diagram source [10]. But index pages need a custom plugin [9], and local search is suggested only where "the search index is small" [19].

**Hugo.** Strongest native data templating [12][13] and speed reputation [11], and Pagefind can index any static output [21]. Cost: everything a docs site needs (theme, navigation, search UI, Mermaid hook [16]) is ours to build or borrow from a third-party theme.

## Sources

1. https://docs.astro.build/en/guides/content-collections/
2. https://astro.build/blog/astro-5/
3. https://starlight.astro.build/reference/frontmatter/
4. https://starlight.astro.build/guides/authoring-content/
5. https://squidfunk.github.io/mkdocs-material/blog/2025/11/05/zensical/
6. https://www.mkdocs.org/user-guide/writing-your-docs/
7. https://squidfunk.github.io/mkdocs-material/plugins/tags/
8. https://docusaurus.io/blog/releases/3.6
9. https://docusaurus.io/docs/advanced/plugins
10. https://docusaurus.io/docs/markdown-features/diagrams
11. https://gohugo.io/
12. https://gohugo.io/functions/collections/where/
13. https://gohugo.io/content-management/taxonomies/
14. https://www.mkdocs.org/dev-guide/plugins/
15. https://squidfunk.github.io/mkdocs-material/reference/diagrams/
16. https://gohugo.io/content-management/diagrams/
17. https://starlight.astro.build/guides/site-search/
18. https://squidfunk.github.io/mkdocs-material/plugins/search/
19. https://docusaurus.io/docs/search
20. https://starlight.astro.build/guides/pages/
21. https://pagefind.app/
