import { initialsOf } from './initials';

// The same cases as Identity's InstallationBrandTests (C6), so the avatar and the installation mark agree.
describe('Initials of a display name (C6)', () => {
  it.each([
    ['State Bank', 'SB'],
    ['State Bank of Example', 'SB'],
    ['Flowza', 'FL'],
    ['X', 'X'],
    ['acme bank', 'AB'],
    ['  Acme \t  Bank  ', 'AB'],
    ['भारतीय सेवा', 'भासे'],
    ['भारत', 'भार'],
    ['தமிழ் நாடு', 'தநா'],
    ['école Lumière', 'ÉL'],
    ['Asha Admin', 'AA'],
  ])('UI_03_AC2_initials_of_%s_are_%s', (name, initials) => {
    expect(initialsOf(name)).toBe(initials);
  });

  it('UI_03_AC2_a_letter_with_a_combining_accent_stays_whole', () => {
    // "e" + combining acute accent (two code points, one letter).
    expect(initialsOf('émile')).toBe('ÉM');
  });

  it.each(['', '   ', '\t\n'])('an_empty_name_has_no_initials', (name) => {
    expect(initialsOf(name)).toBe('');
  });
});
