import 'i18next';
import type { defaultNamespace, resources } from './i18n';

// Typed translation keys: t('…') with a key that is not in locales/en fails the type check.
declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: typeof defaultNamespace;
    resources: (typeof resources)['en'];
    returnNull: false;
  }
}
