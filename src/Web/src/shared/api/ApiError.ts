/**
 * An API call that failed, built from the server's ProblemDetails (RFC 9457). The services add `code` (a stable
 * translation key), `reference` (the first 8 characters of the trace ID, PLT-05) and, for validation errors,
 * `errors` (field → messages).
 */
export class ApiError extends Error {
  /** HTTP status; 0 when the server could not be reached. */
  readonly status: number;
  /** Stable error code from the server, used as a translation key (`errors.codes.<code>`). */
  readonly code: string | undefined;
  /** Short reference code support can search the logs for. */
  readonly reference: string | undefined;
  /** English fallback text from the server, shown only when there is no translation for the code. */
  readonly detail: string | undefined;
  /** Field errors by field name (camelCase, as the web app names fields). */
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;

  constructor(init: {
    status: number;
    code?: string | undefined;
    reference?: string | undefined;
    detail?: string | undefined;
    fieldErrors?: Readonly<Record<string, readonly string[]>> | undefined;
  }) {
    super(`API call failed with status ${init.status}${init.code ? ` (${init.code})` : ''}`);
    this.name = 'ApiError';
    this.status = init.status;
    this.code = init.code;
    this.reference = init.reference;
    this.detail = init.detail;
    this.fieldErrors = init.fieldErrors ?? {};
  }
}

/** The reference code of an error, if the server gave one. */
export function referenceOf(error: unknown): string | undefined {
  return error instanceof ApiError ? error.reference : undefined;
}
