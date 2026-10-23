import { useSyncExternalStore } from 'react';

const query = '(prefers-reduced-motion: reduce)';

function mediaQuery(): MediaQueryList | undefined {
  return typeof window === 'undefined' ? undefined : window.matchMedia(query);
}

function subscribe(onChange: () => void): () => void {
  const list = mediaQuery();
  list?.addEventListener('change', onChange);
  return () => list?.removeEventListener('change', onChange);
}

function snapshot(): boolean {
  return mediaQuery()?.matches ?? false;
}

/** Whether the user asked their system for reduced motion; follows changes while the app is open (spec 007 edge cases). */
export function usePrefersReducedMotion(): boolean {
  return useSyncExternalStore(subscribe, snapshot, () => false);
}
