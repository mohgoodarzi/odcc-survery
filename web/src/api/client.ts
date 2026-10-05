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
  /**
   * نوع پاسخ مورد انتظار. پیش‌فرض JSON است؛ «blob» برای دانلود منابع باینری
   * (مثل تصویر آواتار) به کار می‌رود.
   */
  responseType?: 'json' | 'blob';
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
  // برای دانلود منابع باینری (تصویر آواتار، خروجی PDF/Excel گزارش‌ها) هر نوع
  // محتوایی قابل قبول است؛ سرور نوع محتوای درست را بر اساس قالب برمی‌گرداند.
  const headers: Record<string, string> = {
    Accept: options.responseType === 'blob' ? '*/*' : 'application/json'
  };

  // برای FormData، مرورگر خودش Content-Type با boundary را تنظیم می‌کند؛
  // تنظیم دستی آن باعث خراب شدن درخواست multipart می‌شود.
  const isFormData = options.body instanceof FormData;

  if (options.body !== undefined && !isFormData) {
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

/**
 * اجرای یک درخواست روی نشانی نهایی. این تابع از پاسخ بی‌اطلاع است و فقط
 * احراز هویت، تازه‌سازی توکن و پردازش خطا را مدیریت می‌کند تا منطق مشترک
 * بین درخواست‌های JSON و دانلود منابع باینری تکرار نشود.
 */
async function sendRequest<T>(
  url: string,
  options: RequestOptions,
  parse: (response: Response) => Promise<T>
): Promise<T> {
  const isFormData = options.body instanceof FormData;

  async function execute(): Promise<Response> {
    return fetch(url, {
      method: options.method ?? 'GET',
      credentials: 'same-origin',
      headers: buildHeaders(options),
      body: isFormData ? (options.body as FormData) : (options.body !== undefined ? JSON.stringify(options.body) : undefined),
      signal: options.signal
    });
  }

  const response = await execute();

  if (response.status === StatusCodes.Unauthorized && !options.skipRefresh && refreshHandler) {
    const refreshed = await refreshHandler();
    if (refreshed) {
      const retryResponse = await execute();

      if (retryResponse.ok) {
        if (retryResponse.status === StatusCodes.NoContent) {
          return undefined as T;
        }
        return parse(retryResponse);
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

  return parse(response);
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
  return sendRequest<T>(
    `${BASE_PATH}/${culture}${path}`,
    options,
    async response => (await response.json()) as T
  );
}

/**
 * دانلود یک منبع باینری (مثل تصویر آواتار یا خروجی PDF/Excel گزارش‌ها) که
 * نیازمند احراز هویت است.
 *
 * مرورگر نمی‌تواند هدر <c>Authorization</c> را به درخواستِ یک تصویر
 * (<c>&lt;img src&gt;</c>) اضافه کند؛ بنابراین تصویر از طریق همین کلاینت
 * (با توکن و مکانیزم تازه‌سازی توکن) دانلود می‌شود و سپس در سمت کلاینت
 * به نشانی قابل‌نمایش تبدیل می‌گرداند.
 *
 * نشانی داده‌شده نسبی و نسبت به مبدأ برنامه است و باید با <c>/api</c>
 * شروع شود (مثل نشانی‌ای که سرور در <c>avatarUrl</c> برمی‌گرداند).
 */
export async function requestResource(url: string, signal?: AbortSignal): Promise<Blob> {
  return sendRequest<Blob>(
    url,
    { signal, responseType: 'blob' },
    async response => response.blob()
  );
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
