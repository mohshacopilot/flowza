import { QueryClient } from '@tanstack/react-query';

/** Server state (TanStack Query). Lists stay fresh for 30 seconds; no refetch just because the tab got focus. */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { staleTime: 30_000, refetchOnWindowFocus: false },
    },
  });
}
