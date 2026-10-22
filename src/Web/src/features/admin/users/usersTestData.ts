import { http, HttpResponse } from 'msw';
import type {
  PagedResultOfUserListItem,
  UserDetails,
  UserListItem,
} from '../../../shared/api/generated/identity/model';

/** Test data and MSW handlers for Identity's users API, as the Gateway exposes it (/api/identity/v1/users). */
export const usersUrl = '*/api/identity/v1/users';
export const userUrl = '*/api/identity/v1/users/:id';

export const asha: UserDetails = {
  id: '0195f0c2-0000-7000-8000-000000000001',
  username: 'asha',
  displayName: 'Asha Admin',
  email: 'asha@example.test',
  mobile: null,
  roles: ['PlatformAdmin', 'User'],
  status: 'Active',
  mustChangePassword: false,
  version: 'AAAAAAAAB9E=',
};

export const ravi: UserDetails = {
  id: '0195f0c2-0000-7000-8000-000000000002',
  username: 'ravi.k',
  displayName: 'Ravi Kumar',
  email: 'ravi@example.test',
  mobile: '+91 98000 00000',
  roles: ['User'],
  status: 'Active',
  mustChangePassword: false,
  version: 'AAAAAAAAB9I=',
};

export function listItem(user: UserDetails): UserListItem {
  const { id, username, displayName, email, roles, status } = user;
  return { id, username, displayName, email, roles, status };
}

export function page(
  items: UserListItem[],
  totalCount = items.length,
  pageNumber = 1,
): PagedResultOfUserListItem {
  return { items, page: pageNumber, size: 25, totalCount, totalPages: Math.ceil(totalCount / 25) };
}

/** GET one user answers with this user. */
export const userIs = (user: UserDetails) => http.get(userUrl, () => HttpResponse.json(user));

/** An empty 204 answer, as the services send for a successful change. */
export const noContent = () => new HttpResponse(null, { status: 204 });
