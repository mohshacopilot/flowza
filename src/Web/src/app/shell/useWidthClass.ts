import { Grid } from 'antd';
import designValues from '../theme/designValues.json';

/** The frame's three layouts (plan 007 §5.5): phone < 768 px, tablet 768–1024 px, desktop from 1025 px. */
export type WidthClass = 'phone' | 'tablet' | 'desktop';

const atLeast = (width: number): boolean => window.matchMedia(`(min-width: ${String(width)}px)`).matches;

/**
 * The current layout. The theme makes Ant Design's md and lg breakpoints our tablet and desktop widths.
 * useBreakpoint() knows the width only after its first effect, so the first render asks the browser directly;
 * otherwise a desktop panel would start collapsed and slide open (found by the axe checks, T008). O(1).
 */
export function useWidthClass(): WidthClass {
  const screens = Grid.useBreakpoint();
  if (screens.lg ?? atLeast(designValues.breakpoints.desktop)) {
    return 'desktop';
  }
  if (screens.md ?? atLeast(designValues.breakpoints.tablet)) {
    return 'tablet';
  }
  return 'phone';
}
