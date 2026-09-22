import * as React from 'react';

import { cn } from '@/lib/utils';

export type LabelProps = React.LabelHTMLAttributes<HTMLLabelElement>;

export const Label = React.forwardRef<HTMLLabelElement, LabelProps>(
  ({ className, ...props }, ref) => {
    return (
      <label
        ref={ref}
        className={cn(
          'text-sm font-medium leading-none text-foreground peer-disabled:cursor-not-allowed peer-disabled:opacity-70',
          className
        )}
        {...props}
      />
    );
  }
);
Label.displayName = 'Label';

/**
 * برچسب فیلد فرم با علامت الزامی (ستاره) که در ابتدای برچسب می‌آید
 * تا در هر دو جهت RTL/LTR درست نمایش داده شود.
 */
export function RequiredLabel({ children, ...props }: LabelProps) {
  return (
    <Label {...props}>
      <span className="text-destructive" aria-hidden>*</span> {children}
    </Label>
  );
}
