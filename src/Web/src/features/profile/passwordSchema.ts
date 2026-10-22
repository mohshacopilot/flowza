import { z } from 'zod';

/** Fixed in M1 (IAM-06, K2); becomes an installation setting in M3 (T040). The server checks it again. */
export const passwordMinimumLength = 10;

// Messages are translation keys (zodResolver translates them). Only what the browser can know is checked here;
// "not a common password" and "not the username" are the server's checks (IAM-06-AC1).
export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'profile.validation.currentRequired'),
    newPassword: z.string().min(passwordMinimumLength, 'profile.validation.newTooShort'),
    confirmPassword: z.string(),
  })
  .refine((values) => values.confirmPassword === values.newPassword, {
    message: 'profile.validation.confirmMismatch',
    path: ['confirmPassword'],
  })
  .refine((values) => values.currentPassword === '' || values.newPassword !== values.currentPassword, {
    message: 'profile.validation.sameAsCurrent',
    path: ['newPassword'],
  });

export type ChangePasswordValues = z.infer<typeof changePasswordSchema>;
