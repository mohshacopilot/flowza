import { http, HttpResponse } from 'msw';
import type { InstallationView } from '../shared/api/generated/identity/model';
import type { CurrentUser } from '../shared/auth/currentUser';
import { SystemRole } from '../shared/auth/systemRoles';

export const platformAdmin: CurrentUser = {
  displayName: 'Asha Admin',
  roles: [SystemRole.PlatformAdmin, SystemRole.User],
};

export const plainUser: CurrentUser = { displayName: 'Uma User', roles: [SystemRole.User] };

/** GET /bff/user answers with this user. */
export const currentUserIs = (user: CurrentUser) => http.get('*/bff/user', () => HttpResponse.json(user));

export const installationUrl = '*/api/identity/v1/installation';

/** The installation every test sees unless it says otherwise (Identity's default, C7). */
export const defaultInstallation: InstallationView = { name: 'Flowza', initials: 'FL' };

/** GET /api/identity/v1/installation answers with this name and initials (UI-08). */
export const installationIs = (installation: InstallationView) =>
  http.get(installationUrl, () => HttpResponse.json(installation));

/** A ProblemDetails answer as the services send it (code, reference, optional field errors). */
export function problem(status: number, extra: Record<string, unknown> = {}): HttpResponse<string> {
  return new HttpResponse(JSON.stringify({ status, title: 'Problem', ...extra }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}
