import js from '@eslint/js';
import prettier from 'eslint-config-prettier';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import { defineConfig, globalIgnores } from 'eslint/config';
import globals from 'globals';
import tseslint from 'typescript-eslint';

// Properties whose value is shown to people (or read out by screen readers), so they must come from the
// translation files like any other text (constitution 12.1, NFR-10).
const textProps = [
  'alt',
  'title',
  'label',
  'placeholder',
  'aria-label',
  'aria-description',
  'aria-placeholder',
  'aria-roledescription',
  'aria-valuetext',
  'subTitle',
  'description',
  'message',
  'tooltip',
  'okText',
  'cancelText',
];
const textPropPattern = `/^(${textProps.join('|')})$/`;
const hardCodedTextMessage =
  'Screen text must come from the translation files (src/locales): use t("…") (constitution 12.1).';

/** A string literal counts as text when it contains a letter in any script. */
const lettersPattern = String.raw`/\p{L}/u`;

/** @type {import('eslint').Linter.RulesRecord} */
export const noHardCodedText = {
  'no-restricted-syntax': [
    'error',
    { selector: `JSXText[value=${lettersPattern}]`, message: hardCodedTextMessage },
    {
      selector: `JSXElement > JSXExpressionContainer > Literal[value=${lettersPattern}]`,
      message: hardCodedTextMessage,
    },
    {
      selector: `JSXElement > JSXExpressionContainer > TemplateLiteral`,
      message: hardCodedTextMessage,
    },
    {
      selector: `JSXAttribute[name.name=${textPropPattern}] > Literal[value=${lettersPattern}]`,
      message: hardCodedTextMessage,
    },
    {
      selector: `JSXAttribute[name.name=${textPropPattern}] > JSXExpressionContainer > Literal[value=${lettersPattern}]`,
      message: hardCodedTextMessage,
    },
  ],
};

export default defineConfig([
  globalIgnores(['dist', 'coverage', 'node_modules', 'src/shared/api/generated', '.orval-check']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.strictTypeChecked,
      reactHooks.configs.flat['recommended-latest'],
      reactRefresh.configs.vite,
      jsxA11y.flatConfigs.strict,
    ],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/consistent-type-imports': 'error',
      '@typescript-eslint/restrict-template-expressions': ['error', { allowNumber: true }],
      'no-console': ['error', { allow: ['warn', 'error'] }],
    },
  },
  {
    files: ['src/**/*.tsx'],
    ignores: ['src/**/*.test.tsx', 'src/test/**'],
    rules: noHardCodedText,
  },
  {
    files: ['src/**/*.test.{ts,tsx}', 'src/test/**'],
    rules: {
      // Tests may build components inline and use plain example strings.
      'react-refresh/only-export-components': 'off',
    },
  },
  {
    files: ['*.config.{js,ts}'],
    languageOptions: { globals: globals.node },
  },
  {
    files: ['**/*.js'],
    extends: [tseslint.configs.disableTypeChecked],
  },
  prettier,
]);
