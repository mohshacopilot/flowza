import type { ParseKeys } from 'i18next';
import { useMemo } from 'react';
import type { FieldValues, Resolver } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { z } from 'zod';
import { zodResolver } from './zodResolver';

/** {@link zodResolver} with the app's translations; schema messages are keys under locales/en. */
export function useZodResolver<TValues extends FieldValues>(
  schema: z.ZodType<TValues, TValues>,
): Resolver<TValues> {
  const { i18n } = useTranslation();
  // Schema messages are keys written in code, not checked by the compiler, so they are cast here once.
  return useMemo(() => zodResolver(schema, (key) => i18n.t(key as ParseKeys)), [schema, i18n]);
}
