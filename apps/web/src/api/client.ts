import type { ProblemDetails } from './types';

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | null;
  constructor(status: number, problem: ProblemDetails | null, message?: string) {
    super(message ?? problem?.title ?? `HTTP ${status}`);
    this.status = status;
    this.problem = problem;
  }
}

const TOKEN_KEY = 'dspc.token';
let token: string | null = null;
try {
  token = sessionStorage.getItem(TOKEN_KEY);
} catch {
  token = null;
}

const unauthorizedListeners = new Set<() => void>();
export function onUnauthorized(fn: () => void): () => void {
  unauthorizedListeners.add(fn);
  return () => unauthorizedListeners.delete(fn);
}

export function setToken(next: string | null): void {
  token = next;
  try {
    if (next) sessionStorage.setItem(TOKEN_KEY, next);
    else sessionStorage.removeItem(TOKEN_KEY);
  } catch {
    /* storage unavailable */
  }
}
export function getToken(): string | null {
  return token;
}

export function uuid(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE';
  body?: unknown;
  formData?: FormData;
  ifMatch?: string;
  query?: Record<string, string | number | boolean | undefined | null>;
  signal?: AbortSignal;
  idempotent?: boolean;
}

export interface ApiResponse<T> {
  data: T;
  etag: string | null;
  status: number;
}

export const API_BASE = '/api/v1';

export function buildUrl(path: string, query?: RequestOptions['query']): string {
  const url = path.startsWith('/api') || path.startsWith('/health') ? path : `${API_BASE}${path}`;
  if (!query) return url;
  const qs = Object.entries(query)
    .filter(([, v]) => v !== undefined && v !== null && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
    .join('&');
  return qs ? `${url}?${qs}` : url;
}

export async function requestRaw<T>(path: string, opts: RequestOptions = {}): Promise<ApiResponse<T>> {
  const method = opts.method ?? 'GET';
  const headers: Record<string, string> = {
    Accept: 'application/json',
    'X-Correlation-Id': uuid(),
  };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (opts.ifMatch) headers['If-Match'] = opts.ifMatch;
  if ((method === 'POST' || method === 'PATCH' || method === 'PUT') && opts.idempotent !== false) {
    headers['Idempotency-Key'] = uuid();
  }
  let body: BodyInit | undefined;
  if (opts.formData) body = opts.formData;
  else if (opts.body !== undefined) {
    headers['Content-Type'] = 'application/json';
    body = JSON.stringify(opts.body);
  }
  const res = await fetch(buildUrl(path, opts.query), { method, headers, body, signal: opts.signal });
  if (res.status === 401) {
    unauthorizedListeners.forEach((fn) => fn());
  }
  const etag = res.headers.get('ETag');
  const ct = res.headers.get('Content-Type') ?? '';
  if (!res.ok) {
    let problem: ProblemDetails | null = null;
    if (ct.includes('json')) {
      try {
        problem = (await res.json()) as ProblemDetails;
      } catch {
        problem = null;
      }
    }
    throw new ApiError(res.status, problem);
  }
  if (res.status === 204) return { data: undefined as T, etag, status: res.status };
  if (ct.includes('json')) return { data: (await res.json()) as T, etag, status: res.status };
  return { data: (await res.blob()) as unknown as T, etag, status: res.status };
}

export async function request<T>(path: string, opts: RequestOptions = {}): Promise<T> {
  return (await requestRaw<T>(path, opts)).data;
}

export const api = {
  get: <T>(path: string, query?: RequestOptions['query'], signal?: AbortSignal) =>
    request<T>(path, { query, signal }),
  post: <T>(path: string, body?: unknown, opts: Omit<RequestOptions, 'method' | 'body'> = {}) =>
    request<T>(path, { ...opts, method: 'POST', body }),
  patch: <T>(path: string, body?: unknown, opts: Omit<RequestOptions, 'method' | 'body'> = {}) =>
    request<T>(path, { ...opts, method: 'PATCH', body }),
  upload: <T>(path: string, formData: FormData) => request<T>(path, { method: 'POST', formData }),
};

export function isConflict(e: unknown): boolean {
  return e instanceof ApiError && e.status === 412;
}
