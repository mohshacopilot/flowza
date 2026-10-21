import type { RouteObject } from 'react-router';

/** The route table. Feature areas are loaded lazily, so each one is its own bundle. */
export const routes: RouteObject[] = [
  {
    index: true,
    lazy: async () => ({ Component: (await import('../features/home/HomePage')).HomePage }),
  },
];
