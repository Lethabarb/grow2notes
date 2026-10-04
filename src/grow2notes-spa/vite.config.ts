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
