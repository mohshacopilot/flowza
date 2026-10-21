// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { services, toGatewayDocument, type OpenApiDocumentLike } from './openapi.ts';

const document = (paths: Record<string, unknown>): OpenApiDocumentLike => ({
  openapi: '3.1.1',
  info: { title: 'x', version: '1' },
  servers: [{ url: 'http://localhost:5101/' }],
  paths,
});

describe('OpenAPI documents for the Gateway (FE5)', () => {
  it('FE5_service_paths_are_rewritten_to_the_gateway_route', () => {
    const result = toGatewayDocument(
      document({ '/api/v1/users': { get: {} }, '/api/v1/users/{id}': { put: {} } }),
      'identity',
    );

    expect(Object.keys(result.paths ?? {})).toEqual([
      '/api/identity/v1/users',
      '/api/identity/v1/users/{id}',
    ]);
    expect(result.paths?.['/api/identity/v1/users/{id}']).toEqual({ put: {} });
  });

  it('FE5_servers_are_removed_so_calls_stay_on_the_gateway_origin', () => {
    const result = toGatewayDocument(document({}), 'identity');

    expect(result).not.toHaveProperty('servers');
    expect(result.info).toEqual({ title: 'x', version: '1' });
  });

  it('FE5_paths_outside_api_are_kept', () => {
    const result = toGatewayDocument(document({ '/health/ready': { get: {} } }), 'identity');

    expect(Object.keys(result.paths ?? {})).toEqual(['/health/ready']);
  });

  it('FE5_a_document_without_paths_gives_no_paths', () => {
    expect(toGatewayDocument({ openapi: '3.1.1' }, 'design').paths).toEqual({});
  });

  it('FE5_the_original_document_is_not_changed', () => {
    const original = document({ '/api/v1/users': {} });

    toGatewayDocument(original, 'identity');

    expect(Object.keys(original.paths ?? {})).toEqual(['/api/v1/users']);
    expect(original.servers).toBeDefined();
  });

  it('FE5_every_service_behind_the_gateway_has_a_fixed_development_port', () => {
    expect(services.map((s) => `${s.name}:${s.port}`)).toEqual([
      'identity:5101',
      'design:5102',
      'runtime:5103',
      'worker:5104',
    ]);
  });
});
