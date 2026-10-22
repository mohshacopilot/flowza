import type { FieldError, FieldErrors, FieldValues, Resolver } from 'react-hook-form';
import type { z } from 'zod';

/**
 * Connects a Zod schema to React Hook Form (plan 001 §10). The schema's messages are translation keys; they are
 * translated here, so the form's errors already hold the text to show. Only the first problem of a field is
 * kept. A dozen lines of our own instead of a separate adapter package (constitution 1.6: no extra library).
 */
export function zodResolver<TValues extends FieldValues>(
  schema: z.ZodType<TValues, TValues>,
  translate: (key: string) => string,
): Resolver<TValues> {
  return async (values) => {
    const result = await schema.safeParseAsync(values);
    if (result.success) {
      return { values: result.data, errors: {} };
    }
    const errors: Record<string, FieldError> = {};
    for (const issue of result.error.issues) {
      const name = issue.path.map(String).join('.');
      errors[name] ??= { type: issue.code, message: translate(issue.message) };
    }
    return { values: {}, errors: errors as FieldErrors<TValues> };
  };
}
