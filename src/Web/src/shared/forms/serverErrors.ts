import type { i18n as I18n } from 'i18next';
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { describeApiError } from '../api/errorMessages';

/**
 * Shows a failed save on the form: each field error the server named goes next to its field (when the form has
 * that field); the overall message and reference code are returned for an alert above the form.
 */
export function applyServerErrors<TValues extends FieldValues>(
  error: unknown,
  i18n: I18n,
  fields: readonly Path<TValues>[],
  setError: UseFormSetError<TValues>,
): { message: string; reference: string | undefined } {
  const described = describeApiError(error, i18n);
  for (const field of fields) {
    const message = described.fieldErrors[field];
    if (message !== undefined) {
      setError(field, { type: 'server', message });
    }
  }
  return { message: described.message, reference: described.reference };
}
