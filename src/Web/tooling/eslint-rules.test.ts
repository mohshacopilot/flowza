// @vitest-environment node
import { fileURLToPath } from 'node:url';
import { ESLint, Linter } from 'eslint';
import { describe, expect, it } from 'vitest';
import tseslint from 'typescript-eslint';
import { noHardCodedText } from '../eslint.config.js';

const webRoot = new URL('..', import.meta.url);

function lintJsx(code: string): Linter.LintMessage[] {
  const linter = new Linter({ configType: 'flat' });
  return linter.verify(
    code,
    [
      {
        files: ['**/*.tsx'],
        languageOptions: {
          parser: tseslint.parser,
          parserOptions: { ecmaFeatures: { jsx: true } },
        },
        rules: noHardCodedText,
      },
    ],
    { filename: 'Example.tsx' },
  );
}

describe('Lint rule against hard-coded screen text (constitution 12.1)', () => {
  it.each([
    ['text between tags', 'const a = <p>Save changes</p>;'],
    ['a string in braces', "const a = <p>{'Save changes'}</p>;"],
    ['a template string in braces', 'const a = <p>{`Save ${1}`}</p>;'],
    ['an aria-label', 'const a = <button aria-label="Close" />;'],
    ['a placeholder in braces', "const a = <input placeholder={'Search'} />;"],
    ['text in another script', 'const a = <p>नमस्ते</p>;'],
  ])('NFR_10_reports_%s', (_case, code) => {
    const messages = lintJsx(code);
    expect(messages.map((m) => m.ruleId)).toContain('no-restricted-syntax');
  });

  it.each([
    ['translated text', "const a = <p>{t('home.title')}</p>;"],
    ['a translated label', "const a = <button aria-label={t('shell.close')} />;"],
    ['punctuation and numbers only', 'const a = <p>{count} / 10 · –</p>;'],
    ['non-text props', 'const a = <input type="text" name="email" />;'],
  ])('NFR_10_allows_%s', (_case, code) => {
    expect(lintJsx(code)).toEqual([]);
  });

  it('NFR_10_the_rule_is_on_for_components_and_off_for_tests', async () => {
    const eslint = new ESLint({ cwd: fileURLToPath(webRoot) });
    const severityFor = async (file: string): Promise<unknown> => {
      const config = (await eslint.calculateConfigForFile(file)) as Linter.Config;
      const entry = config.rules?.['no-restricted-syntax'];
      return Array.isArray(entry) ? entry[0] : entry;
    };

    expect(await severityFor('src/features/home/HomePage.tsx')).toBe(2);
    expect(await severityFor('src/features/home/HomePage.test.tsx')).toBeUndefined();
  });
});
