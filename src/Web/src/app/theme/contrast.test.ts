import { contrastRatio, readableTextOn, relativeLuminance } from './contrast';
import designValues from './designValues.json';

const onColour = designValues.onColour;

describe('Colour contrast (WCAG 2.1)', () => {
  it.each([
    ['#000000', 0],
    ['#ffffff', 1],
    ['#fff', 1],
    ['#FFFFFF', 1],
  ])('relative_luminance_of_%s_is_%s', (colour, expected) => {
    expect(relativeLuminance(colour)).toBeCloseTo(expected, 6);
  });

  it('black_on_white_is_21_to_1_in_either_order', () => {
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 6);
    expect(contrastRatio('#ffffff', '#000000')).toBeCloseTo(21, 6);
  });

  it('a_colour_on_itself_is_1_to_1', () => {
    expect(contrastRatio('#1f5fbf', '#1f5fbf')).toBeCloseTo(1, 6);
  });

  it('matches_a_known_reference_value', () => {
    // #777777 on white is the classic "just below AA" grey: 4.48 : 1.
    expect(contrastRatio('#777777', '#ffffff')).toBeCloseTo(4.48, 2);
  });

  it.each(['', '1f5fbf', '#12345', '#ggg', 'rgb(0, 0, 0)'])('rejects_%s_as_not_a_hex_colour', (colour) => {
    expect(() => relativeLuminance(colour)).toThrow();
  });

  it('chooses_white_on_dark_and_black_on_light_surfaces', () => {
    expect(readableTextOn('#14294d', onColour)).toBe(onColour.light);
    expect(readableTextOn('#fff8e1', onColour)).toBe(onColour.dark);
  });

  it('the_brand_colour_carries_white_text', () => {
    expect(readableTextOn(designValues.brand, onColour)).toBe(onColour.light);
  });

  // Light, mid and dark brand colours, pure primaries, the greys around the crossover point (where white and black
  // contrast equally, the worst case) and a colour close to the navigation's navy.
  const brandColours = [
    '#ffffff',
    '#fff8e1',
    '#ffd27f',
    '#00ff00',
    '#ffff00',
    '#00ffff',
    '#808080',
    '#777777',
    '#767676',
    '#7a7a7a',
    '#5b9bd5',
    '#ff0000',
    '#ff00ff',
    '#c62828',
    '#2e7d32',
    '#a35a00',
    designValues.brand,
    '#1e3a8a',
    '#14294d',
    '#16325c',
    '#0000ff',
    '#000000',
  ];

  it.each(brandColours)('UI_07_AC1_text_on_brand_colour_%s_reaches_4_5_to_1', (brand) => {
    expect(contrastRatio(readableTextOn(brand, onColour), brand)).toBeGreaterThanOrEqual(4.5);
  });

  it('UI_07_AC1_every_grey_from_black_to_white_reaches_4_5_to_1', () => {
    for (let level = 0; level <= 255; level++) {
      const hex = level.toString(16).padStart(2, '0');
      const grey = `#${hex}${hex}${hex}`;
      expect(contrastRatio(readableTextOn(grey, onColour), grey)).toBeGreaterThanOrEqual(4.5);
    }
  });

  it('a_dark_candidate_lighter_than_black_cannot_promise_4_5_to_1', () => {
    // Why the on-colour "dark" value is black and not the body text colour: with #1b2333 a mid grey only gets ~4 : 1.
    const withBodyText = { light: onColour.light, dark: designValues.neutral.text };
    expect(contrastRatio(readableTextOn('#7a7a7a', withBodyText), '#7a7a7a')).toBeLessThan(4.5);
  });
});
