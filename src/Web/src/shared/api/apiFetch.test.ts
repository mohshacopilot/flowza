import { http, HttpResponse } from 'msw';
import { problem } from '../../test/handlers';
import { server } from '../../test/server';
import { ApiError } from './ApiError';
import { antiForgeryHeader, apiFetch } from './apiFetch';

/** Records the request the server received, then answers with `response`. */
function capture(method: 'get' | 'post' | 'put' | 'patch' | 'delete', response: () => Response) {
  const seen: { request?: Request } = {};
  server.use(
    http[method]('*/api/identity/v1/things', ({ request }) => {
      seen.request = request;
      return response();
    }),
  );
  return seen;
}

describe('apiFetch', () => {
  it.each(['post', 'put', 'patch', 'delete'] as const)(
    'FE5_adds_the_anti_forgery_header_to_%s',
    async (method) => {
      const seen = capture(method, () => new HttpResponse(null, { status: 204 }));

      await apiFetch('/api/identity/v1/things', { method: method.toUpperCase() });

      expect(seen.request?.headers.get(antiForgeryHeader.name)).toBe(antiForgeryHeader.value);
    },
  );

  it('FE5_does_not_add_the_anti_forgery_header_to_reads', async () => {
    const seen = capture('get', () => HttpResponse.json([]));

    await apiFetch('/api/identity/v1/things');

    expect(seen.request?.headers.get(antiForgeryHeader.name)).toBeNull();
    expect(seen.request?.headers.get('Accept')).toBe('application/json');
  });

  it('FE5_returns_the_JSON_body', async () => {
    capture('get', () => HttpResponse.json({ id: '1', name: 'one' }));

    await expect(apiFetch('/api/identity/v1/things')).resolves.toEqual({ id: '1', name: 'one' });
  });

  it('FE5_returns_undefined_for_204_no_content', async () => {
    capture('post', () => new HttpResponse(null, { status: 204 }));

    await expect(apiFetch('/api/identity/v1/things', { method: 'POST' })).resolves.toBeUndefined();
  });

  it('FE5_keeps_caller_headers_and_body', async () => {
    const seen = capture('post', () => HttpResponse.json({ ok: true }));

    await apiFetch('/api/identity/v1/things', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: 'x' }),
    });

    expect(seen.request?.headers.get('Content-Type')).toBe('application/json');
    expect(await seen.request?.json()).toEqual({ name: 'x' });
  });

  it('PLT_05_AC2_turns_problem_details_into_an_ApiError_with_code_and_reference', async () => {
    capture('get', () =>
      problem(409, { code: 'concurrency', detail: 'Changed by someone else.', reference: 'abcd1234' }),
    );

    const error = await apiFetch('/api/identity/v1/things').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: 409,
      code: 'concurrency',
      detail: 'Changed by someone else.',
      reference: 'abcd1234',
    });
  });

  it('FE5_validation_problem_gives_field_errors_by_camelCase_field_name', async () => {
    capture('post', () =>
      problem(400, {
        code: 'validation',
        reference: '12345678',
        errors: { DisplayName: ['Display name is required.'], 'Address.City': ['Too long.', 'Bad letters.'] },
      }),
    );

    const error = await apiFetch('/api/identity/v1/things', { method: 'POST' }).catch((e: unknown) => e);

    expect((error as ApiError).fieldErrors).toEqual({
      displayName: ['Display name is required.'],
      'address.city': ['Too long.', 'Bad letters.'],
    });
  });

  it('FE5_a_non_JSON_error_body_still_gives_the_status', async () => {
    capture(
      'get',
      () =>
        new HttpResponse('<html>Bad gateway</html>', {
          status: 502,
          headers: { 'Content-Type': 'text/html' },
        }),
    );

    const error = await apiFetch('/api/identity/v1/things').catch((e: unknown) => e);

    expect(error).toMatchObject({ status: 502, code: undefined, reference: undefined, fieldErrors: {} });
  });

  it('FE5_a_network_failure_gives_status_0', async () => {
    capture('get', () => HttpResponse.error());

    const error = await apiFetch('/api/identity/v1/things').catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(0);
  });
});
