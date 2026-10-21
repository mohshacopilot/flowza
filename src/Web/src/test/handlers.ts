import { http, HttpResponse } from 'msw';
import type { CurrentUser } from '../shared/auth/currentUser';
import { SystemRole } from '../shared/auth/systemRoles';

export const platformAdmin: CurrentUser = {
  displayName: 'Asha Admin',
  roles: [SystemRole.PlatformAdmin, SystemRole.User],
};

export const plainUser: CurrentUser = { displayName: 'Uma User', roles: [SystemRole.User] };

/** GET /bff/user answers with this user. */
export const currentUserIs = (user: CurrentUser) => http.get('*/bff/user', () => HttpResponse.json(user));

/** A ProblemDetails answer as the services send it (code, reference, optional field errors). */
export function problem(status: number, extra: Record<string, unknown> = {}): HttpResponse<string> {
  return new HttpResponse(JSON.stringify({ status, title: 'Problem', ...extra }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  });
}
