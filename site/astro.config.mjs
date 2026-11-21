import { defineConfig } from 'astro/config';

// CARTOGRAPHER_BASE: the path the site is served under, when it is not the root of its host.
export default defineConfig({
  base: process.env.CARTOGRAPHER_BASE ?? '/',
  trailingSlash: 'always',
  devToolbar: { enabled: false },
});
