import { config } from '@/config';

/** Error body the backend returns for every failure (RFC 9457 problem details). */
export type Problem = { title?: string; detail?: string; status?: number; code?: string };

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: Problem,
  ) {
    super(problem.detail ?? problem.title ?? `Request failed (${status})`);
  }

  /** 4xx errors won't succeed on retry; offline sync uses this to stop retrying bad uploads. */
  get isClientError() {
    return this.status >= 400 && this.status < 500 && this.status !== 408 && this.status !== 429;
  }
}

/** User-facing text for a failed request: the server's problem detail, or a plain "can't connect" for network failures. */
export function describeError(error: Error) {
  if (!(error instanceof ApiError)) return "Can't reach TrailHub. Check your connection and try again.";
  // 503s carry a specific reason (e.g. a map data service is down); other 5xx are internal and not worth showing.
  if (error.status >= 500 && !(error.status === 503 && error.message)) return 'TrailHub is having trouble right now. Try again in a moment.';
  return error.message;
}

type AuthHeaderProvider =() => Promise<string | null>;
let getAuthHeader: AuthHeaderProvider = async () => null;

/** The current `Authorization` header value, or null when signed out. */
export const currentAuthHeader = () => getAuthHeader();

/** Set by the session provider so code outside React (e.g. the sync queue) can call the API. */
export function setAuthHeaderProvider(provider: AuthHeaderProvider) {
  getAuthHeader = provider;
}

type RequestOptions = {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  query?: Record<string, string | number | boolean | undefined | null | (string | number)[]>;
  signal?: AbortSignal;
};

export function buildUrl(path: string, query?: RequestOptions['query']) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value === undefined || value === null || value === '') continue;
    for (const v of Array.isArray(value) ? value : [value]) params.append(key, String(v));
  }
  const qs = params.toString();
  return `${config.apiUrl}${path}${qs ? `?${qs}` : ''}`;
}

export async function apiFetch(path: string, { method = 'GET', body, query, signal }: RequestOptions = {}) {
  const headers: Record<string, string> = { Accept: 'application/json' };
  const auth = await getAuthHeader();
  if (auth) headers.Authorization = auth;

  let payload: BodyInit | undefined;
  if (body instanceof FormData) {
    payload = body;
  } else if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
    payload = JSON.stringify(body);
  }

  const response = await fetch(buildUrl(path, query), { method, headers, body: payload, signal });
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as Problem;
    throw new ApiError(response.status, problem);
  }
  return response;
}

export async function api<T>(path: string, options?: RequestOptions): Promise<T> {
  const response = await apiFetch(path, options);
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}
