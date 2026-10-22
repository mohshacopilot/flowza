import type { TFunction } from 'i18next';
import { SystemRole } from './systemRoles';

const knownRoles = new Set<string>(Object.values(SystemRole));

/** The translated name of a system role; an unknown name (a newer server) is shown as it is. */
export function roleLabel(t: TFunction, role: string): string {
  return knownRoles.has(role) ? t(`roles.${role as SystemRole}`) : role;
}
