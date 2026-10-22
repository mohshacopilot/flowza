import { defineConfig } from 'orval';
import { apiProject, readOpenApiDocument, toGatewayDocument } from './tooling/openapi.ts';

// `npm run api:check`: generates a client from a small sample document the way the real services are
// generated, then type-checks it with apiFetch. Proves the generator set-up compiles before a service has an
// API (and keeps proving it after orval upgrades). Output in .orval-check/ (not committed).
export default defineConfig({
  sample: apiProject(
    toGatewayDocument(readOpenApiDocument('tooling/fixtures/sample.openapi.json'), 'sample'),
    '.orval-check/sample',
  ),
});
