/**
 * کمک‌کننده‌ی تست برای بارگذاری واقعی فایل.
 *
 * محیط jsdom یک پیاده‌سازی مختصرِ <c>File</c>/<c>FormData</c> دارد که برای
 * <c>fetch</c> خود Node (undici) قابل تشخیص نیست؛ در نتیجه بدنه‌ی multipart
 * خالی و با نام فایل «blob» ارسال می‌شود و سرور فایل خالی می‌بیند. این ماژول
 * بدنه‌ی multipart را به‌صورت دستی می‌سازد تا آپلود واقعی در تست‌ها کار کند.
 */

export interface SerializedBody {
  body: BodyInit;
  contentType: string;
}

const encoder = new TextEncoder();

function randomBoundary(): string {
  const value = new Uint16Array(8);
  (globalThis.crypto as Crypto).getRandomValues(value);
  return `----odcc-test-boundary-${Array.from(value, (n) => n.toString(16).padStart(4, '0')).join('')}`;
}

function concat(parts: Uint8Array[]): Uint8Array {
  const total = parts.reduce((sum, part) => sum + part.length, 0);
  const result = new Uint8Array(total);
  let offset = 0;
  for (const part of parts) {
    result.set(part, offset);
    offset += part.length;
  }
  return result;
}

function readBlobBytes(blob: Blob): Promise<ArrayBuffer> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();

    reader.onload = () => resolve(reader.result as ArrayBuffer);
    reader.onerror = () => reject(reader.error ?? new Error('خواندن محتوای فایل ناموفق بود.'));

    reader.readAsArrayBuffer(blob);
  });
}

/**
 * ساخت بدنه‌ی multipart/form-data از روی یک FormData (با خواندن بایت‌های واقعی
 * فایل‌ها از طریق FileReader که در jsdom در دسترس است).
 */
export async function serializeMultipartFormData(
  formData: FormData
): Promise<SerializedBody> {
  const boundary = randomBoundary();
  const boundaryBytes = encoder.encode(`--${boundary}\r\n`);
  const parts: Uint8Array[] = [];

  for (const [name, value] of formData.entries()) {
    if (value instanceof Blob) {
      const fileName = (value as File).name ?? 'blob';
      const contentType = value.type || 'application/octet-stream';
      const header = encoder.encode(
        `Content-Disposition: form-data; name="${name}"; filename="${fileName}"\r\n` +
          `Content-Type: ${contentType}\r\n\r\n`
      );
      const content = new Uint8Array(await readBlobBytes(value));

      parts.push(boundaryBytes, header, content, encoder.encode('\r\n'));
    } else {
      const header = encoder.encode(
        `Content-Disposition: form-data; name="${name}"\r\n\r\n`
      );
      parts.push(boundaryBytes, header, encoder.encode(String(value)), encoder.encode('\r\n'));
    }
  }

  parts.push(encoder.encode(`--${boundary}--\r\n`));

  return {
    body: concat(parts) as BodyInit,
    contentType: `multipart/form-data; boundary=${boundary}`
  };
}

/**
 * اگر بدنه‌ی درخواست FormData است، آن را به بدنه‌ی multipart واقعی تبدیل می‌کند
 * تا سرور فایل را به‌درستی دریافت کند. در غیر این صورت، درخواست دست‌نخورده است.
 */
export async function serializeBodyIfNeeded(
  init: RequestInit | undefined
): Promise<RequestInit | undefined> {
  if (!init?.body || !(init.body instanceof FormData)) {
    return init;
  }

  const { body, contentType } = await serializeMultipartFormData(init.body);
  const headers = new Headers(init.headers ?? {});
  headers.set('content-type', contentType);

  return { ...init, body, headers };
}
