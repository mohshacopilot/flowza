import { MutationCache, QueryCache, QueryClient, type DefaultOptions } from '@tanstack/react-query';
import { ApiError } from '../shared/api/ApiError';
import { redirectToLogin } from '../shared/auth/session';

const isClientError = (error: unknown): boolean =>
  error instanceof ApiError && error.status >= 400 && error.status < 500;

/** An ended session (401) on any call sends the browser to the login page, once, from one place. */
function onApiError(error: unknown): void {
  if (error instanceof ApiError && error.status === 401) {
    redirectToLogin();
  }
}

/**
 * Server state (TanStack Query). Lists stay fresh for 30 seconds; no refetch just because the tab got focus.
 * Network and server errors are retried twice; client errors (4xx) never, as the answer would not change.
 */
export function createQueryClient(overrides: DefaultOptions = {}): QueryClient {
  return new QueryClient({
    queryCache: new QueryCache({ onError: onApiError }),
    mutationCache: new MutationCache({ onError: onApiError }),
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        retry: (failureCount, error) => !isClientError(error) && failureCount < 2,
        ...overrides.queries,
      },
      mutations: { ...overrides.mutations },
    },
  });
}
