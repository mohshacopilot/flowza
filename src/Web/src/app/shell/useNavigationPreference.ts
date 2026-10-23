import { Grid } from 'antd';
import { useCallback, useState } from 'react';
import designValues from '../theme/designValues.json';

const isDesktopWidth = (): boolean =>
  window.matchMedia(`(min-width: ${String(designValues.breakpoints.desktop)}px)`).matches;

/** Where the browser remembers the user's choice (spec 007 UI-04; plan 007 §5.5). */
export const navigationPreferenceKey = 'flowza.navigation.collapsed';

/** The remembered choice; undefined without one, or when the browser blocks site data (private windows). */
function readStoredChoice(): boolean | undefined {
  try {
    const value = window.localStorage.getItem(navigationPreferenceKey);
    if (value === 'true') {
      return true;
    }
    if (value === 'false') {
      return false;
    }
    return undefined;
  } catch {
    return undefined;
  }
}

function storeChoice(collapsed: boolean): void {
  try {
    window.localStorage.setItem(navigationPreferenceKey, String(collapsed));
  } catch {
    // Storage is blocked: the choice still holds until the page is reloaded.
  }
}

/**
 * Whether the side navigation is collapsed to icons (UI-04). The user's remembered choice wins; without one it
 * starts open at desktop width (C3) and collapsed at tablet width (C4). The theme makes Ant Design's lg breakpoint
 * our desktop width. O(1): one storage read when the frame mounts.
 */
export function useNavigationPreference(): {
  collapsed: boolean;
  setCollapsed: (collapsed: boolean) => void;
} {
  const screens = Grid.useBreakpoint();
  const [stored, setStored] = useState(readStoredChoice);
  const setCollapsed = useCallback((collapsed: boolean) => {
    storeChoice(collapsed);
    setStored(collapsed);
  }, []);
  // useBreakpoint() knows the width only after its first effect; asking the browser directly for the first render
  // keeps a desktop panel from starting collapsed and visibly sliding open (found by the axe checks, T008).
  const desktop = screens.lg ?? isDesktopWidth();
  return { collapsed: stored ?? !desktop, setCollapsed };
}
