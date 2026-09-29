import { apiDelete, apiPut, apiRequest } from './client';
import type { Culture } from '@/i18n/types';
import type { PagedResult } from './users';

/**
 * قرارداد API ماژول یکپارچه‌سازی: اندپوینت‌های خارجی (وب‌هوک خروجی/ورودی،
 * همگام‌سازی HR، SSO، ارائه‌دهنده‌ی هوش مصنوعی) و تاریخچه‌ی تحویل وب‌هوک.
 *
 * **امنیت:** مقدار رازها هرگز از API خارج نمی‌شود — فقط نام منطقی آن
 * (`secretRef`) و پرچم «پیکربندی‌شده». راز واقعی از پیکربندی سرور خوانده می‌شود.
 *
 * **حریم خصوصی:** payload وب‌هوک فقط متادیتای عمومی است (شناسه‌ها، کدها،
 * شاخص‌های تجمعی) — هرگز شناسه‌ی پاسخ‌گوی یک نظرسنجی.
 */

export enum IntegrationType {
  OutboundWebhook = 0,
  InboundWebhook = 1,
  HrSync = 2,
  Sso = 3,
  AiProvider = 4
}

export enum IntegrationAuthType {
  None = 0,
  HmacSignature = 1,
  BearerToken = 2,
  ApiKey = 3,
  Basic = 4
}

export enum DeliveryStatus {
  Pending = 0,
  Succeeded = 1,
  Failed = 2
}

// --- اندپوینت: درخواست‌ها -----------------------------------------------------------

