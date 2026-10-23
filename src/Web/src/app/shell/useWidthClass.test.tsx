import { renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { TestProviders } from '../../test/render';
import { viewportWidth } from '../../test/viewport';
import { useWidthClass } from './useWidthClass';

// The real theme carries our breakpoints (tablet 768, desktop 1025), so the providers are needed.
const wrapper = ({ children }: { children: ReactNode }) => <TestProviders>{children}</TestProviders>;

function widthClassesAt(width: number): string[] {
  const viewport = viewportWidth(width);
  const seen: string[] = [];
  renderHook(
    () => {
      seen.push(useWidthClass());
    },
    { wrapper },
  ).unmount();
  viewport.restore();
  return seen;
}

describe('Width class (plan 007 §5.5)', () => {
  it.each([
    [375, 'phone'],
    [767, 'phone'],
    [768, 'tablet'],
    [1024, 'tablet'],
    [1025, 'desktop'],
    [1280, 'desktop'],
  ])('UI_05_at_%i_px_the_layout_is_%s_from_the_very_first_render', (width, expected) => {
    const seen = widthClassesAt(width);

    expect(seen.length).toBeGreaterThan(0);
    expect(new Set(seen)).toEqual(new Set([expected]));
  });
});
