// `npm run design:css`: writes the login pages' design-values.css in Identity from designValues.json (spec 007
// UI-02, PA4). Run it after every change to the design values and commit the result; the drift test fails otherwise.
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import type { DesignValues } from '../src/app/theme/antdTheme.ts';
import { accountCssFile, renderAccountCss } from './accountCss.ts';

const values = JSON.parse(
  readFileSync(new URL('../src/app/theme/designValues.json', import.meta.url), 'utf8'),
) as DesignValues;

writeFileSync(accountCssFile, renderAccountCss(values));
console.warn(`Wrote ${fileURLToPath(accountCssFile)}`);
