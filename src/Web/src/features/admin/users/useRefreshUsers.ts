import { useQueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';
import { getListUsersQueryKey } from '../../../shared/api/generated/identity/users/users';

const usersPath = getListUsersQueryKey()[0];

/** After a change, every cached users list and user (keys start with the users path) is loaded again. */
export function useRefreshUsers(): () => Promise<void> {
  const queryClient = useQueryClient();
  return useCallback(
    () =>
      queryClient.invalidateQueries({
        predicate: (query) =>
          typeof query.queryKey[0] === 'string' && query.queryKey[0].startsWith(usersPath),
      }),
    [queryClient],
  );
}
