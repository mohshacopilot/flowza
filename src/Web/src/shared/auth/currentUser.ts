import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { apiErrorFromResponse } from '../api/problemDetails';
import { bffEndpoints } from './session';
import type { SystemRole } from './systemRoles';

/** The logged-in user as the Gateway reports it (GET /bff/user): display name and system roles only. */
export interface CurrentUser {
  displayName: string;
  roles: readonly string[];
}

export const currentUserQueryKey = ['bff', 'user'] as const;

export async function fetchCurrentUser(): Promise<CurrentUser> {
  const response = await fetch(bffEndpoints.user, {
    credentials: 'same-origin',
    headers: { Accept: 'application/json' },
  });
  if (!response.ok) {
    throw await apiErrorFromResponse(response);
  }
  return (await response.json()) as CurrentUser;
}

export function useCurrentUser(): UseQueryResult<CurrentUser> {
  return useQuery({ queryKey: currentUserQueryKey, queryFn: fetchCurrentUser, staleTime: 5 * 60_000 });
}

export function hasRole(user: CurrentUser, role: SystemRole): boolean {
  return user.roles.includes(role);
}
