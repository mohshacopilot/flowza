import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { initI18n } from '../shared/i18n/i18n';
import { server } from './server';

initI18n();

// Ant Design reads these browser APIs, which jsdom does not provide. (Tooling tests run in Node, without a window.)
if (typeof window !== 'undefined') {
  window.ResizeObserver = class {
    observe(): void {}
    unobserve(): void {}
    disconnect(): void {}
  };
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string): Partial<MediaQueryList> => ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });
}

// Every request in a test must be answered by an MSW handler; an unexpected call fails the test.
beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' });
});
afterEach(() => {
  server.resetHandlers();
  cleanup();
  // Remembered choices (e.g. the collapsed navigation) must not leak into the next test.
  if (typeof window !== 'undefined') {
    window.localStorage.clear();
  }
});
afterAll(() => {
  server.close();
});
