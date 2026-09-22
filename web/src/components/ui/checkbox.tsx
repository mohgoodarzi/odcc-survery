import * as React from 'react';

import { cn } from '@/lib/utils';

export interface CheckboxProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type' | 'onChange' | 'checked'> {
  checked?: boolean;
  /** معادل onChange اما با نام آشنا‌تر (مثل Radix Checkbox). */
  onCheckedChange?: (checked: boolean) => void;
}

/**
 * چک‌باکس بومی. از input بومی استفاده می‌شود تا با RTL و صفحه‌خوان‌ها
 * کاملاً سازگار باشد.
 */
export const Checkbox = React.forwardRef<HTMLInputElement, CheckboxProps>(
  ({ className, checked, onCheckedChange, ...props }, ref) => {
    return (
      <input
        ref={ref}
        type="checkbox"
        checked={checked}
        onChange={(event) => onCheckedChange?.(event.target.checked)}
        className={cn(
          'size-4 shrink-0 rounded border border-input bg-background accent-primary',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
          'disabled:cursor-not-allowed disabled:opacity-50',
          className
        )}
        {...props}
      />
    );
  }
);
Checkbox.displayName = 'Checkbox';
