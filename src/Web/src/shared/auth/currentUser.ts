import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { apiFetch } from '../api/apiFetch';
import { bffEndpoints } from './session';
import type { SystemRole } from './systemRoles';

/** The logged-in user as the Gateway reports it (GET /bff/user): display name and system roles only. */
export interface CurrentUser {
  displayName: string;
  roles: readonly string[];
}

export const currentUserQueryKey = ['bff', 'user'] as const;

export function fetchCurrentUser(signal?: AbortSignal): Promise<CurrentUser> {
  return apiFetch<CurrentUser>(bffEndpoints.user, signal ? { signal } : {});
}

export function useCurrentUser(): UseQueryResult<CurrentUser> {
  return useQuery({
    queryKey: currentUserQueryKey,
    queryFn: ({ signal }) => fetchCurrentUser(signal),
    staleTime: 5 * 60_000,
  });
}

export function hasRole(user: CurrentUser, role: SystemRole): boolean {
  return user.roles.includes(role);
}
