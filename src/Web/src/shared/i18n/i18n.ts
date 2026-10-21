import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import common from '../../locales/en/common.json';

/** All languages ship as JSON files under src/locales/<language>/ (tech discussion T10, FE3). English only in v0.1. */
export const resources = {
  en: { common },
} as const;

export const defaultNamespace = 'common';

/** Creates the i18next instance. Tests call it too, so they see the real English text. */
export function initI18n(language = 'en'): typeof i18n {
  if (!i18n.isInitialized) {
    void i18n.use(initReactI18next).init({
      resources,
      lng: language,
      fallbackLng: 'en',
      defaultNS: defaultNamespace,
      ns: [defaultNamespace],
      interpolation: { escapeValue: false },
      returnNull: false,
      initAsync: false,
    });
  }
  return i18n;
}
