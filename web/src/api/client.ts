import type { Culture } from '../i18n/types';

const BASE_PATH = '/api';

/**
 * کلاینت پایه‌ی ارتباط با API.
 * همه‌ی درخواست‌ها از طریق پروکسی Vite (توسعه) یا همان مبدأ (تولید) ارسال می‌شوند.
 */

export class ApiError extends Error {
  constructor(public readonly status: number) {
    super(`درخواست API با وضعیت ${status} شکست خورد`);
    this.name = 'ApiError';
  }
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  signal?: AbortSignal;
  antiforgeryToken?: string;
}

/**
 * یک درخواست JSON به /api/{culture}/... ارسال می‌کند.
 * خطاها به‌صورت ApiError پرتاب می‌شوند تا لایه‌ی فراخوان آن‌ها را مدیریت کند.
 */
export async function apiRequest<T>(
  culture: Culture,
  path: string,
  options: RequestOptions = {}
): Promise<T> {
  const headers: Record<string, string> = {
    Accept: 'application/json'
  };

  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  if (options.antiforgeryToken) {
    headers['X-CSRF-TOKEN'] = options.antiforgeryToken;
  }

  const response = await fetch(`${BASE_PATH}/${culture}${path}`, {
    method: options.method ?? 'GET',
    credentials: 'same-origin',
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: options.signal
  });

  if (!response.ok) {
    throw new ApiError(response.status);
  }

  if (response.status === StatusCodes.NoContent) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

const StatusCodes = { NoContent: 204 } as const;
