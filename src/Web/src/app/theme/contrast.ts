/**
 * WCAG 2.1 colour contrast (spec 007 UI-07, plan 007 section 5.2). All functions are O(1).
 * Colours are hex strings (#rgb or #rrggbb), the only form the design values use.
 */

/** The two text colours a coloured surface may carry; the one with the higher contrast wins. */
export interface TextColourCandidates {
  light: string;
  dark: string;
}

const hexPattern = /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i;

function channels(colour: string): [number, number, number] {
  if (!hexPattern.test(colour)) {
    throw new Error(`Not a hex colour: ${colour}`);
  }
  const digits = colour.slice(1);
  const hex = digits.length === 3 ? digits.replace(/./g, (digit) => digit + digit) : digits;
  return [0, 2, 4].map((start) => parseInt(hex.slice(start, start + 2), 16) / 255) as [
    number,
    number,
    number,
  ];
}

/** sRGB channel (0–1) to linear light, as WCAG defines it. */
function linear(channel: number): number {
  return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
}

/** WCAG relative luminance: 0 for black, 1 for white. */
export function relativeLuminance(colour: string): number {
  const [red, green, blue] = channels(colour).map(linear) as [number, number, number];
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
}

/** WCAG contrast ratio of two colours, from 1 (same) to 21 (black on white); the order does not matter. */
export function contrastRatio(first: string, second: string): number {
  const a = relativeLuminance(first);
  const b = relativeLuminance(second);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

/**
 * The text colour to use on a coloured surface: whichever candidate contrasts more. With white and black as the
 * candidates the result is at least about 4.58 : 1 for every background, so any brand colour stays readable.
 */
export function readableTextOn(background: string, candidates: TextColourCandidates): string {
  return contrastRatio(candidates.light, background) >= contrastRatio(candidates.dark, background)
    ? candidates.light
    : candidates.dark;
}
