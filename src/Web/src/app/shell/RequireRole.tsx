import { Outlet } from 'react-router';
import { hasRole, useCurrentUser } from '../../shared/auth/currentUser';
import type { SystemRole } from '../../shared/auth/systemRoles';
import { ForbiddenPage } from '../../shared/components/ErrorPages';

/**
 * Route guard: users without the role see the 403 page instead of the screen (IAM-02-AC1). The server still
 * refuses every call; this only avoids showing a screen that cannot work. Rendered inside AppShell, so the
 * current user is already loaded.
 */
export function RequireRole({ role }: { role: SystemRole }) {
  const { data: user } = useCurrentUser();
  return user && hasRole(user, role) ? <Outlet /> : <ForbiddenPage />;
}
