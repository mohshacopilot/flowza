import { ApiError } from './ApiError';

interface ProblemDetailsBody {
  detail?: unknown;
  code?: unknown;
  reference?: unknown;
  errors?: unknown;
}

const asString = (value: unknown): string | undefined =>
  typeof value === 'string' && value.length > 0 ? value : undefined;

/** ASP.NET names fields as the C# property ("DisplayName", "Address.City"); the web app uses camelCase. */
export function toFieldName(serverName: string): string {
  return serverName
    .split('.')
    .map((part) => (part.length > 0 ? part.charAt(0).toLowerCase() + part.slice(1) : part))
    .join('.');
}

function readFieldErrors(errors: unknown): Record<string, string[]> {
  if (typeof errors !== 'object' || errors === null) {
    return {};
  }
  const result: Record<string, string[]> = {};
  for (const [name, messages] of Object.entries(errors)) {
    const list = (Array.isArray(messages) ? messages : [messages]).filter(
      (m): m is string => typeof m === 'string',
    );
    if (list.length > 0) {
      (result[toFieldName(name)] ??= []).push(...list);
    }
  }
  return result;
}

/**
 * Turns a failed response into an {@link ApiError}. Reads the body as ProblemDetails when it is JSON;
 * any other body (an HTML error page from a proxy, an empty 502) still gives an error with the status.
 */
export async function apiErrorFromResponse(response: Response): Promise<ApiError> {
  let body: ProblemDetailsBody = {};
  const contentType = response.headers.get('content-type') ?? '';
  if (contentType.includes('json')) {
    try {
      const parsed: unknown = await response.json();
      if (typeof parsed === 'object' && parsed !== null) {
        body = parsed;
      }
    } catch {
      // A broken body must not hide the status.
    }
  }
  return new ApiError({
    status: response.status,
    code: asString(body.code),
    reference: asString(body.reference),
    detail: asString(body.detail),
    fieldErrors: readFieldErrors(body.errors),
  });
}
