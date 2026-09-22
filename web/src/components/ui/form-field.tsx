import { cloneElement, isValidElement, type ReactElement, type ReactNode } from 'react';

import { cn } from '@/lib/utils';
import { Label } from './label';

interface FormFieldProps {
  label: string;
  htmlFor?: string;
  required?: boolean;
  error?: string;
  hint?: string;
  /** فیلد کنترل‌شده (input/select/...). */
  children: ReactNode;
  className?: string;
}

/**
 * پوشش فیلد فرم: برچسب، فیلد، راهنما و پیام اعتبارسنجی.
 * پیام خطا با aria-invalid و aria-describedby به فیلد متصل می‌شود تا
 * صفحه‌خوان‌ها آن را اعلام کنند.
 */
export function FormField({
  label,
  htmlFor,
  required,
  error,
  hint,
  children,
  className
}: FormFieldProps) {
  const describedBy = error ? `${htmlFor}-error` : hint ? `${htmlFor}-hint` : undefined;

  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <Label htmlFor={htmlFor}>
        {required && <span className="text-destructive" aria-hidden>*</span>} {label}
      </Label>

      {isValidElement(children) ? (
        cloneElement(children as ReactElement, {
          'aria-invalid': !!error || undefined,
          'aria-describedby': describedBy
        } as Record<string, unknown>)
      ) : (
        <>{children}</>
      )}

      {hint && !error && (
        <p id={`${htmlFor}-hint`} className="text-xs text-muted-foreground">
          {hint}
        </p>
      )}

      {error && (
        <p id={`${htmlFor}-error`} className="text-xs text-destructive" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
