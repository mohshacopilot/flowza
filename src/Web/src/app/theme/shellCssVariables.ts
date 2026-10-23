import type { CSSProperties } from 'react';
import type { DesignValues } from './antdTheme';

const kebab = (name: string): string => name.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`);

/**
 * The navigation colours Ant Design has no token for (focus ring on dark, the selected item's bar, the panel's
 * lines) as CSS variables for the frame's layout CSS (UI-02). They carry the same names as on the login pages
 * (design-values.css: --flowza-navigation-focus, …), so both parts read the deep teal panel the same way.
 */
export function shellCssVariables(values: DesignValues): CSSProperties {
  const navigation = Object.entries(values.navigation).map(([name, value]) => [
    `--flowza-navigation-${kebab(name)}`,
    value,
  ]);
  return Object.fromEntries([
    ...navigation,
    ['--flowza-shell-header-height', `${values.layout.headerHeight}px`],
  ]) as CSSProperties;
}
