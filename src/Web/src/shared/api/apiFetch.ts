import { ApiError } from './ApiError';
import { apiErrorFromResponse } from './problemDetails';

/**
 * Anti-forgery header (tech discussion SE5). The Gateway refuses POST/PUT/PATCH/DELETE without it. A page on
 * another site cannot add a custom header to a request without the Gateway's CORS permission, which it never
 * gives, and the session cookie is SameSite=Strict on top.
 */
export const antiForgeryHeader = { name: 'X-CSRF', value: '1' } as const;

/**
 * orval reads this export: the generated hooks then type their errors as {@link ApiError} (what apiFetch
 * throws) instead of the raw ProblemDetails model of the OpenAPI document.
 */
export type ErrorType<_ServerError> = ApiError;

const safeMethods = new Set(['GET', 'HEAD', 'OPTIONS']);

/** Relative URLs resolve against the page, so the Gateway (one origin for app and API) always receives them. */
function toAbsoluteUrl(url: string): string {
  return new URL(url, window.location.origin).toString();
}

async function readBody(response: Response): Promise<unknown> {
  if (response.status === 204 || response.status === 205) {
    return undefined;
  }
  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('json')) {
    const text = await response.text();
    return text.length > 0 ? (JSON.parse(text) as unknown) : undefined;
  }
  if (contentType.startsWith('text/')) {
    return response.text();
  }
  return response.blob();
}

/**
 * The one fetch function of the web app (tech discussion T10, FE5); the orval-generated hooks call it for
 * every request. It sends the session cookie, adds the anti-forgery header to changes, and turns every failure
 * into an {@link ApiError} (ProblemDetails: code, reference, field errors; status 0 when the network failed).
 */
export async function apiFetch<T>(url: string, options: RequestInit = {}): Promise<T> {
  const method = (options.method ?? 'GET').toUpperCase();
  const headers = new Headers(options.headers);
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }
  if (!safeMethods.has(method)) {
    headers.set(antiForgeryHeader.name, antiForgeryHeader.value);
  }

  let response: Response;
  try {
    response = await fetch(toAbsoluteUrl(url), { ...options, method, headers, credentials: 'same-origin' });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error; // a cancelled query is not an error to show
    }
    throw new ApiError({ status: 0 });
  }

  if (!response.ok) {
    throw await apiErrorFromResponse(response);
  }
  return (await readBody(response)) as T;
}

export default apiFetch;
