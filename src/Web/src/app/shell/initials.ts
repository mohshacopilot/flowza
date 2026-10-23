const letters = new Intl.Segmenter(undefined, { granularity: 'grapheme' });

/** The first `count` letters as the reader sees them (a Hindi "भा" is one letter, as C# StringInfo counts it). */
function firstLetters(word: string, count: number): string {
  let result = '';
  let taken = 0;
  for (const { segment } of letters.segment(word)) {
    if (taken === count) {
      break;
    }
    result += segment;
    taken++;
  }
  return result;
}

/**
 * Initials of a display name for the user's avatar, by the same rule as the installation mark (spec 007 C6,
 * Identity's InstallationBrand): one word → its first two letters; two or more → the first letter of each of the
 * first two words; upper case. An empty name gives "". O(n) in the name's length.
 */
export function initialsOf(name: string): string {
  const words = name
    .trim()
    .split(/\s+/)
    .filter((word) => word.length > 0);
  const [first, second] = words;
  if (first === undefined) {
    return '';
  }
  const initials =
    second === undefined ? firstLetters(first, 2) : firstLetters(first, 1) + firstLetters(second, 1);
  return initials.toUpperCase();
}
