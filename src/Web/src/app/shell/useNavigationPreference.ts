import { Grid } from 'antd';
import { useCallback, useState } from 'react';

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
  return { collapsed: stored ?? screens.lg !== true, setCollapsed };
}
