import type { Culture } from '../i18n/types';

const BASE_PATH = '/api';

/**
 * کلاینت پایه‌ی ارتباط با API.
 * همه‌ی درخواست‌ها از طریق پروکسی Vite (توسعه) یا همان مبدأ (تولید) ارسال می‌شوند.
 *
 * این ماژول توکن دسترسی را به‌صورت خودکار به همه‌ی درخواست‌های احراز هویت‌شده
 * اضافه می‌کند و در صورت دریافت ۴۰۱ یک‌بار تلاش می‌کند توکن را تازه‌سازی کند و
 * درخواست را تکرار نماید.
 */

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  /** کد خطای مستقل از زبان که سمت سرور در extensions قرار می‌دهد. */
  extensions?: Record<string, string>;
  /** فیلدهای نامعتبر (فقط برای ValidationProblemDetails). */
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: ProblemDetails = {}
  ) {
    super(problem.detail ?? `درخواست API با وضعیت ${status} شکست خورد`);
    this.name = 'ApiError';
  }

  /** کد خطای مستقل از زبان (مثلاً «invalid_credentials») یا نامشخص. */
  get code(): string {
    return this.problem.extensions?.code ?? 'unknown_error';
  }

  get validationErrors(): Record<string, string[]> {
    return this.problem.errors ?? {};
  }
}

interface RequestOptions {
  method?: string;
  body?: unknown;
  signal?: AbortSignal;
  /** اگر true باشد، توکن احراز هویت به این درخواست اضافه نمی‌شود. */
  anonymous?: boolean;
  /** اگر true باشد، در صورت ۴۰۱ تلاش برای تازه‌سازی توکن انجام نمی‌شود. */
  skipRefresh?: boolean;
}

type TokenSupplier = () => string | null;
type RefreshHandler = () => Promise<string | null>;

let tokenSupplier: TokenSupplier | null = null;
let refreshHandler: RefreshHandler | null = null;

/**
 * ثبت منابع توکن توسط AuthProvider. کلاینت به این‌صورت به نشست وابسته است
 * تا وابستگی چرخه‌ای بین لایه‌ها ایجاد نشود.
 */
export function configureAuth(supplier: TokenSupplier, refresh: RefreshHandler): void {
  tokenSupplier = supplier;
  refreshHandler = refresh;
}

export function clearAuth(): void {
  tokenSupplier = null;
  refreshHandler = null;
}

function buildHeaders(options: RequestOptions): Record<string, string> {
  const headers: Record<string, string> = {
    Accept: 'application/json'
  };

  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  if (!options.anonymous) {
    const token = tokenSupplier?.();
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
  }

  return headers;
}

async function parseProblem(response: Response): Promise<ProblemDetails> {
  const contentType = response.headers.get('content-type') ?? '';
  if (!contentType.includes('application/json')) {
    return {};
  }

  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return {};
  }
}

const StatusCodes = { NoContent: 204, Unauthorized: 401 } as const;

async function sendRequest<T>(
  culture: Culture,
  path: string,
  options: RequestOptions
): Promise<T> {
  const response = await fetch(`${BASE_PATH}/${culture}${path}`, {
    method: options.method ?? 'GET',
    credentials: 'same-origin',
    headers: buildHeaders(options),
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: options.signal
  });

  if (response.status === StatusCodes.Unauthorized && !options.skipRefresh && refreshHandler) {
    const refreshed = await refreshHandler();
    if (refreshed) {
      const retryResponse = await fetch(`${BASE_PATH}/${culture}${path}`, {
        method: options.method ?? 'GET',
        credentials: 'same-origin',
        headers: buildHeaders(options),
        body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
        signal: options.signal
      });

      if (retryResponse.ok) {
        if (retryResponse.status === StatusCodes.NoContent) {
          return undefined as T;
        }
        return (await retryResponse.json()) as T;
      }

      const problem = await parseProblem(retryResponse);
      throw new ApiError(retryResponse.status, problem);
    }
  }

  if (!response.ok) {
    const problem = await parseProblem(response);
    throw new ApiError(response.status, problem);
  }

  if (response.status === StatusCodes.NoContent) {
    return undefined as T;
  }

  return (await response.json()) as T;
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
  return sendRequest<T>(culture, path, options);
}

/**
 * POST که بدنه دارد و با احراز هویت کامل. خواندن کد را کوتاه می‌کند.
 */
export async function apiPost<T>(
  culture: Culture,
  path: string,
  body?: unknown,
  options: Omit<RequestOptions, 'method' | 'body'> = {}
): Promise<T> {
  return apiRequest<T>(culture, path, { ...options, method: 'POST', body });
}

export async function apiPut<T>(
  culture: Culture,
  path: string,
  body?: unknown,
  options: Omit<RequestOptions, 'method' | 'body'> = {}
): Promise<T> {
  return apiRequest<T>(culture, path, { ...options, method: 'PUT', body });
}

export async function apiDelete<T>(
  culture: Culture,
  path: string,
  options: Omit<RequestOptions, 'method'> = {}
): Promise<T> {
  return apiRequest<T>(culture, path, { ...options, method: 'DELETE' });
}
