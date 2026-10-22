import type { i18n as I18n, ParseKeys } from 'i18next';
import { ApiError } from './ApiError';

/** What a screen shows for a failed call: one message, the reference code and messages per form field. */
export interface ApiErrorDescription {
  message: string;
  reference: string | undefined;
  fieldErrors: Record<string, string>;
}

const statusKeys: Readonly<Record<number, ParseKeys>> = {
  0: 'errors.api.network',
  400: 'errors.api.validation',
  401: 'errors.api.unauthenticated',
  403: 'errors.api.forbidden',
  404: 'errors.api.notFound',
  409: 'errors.api.conflict',
  413: 'errors.api.tooLarge',
  429: 'errors.api.tooManyRequests',
};

/** A server code (or message) is translated when locales/en has `errors.codes.<code>`. */
function translatedCode(i18n: I18n, code: string | undefined): string | undefined {
  if (!code) {
    return undefined;
  }
  // Codes come from the server at run time, so the key cannot be checked at compile time; exists() guards it.
  const key = `errors.codes.${code}` as ParseKeys;
  if (!i18n.exists(key)) {
    return undefined;
  }
  const text: unknown = i18n.t(key);
  return typeof text === 'string' ? text : undefined;
}

function statusMessage(i18n: I18n, status: number): string {
  const key = statusKeys[status] ?? (status >= 500 ? 'errors.api.server' : 'errors.api.unexpected');
  return i18n.t(key);
}

/**
 * Turns any error from {@link apiFetch} into text for the screen: the translation of the server's error code;
 * else, for business errors (4xx), the server's English fallback text; else a message for the status.
 * Field errors are keyed by the web app's field names, ready for the form (React Hook Form's setError).
 */
export function describeApiError(error: unknown, i18n: I18n): ApiErrorDescription {
  if (!(error instanceof ApiError)) {
    return { message: i18n.t('errors.api.unexpected'), reference: undefined, fieldErrors: {} };
  }

  const fallback =
    error.status >= 400 && error.status < 500 && error.status !== 401 && error.status !== 403
      ? error.detail
      : undefined;
  const message = translatedCode(i18n, error.code) ?? fallback ?? statusMessage(i18n, error.status);

  const fieldErrors: Record<string, string> = {};
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    fieldErrors[field] = messages.map((m) => translatedCode(i18n, m) ?? m).join(' ');
  }

  return { message, reference: error.reference, fieldErrors };
}
