import { z } from 'zod';
import { SystemRole } from '../../../shared/auth/systemRoles';

// The same rules as Identity's domain (IAM-04, C4), so most mistakes show before the form is sent. The server
// stays the judge: it checks everything again, and only it knows whether a username or email is already used.
// Messages are translation keys (zodResolver translates them).
const usernamePattern = /^[A-Za-z0-9._-]{3,50}$/;
const emailPattern = /^[^\s@]+@[^\s@]+$/;
const mobilePattern = /^[0-9 +\-()]*$/;

const displayName = z
  .string()
  .trim()
  .min(1, 'users.validation.displayNameRequired')
  .max(100, 'users.validation.displayNameTooLong');

const email = z
  .string()
  .trim()
  .min(1, 'users.validation.emailRequired')
  .max(256, 'users.validation.emailFormat')
  .regex(emailPattern, 'users.validation.emailFormat');

const mobile = z
  .string()
  .trim()
  .max(20, 'users.validation.mobileFormat')
  .regex(mobilePattern, 'users.validation.mobileFormat')
  .refine((value) => value === '' || /\d/.test(value), 'users.validation.mobileFormat');

/** The values an admin may change on an existing user; the username never changes (IAM-04-AC1). */
export const userProfileSchema = z.object({ displayName, email, mobile });
export type UserProfileValues = z.infer<typeof userProfileSchema>;

export const newUserSchema = userProfileSchema.extend({
  username: z
    .string()
    .trim()
    .min(1, 'users.validation.usernameRequired')
    .regex(usernamePattern, 'users.validation.usernameFormat'),
  roles: z.array(z.string()),
});
export type NewUserValues = z.infer<typeof newUserSchema>;

/** The roles an admin chooses; every user has User anyway (IAM-02), the server adds it. */
export const assignableRoles: readonly SystemRole[] = [SystemRole.PlatformAdmin, SystemRole.AppDesigner];

/** An empty optional field is sent as null, not as an empty text. */
export const emptyToNull = (value: string): string | null => (value === '' ? null : value);
