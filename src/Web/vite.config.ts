import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// The browser normally reaches the web app through the Gateway (BFF, tech discussion T6), which proxies every
// non-API path to this dev server. The Aspire AppHost passes the port in PORT; 5105 is the fixed default next
// to the services' ports (Gateway 5100, Identity 5101 … Worker 5104).
const port = Number(process.env.PORT ?? 5105);

// When Vite is opened directly (without the Gateway in front), API and session calls are forwarded to the
// Gateway so the same relative URLs work.
const gateway = process.env.FLOWZA_GATEWAY_URL ?? 'http://localhost:5100';

export default defineConfig({
  plugins: [react()],
  server: {
    port,
    strictPort: true,
    proxy: {
      '/api': gateway,
      '/bff': gateway,
    },
  },
  build: {
    sourcemap: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    restoreMocks: true,
    // Whole-screen tests that type into forms take 2–3 s alone and over 5 s (Vitest's default) when all test files
    // run in parallel on the office PC; a slow test is not a failing one.
    testTimeout: 15_000,
  },
});
