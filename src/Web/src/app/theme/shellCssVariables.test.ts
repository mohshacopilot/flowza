import designValues from './designValues.json';
import { shellCssVariables } from './shellCssVariables';

// That the navigation colours carry the login pages' names and values is checked with the generated CSS
// (tooling/designValues.test.ts).
describe('Frame CSS variables from the design values', () => {
  const variables = shellCssVariables(designValues) as Record<string, string>;

  it('UI_02_every_navigation_colour_becomes_a_variable', () => {
    expect(variables['--flowza-navigation-background']).toBe(designValues.navigation.background);
    expect(variables['--flowza-navigation-text-active']).toBe(designValues.navigation.textActive);
    expect(variables['--flowza-navigation-focus']).toBe(designValues.navigation.focus);
  });

  it('UI_02_the_header_height_comes_from_the_design_values', () => {
    expect(variables['--flowza-shell-header-height']).toBe(`${String(designValues.layout.headerHeight)}px`);
  });
});
