import { act, renderHook } from '@testing-library/react';
import { usePrefersReducedMotion } from './reducedMotion';

/** A matchMedia whose answer the test can change, telling its listeners like a browser does. */
function fakeMatchMedia(initiallyReduced: boolean) {
  let matches = initiallyReduced;
  const listeners = new Set<() => void>();
  const original = window.matchMedia.bind(window);
  window.matchMedia = (query: string) =>
    ({
      get matches() {
        return matches;
      },
      media: query,
      addEventListener: (_type: string, listener: () => void) => listeners.add(listener),
      removeEventListener: (_type: string, listener: () => void) => listeners.delete(listener),
    }) as unknown as MediaQueryList;
  return {
    change(reduced: boolean) {
      matches = reduced;
      listeners.forEach((listener) => {
        listener();
      });
    },
    restore() {
      window.matchMedia = original;
    },
  };
}

describe('Reduced motion', () => {
  it('is_off_when_the_system_does_not_ask_for_it', () => {
    const media = fakeMatchMedia(false);
    const { result } = renderHook(() => usePrefersReducedMotion());
    expect(result.current).toBe(false);
    media.restore();
  });

  it('follows_the_system_setting_while_the_app_is_open', () => {
    const media = fakeMatchMedia(true);
    const { result } = renderHook(() => usePrefersReducedMotion());
    expect(result.current).toBe(true);

    act(() => {
      media.change(false);
    });

    expect(result.current).toBe(false);
    media.restore();
  });
});
