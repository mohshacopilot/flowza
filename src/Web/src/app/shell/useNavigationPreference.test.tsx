import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { TestProviders } from '../../test/render';
import { viewportWidth } from '../../test/viewport';
import { navigationPreferenceKey, useNavigationPreference } from './useNavigationPreference';

// The real theme carries our breakpoints (tablet 768, desktop 1025), so the providers are needed.
const wrapper = ({ children }: { children: ReactNode }) => <TestProviders>{children}</TestProviders>;

function collapsedAt(width: number): boolean {
  const viewport = viewportWidth(width);
  try {
    const { result, unmount } = renderHook(() => useNavigationPreference(), { wrapper });
    const collapsed = result.current.collapsed;
    unmount();
    return collapsed;
  } finally {
    viewport.restore();
  }
}

describe('Navigation preference', () => {
  it('UI_04_AC2_without_a_remembered_choice_it_is_open_at_desktop_width', () => {
    expect(collapsedAt(1280)).toBe(false);
    expect(collapsedAt(1025)).toBe(false);
  });

  it('UI_04_AC2_without_a_remembered_choice_it_is_collapsed_at_tablet_width', () => {
    expect(collapsedAt(900)).toBe(true);
    // 1024 px is still tablet for us (C4), though Ant Design's own "lg" starts at 992 px.
    expect(collapsedAt(1024)).toBe(true);
    expect(collapsedAt(768)).toBe(true);
  });

  it('UI_04_AC2_at_desktop_width_the_very_first_render_is_already_open', () => {
    // Otherwise the panel would start collapsed and slide open while the page is shown.
    const viewport = viewportWidth(1280);
    const seen: boolean[] = [];
    renderHook(
      () => {
        const { collapsed } = useNavigationPreference();
        seen.push(collapsed);
      },
      { wrapper },
    );
    viewport.restore();

    expect(seen.length).toBeGreaterThan(0);
    expect(seen.every((collapsed) => !collapsed)).toBe(true);
  });

  it('UI_04_AC2_a_remembered_choice_wins_over_the_width', () => {
    window.localStorage.setItem(navigationPreferenceKey, 'true');
    expect(collapsedAt(1280)).toBe(true);

    window.localStorage.setItem(navigationPreferenceKey, 'false');
    expect(collapsedAt(900)).toBe(false);
  });

  it('UI_04_AC1_the_choice_is_remembered_in_this_browser', () => {
    const viewport = viewportWidth(1280);
    const { result } = renderHook(() => useNavigationPreference(), { wrapper });

    act(() => {
      result.current.setCollapsed(true);
    });

    expect(result.current.collapsed).toBe(true);
    expect(window.localStorage.getItem(navigationPreferenceKey)).toBe('true');
    viewport.restore();
  });

  it('UI_04_AC2_an_unknown_stored_value_is_ignored', () => {
    window.localStorage.setItem(navigationPreferenceKey, 'maybe');
    expect(collapsedAt(1280)).toBe(false);
  });

  it('UI_04_AC2_blocked_storage_falls_back_to_the_default_and_the_choice_still_works', () => {
    const blocked = () => {
      throw new DOMException('Site data is blocked.', 'SecurityError');
    };
    const read = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(blocked);
    const write = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(blocked);
    try {
      expect(collapsedAt(1280)).toBe(false);
      expect(collapsedAt(900)).toBe(true);

      const viewport = viewportWidth(1280);
      const { result } = renderHook(() => useNavigationPreference(), { wrapper });
      act(() => {
        result.current.setCollapsed(true);
      });
      expect(result.current.collapsed).toBe(true);
      viewport.restore();
    } finally {
      read.mockRestore();
      write.mockRestore();
    }
  });
});
