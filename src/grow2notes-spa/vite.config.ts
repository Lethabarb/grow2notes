import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react()],
  build: {
    // ASP.NET Core serves the build from the same origin as the API (design.md §7.2).
    outDir: '../Grow2Notes.Web/wwwroot',
    emptyOutDir: true,
    // Never inline small assets as data: URIs, which the production CSP blocks (design.md §9.7).
    assetsInlineLimit: 0,
    // foundations.md's media queries use `min-width`, not range syntax, for older iPhones. At Vite's default targets
    // (Safari and iOS 16.4) Lightning CSS writes them as `(width>=40rem)`, which Safari before 16.4 ignores; these are
    // the same targets with Safari and iOS at 16, so the build keeps `min-width`.
    cssTarget: ['chrome111', 'edge111', 'firefox114', 'safari16', 'ios16'],
  },
  server: {
    proxy: {
      '/api': {
        target: 'https://localhost:7107',
        // Node does not trust the ASP.NET Core development certificate.
        secure: false,
      },
    },
  },
});
