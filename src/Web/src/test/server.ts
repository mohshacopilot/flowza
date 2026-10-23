import { setupServer } from 'msw/node';
import { defaultInstallation, installationIs } from './handlers';

/**
 * The fake API for component tests (MSW). Tests add their own handlers with server.use(...). The frame of every
 * screen asks for the installation's name (UI-08), so that one is answered by default; resetHandlers keeps it.
 */
export const server = setupServer(installationIs(defaultInstallation));

/** jsdom's origin; relative API URLs resolve against it. */
export const testOrigin = 'http://localhost:3000';
