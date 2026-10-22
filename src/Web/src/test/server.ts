import { setupServer } from 'msw/node';

/** The fake API for component tests (MSW). Tests add their own handlers with server.use(...). */
export const server = setupServer();

/** jsdom's origin; relative API URLs resolve against it. */
export const testOrigin = 'http://localhost:3000';