export interface IntegrationEndpointSearchRequest {
  searchText?: string | null;
  type?: IntegrationType | null;
  isActive?: boolean | null;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface SaveIntegrationEndpointRequest {
  name: string;
  code: string;
  description?: string | null;
  type: IntegrationType;
  url: string;
  httpMethod: string;
  authType: IntegrationAuthType;
  /** نام منطقی راز؛ مقدار واقعی از پیکربندی خوانده می‌شود. */
  secretRef?: string | null;
  authHeaderName?: string | null;
  timeoutSeconds: number;
  maxRetries: number;
  /** فقط این نوع رویدادها ارسال شوند (خالی یعنی همه). */
  subscribedEvents?: string[] | null;
  activateImmediately: boolean;
}

export interface TestIntegrationEndpointRequest {
  payloadJson?: string | null;
}

// --- تحویل وب‌هوک: درخواست‌ها --------------------------------------------------------

export interface WebhookDeliverySearchRequest {
  searchText?: string | null;
  status?: DeliveryStatus | null;
  endpointId?: string | null;
  eventType?: string | null;
  retryableOnly?: boolean;
  page?: number;
  pageSize?: number;
}

// --- خروجی‌ها -----------------------------------------------------------------------

export interface IntegrationEndpointDto {
  id: string;
  name: string;
  code: string;
  description?: string | null;
  type: IntegrationType;
  url: string;
  httpMethod: string;
  authType: IntegrationAuthType;
  /** فقط نام منطقی راز — مقدار واقعی هرگز برگردانده نمی‌شود. */
  secretRef?: string | null;
  /** آیا رازِ این اندپوینت در پیکربندی موجود است؟ */
  secretConfigured: boolean;
  authHeaderName?: string | null;
  isActive: boolean;
  timeoutSeconds: number;
  maxRetries: number;
  subscribedEvents: string[];
  createdByUserId?: string | null;
  createdByUserName?: string | null;
  createdAt: string;
  successfulDeliveries: number;
  lastDeliveryError?: string | null;
  lastDeliveryAt?: string | null;
}

export interface WebhookDeliveryDto {
  id: string;
  endpointId: string;
  endpointCode: string;
  eventType: string;
  eventId: string;
  status: DeliveryStatus;
  attemptCount: number;
  lastAttemptAt?: string | null;
  nextAttemptAt?: string | null;
  deliveredAt?: string | null;
  responseStatusCode?: number | null;
  lastError?: string | null;
  createdAt: string;
  /** payload فقط در جستجوی جزئیات برگردانده می‌شود (نه فهرست). */
  payloadJson?: string | null;
}

export interface TestEndpointResultDto {
  success: boolean;
  statusCode?: number | null;
  error?: string | null;
  elapsedMilliseconds: number;
}

export interface IntegrationStatsDto {
  totalEndpoints: number;
  activeEndpoints: number;
  pendingDeliveries: number;
  failedDeliveries: number;
  successfulDeliveries: number;
}

function appendSearchParams(path: string, params: Record<string, string>): string {
  const search = new URLSearchParams(params).toString();
  return search ? `${path}?${search}` : path;
}

function buildEndpointParams(request: IntegrationEndpointSearchRequest): Record<string, string> {
  const params: Record<string, string> = {};
  if (request.searchText) params.searchText = request.searchText;
  if (request.type !== null && request.type !== undefined) params.type = String(request.type);
  if (request.isActive !== null && request.isActive !== undefined) params.isActive = String(request.isActive);
  if (request.includeArchived) params.includeArchived = 'true';
  params.page = String(request.page ?? 1);
  params.pageSize = String(request.pageSize ?? 20);
  return params;
}

function buildDeliveryParams(request: WebhookDeliverySearchRequest): Record<string, string> {
  const params: Record<string, string> = {};
  if (request.searchText) params.searchText = request.searchText;
  if (request.status !== null && request.status !== undefined) params.status = String(request.status);
  if (request.endpointId) params.endpointId = request.endpointId;
  if (request.eventType) params.eventType = request.eventType;
  if (request.retryableOnly) params.retryableOnly = 'true';
  params.page = String(request.page ?? 1);
  params.pageSize = String(request.pageSize ?? 20);
  return params;
}

export const integrationsApi = {
  // --- اندپوینت‌ها ---------------------------------------------------------------

  searchEndpoints: (culture: Culture, request: IntegrationEndpointSearchRequest, signal?: AbortSignal) =>
    apiRequest<PagedResult<IntegrationEndpointDto>>(
      culture,
      appendSearchParams('/integrations/endpoints', buildEndpointParams(request)),
      { signal }
    ),

  getStats: (culture: Culture, signal?: AbortSignal) =>
    apiRequest<IntegrationStatsDto>(culture, '/integrations/endpoints/stats', { signal }),

  getEndpoint: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<IntegrationEndpointDto>(culture, `/integrations/endpoints/${id}`, { signal }),

  createEndpoint: (culture: Culture, request: SaveIntegrationEndpointRequest) =>
    apiRequest<IntegrationEndpointDto>(culture, '/integrations/endpoints', { method: 'POST', body: request }),

  updateEndpoint: (culture: Culture, id: string, request: SaveIntegrationEndpointRequest) =>
    apiPut<IntegrationEndpointDto>(culture, `/integrations/endpoints/${id}`, request),

  activateEndpoint: (culture: Culture, id: string) =>
    apiRequest<IntegrationEndpointDto>(culture, `/integrations/endpoints/${id}/activate`, { method: 'POST' }),

  deactivateEndpoint: (culture: Culture, id: string) =>
    apiRequest<void>(culture, `/integrations/endpoints/${id}/deactivate`, { method: 'POST' }),

  archiveEndpoint: (culture: Culture, id: string) =>
    apiDelete<void>(culture, `/integrations/endpoints/${id}`),

  testEndpoint: (culture: Culture, id: string, request: TestIntegrationEndpointRequest) =>
    apiRequest<TestEndpointResultDto>(culture, `/integrations/endpoints/${id}/test`, {
      method: 'POST',
      body: request
    }),

  // --- تحویل‌ها -------------------------------------------------------------------

  searchDeliveries: (culture: Culture, request: WebhookDeliverySearchRequest, signal?: AbortSignal) =>
    apiRequest<PagedResult<WebhookDeliveryDto>>(
      culture,
      appendSearchParams('/integrations/deliveries', buildDeliveryParams(request)),
      { signal }
    ),

  getDelivery: (culture: Culture, id: string, signal?: AbortSignal) =>
    apiRequest<WebhookDeliveryDto>(culture, `/integrations/deliveries/${id}`, { signal }),

  retryDelivery: (culture: Culture, id: string) =>
    apiRequest<WebhookDeliveryDto>(culture, `/integrations/deliveries/${id}/retry`, { method: 'POST' })
};

/**
 * مسیر دریافت وب‌هوک ورودی. این مسیر ثابت و عمومی است (احراز هویت با امضای HMAC
 * به‌جای JWT) و در پیکربندی سرور نیز مستند شده است.
 */
export const INBOUND_WEBHOOK_PATH = '/api/webhooks';

/**
 * ساخت URL کامل دریافت وب‌هوک ورودی برای یک اندپوینت. برای نمایش به مدیر در
 * صفحه‌ی اندپوینت‌ها استفاده می‌شود.
 */
export function buildInboundWebhookUrl(origin: string, endpointCode: string): string {
  return `${origin.replace(/\/$/, '')}${INBOUND_WEBHOOK_PATH}/${encodeURIComponent(endpointCode)}`;
}

/**
 * مسیر نسبی دریافت وب‌هوک ورودی (بدون مبدأ) — برای نمایش در رابط کاربری.
 */
export function buildInboundWebhookPath(endpointCode: string): string {
  return `${INBOUND_WEBHOOK_PATH}/${encodeURIComponent(endpointCode)}`;
}
