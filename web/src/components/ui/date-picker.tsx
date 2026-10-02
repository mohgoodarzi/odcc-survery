import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import {
  Calendar as CalendarIcon, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight
} from 'lucide-react';

import { useLanguage } from '@/i18n/LanguageProvider';
import {
  firstCellOfJalaliMonthGrid,
  formatJalaliDate,
  formatJalaliLongDate,
  gregorianToJalali,
  isEqualJalali,
  jalaliMonthName,
  jalaliToday,
  jalaliToGregorianMs,
  jalaliWeekdayNames,
  parseJalaliDate,
  MS_PER_DAY,
  type JalaliDate
} from '@/i18n/jalali';
import { useClickOutside } from '@/lib/use-click-outside';
import { formatLocalInput, parseLocalInput } from '@/lib/datetime';
import { cn } from '@/lib/utils';
import { Input } from './input';

interface JalaliDatePickerProps {
  id?: string;
  /**
   * مقدار فعلی در قالب ورودی محلی: `YYYY-MM-DD` یا (در حالت تاریخ و زمان)
   * `YYYY-MM-DDTHH:mm`. رشته‌ی خالی یعنی بدون تاریخ.
   */
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  /** نمایش ورودی زمان در کنار تقویم. */
  withTime?: boolean;
  'aria-invalid'?: boolean;
  'aria-describedby'?: string;
}

/**
 * انتخاب‌گر تاریخ تقویم جلالی.
 *
 * تمام تاریخ‌ها در سطح رابط کاربری شمسی نمایش داده و انتخاب می‌شوند،
 * اما مقدار مبادله‌شده با فرم همان مقدار میلادی است که قبلاً استفاده
 * می‌شد؛ بنابراین اعتبارسنجی، مرتب‌سازی و ارسال به API دست‌نخورده می‌ماند.
 *
 * تبدیل تقویم بر عهده‌ی موتور Intl مرورگر است (تقویم `persian`)، پس
 * تقویم نمایش‌داده‌شده با سایر تاریخ‌های برنامه کاملاً یکسان است.
 */
export function JalaliDatePicker({
  id,
  value,
  onChange,
  disabled,
  withTime = false,
  'aria-invalid': ariaInvalid,
  'aria-describedby': ariaDescribedBy
}: JalaliDatePickerProps) {
  const { culture, direction, t } = useLanguage();
  const [isOpen, setIsOpen] = useState(false);
  const [text, setText] = useState('');
  const containerRef = useRef<HTMLDivElement>(null);

  const parsed = useMemo(() => parseLocalInput(value), [value]);
  const selected = useMemo(() => (parsed ? gregorianToJalali(parsed.utcMs) : null), [parsed]);

  useClickOutside(containerRef, () => setIsOpen(false), isOpen);

  useEffect(() => {
    setText(selected ? formatJalaliDate(selected, culture) : '');
  }, [selected, culture]);

  function emit(nextMs: number) {
    onChange(
      formatLocalInput(
        nextMs,
        parsed?.hour ?? 0,
        parsed?.minute ?? 0,
        withTime
      )
    );
  }

  function commitText() {
    const trimmed = text.trim();

    if (!trimmed) {
      onChange('');
      return;
    }

    const jalali = parseJalaliDate(trimmed);
    const ms = jalali === null ? null : jalaliToGregorianMs(jalali);

    if (ms === null) {
      // ورودی نامعتبر به آخرین مقدار معتبر برمی‌گردد.
      setText(selected ? formatJalaliDate(selected, culture) : '');
      return;
    }

    emit(ms);
  }

  return (
    <div ref={containerRef} className="flex flex-col gap-1">
      <div className="relative">
        <Input
          id={id}
          type="text"
          value={text}
          placeholder={t.common.datePicker.formatHint}
          onChange={(event) => setText(event.target.value)}
          onBlur={commitText}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              event.currentTarget.blur();
            } else if (event.key === 'Escape') {
              setText(selected ? formatJalaliDate(selected, culture) : '');
              event.currentTarget.blur();
            }
          }}
          disabled={disabled}
          dir="ltr"
          aria-invalid={ariaInvalid}
          aria-describedby={ariaDescribedBy}
          className="pe-9 font-mono"
        />

        <button
          type="button"
          onClick={() => setIsOpen((open) => !open)}
          disabled={disabled}
          aria-label={t.common.datePicker.openCalendar}
          aria-expanded={isOpen}
          className="absolute inset-y-0 end-0 flex w-9 items-center justify-center text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
        >
          <CalendarIcon className="size-4" />
        </button>

        {isOpen && (
          <JalaliCalendarPanel
            selected={selected}
            hour={parsed?.hour ?? 0}
            minute={parsed?.minute ?? 0}
            withTime={withTime}
            direction={direction}
            onSelectDate={(ms) => {
              emit(ms);
              if (!withTime) setIsOpen(false);
            }}
            onChangeTime={(hour, minute) => {
              const anchor = parsed?.utcMs ?? jalaliToGregorianMs(jalaliToday()) ?? Date.now();
              onChange(formatLocalInput(anchor, hour, minute, true));
            }}
            onClear={() => {
              onChange('');
              setIsOpen(false);
            }}
          />
        )}
      </div>

      {selected && (
        <p className="text-xs text-muted-foreground" dir={direction}>
          {formatJalaliLongDate(selected, culture)}
          {withTime && parsed
            ? `، ${String(parsed.hour).padStart(2, '0')}:${String(parsed.minute).padStart(2, '0')}`
            : ''}
        </p>
      )}
    </div>
  );
}

/**
 * انتخاب‌گر تاریخ (بدون زمان). مقدار `YYYY-MM-DD` است.
 */
export function DatePicker(props: Omit<JalaliDatePickerProps, 'withTime'>) {
  return <JalaliDatePicker {...props} withTime={false} />;
}

/**
 * انتخاب‌گر تاریخ و زمان. مقدار `YYYY-MM-DDTHH:mm` است.
 */
export function DateTimePicker(props: Omit<JalaliDatePickerProps, 'withTime'>) {
  return <JalaliDatePicker {...props} withTime />;
}

interface JalaliCalendarPanelProps {
  selected: JalaliDate | null;
  hour: number;
  minute: number;
  withTime: boolean;
  direction: 'rtl' | 'ltr';
  onSelectDate: (utcMs: number) => void;
  onChangeTime: (hour: number, minute: number) => void;
  onClear: () => void;
}

/**
 * شبکه‌ی ماهانه‌ی تقویم جلالی با ناوبری ماه/سال، میان‌بر «امروز»،
 * ورودی زمان (در حالت تاریخ و زمان) و پاک‌سازی.
 */
function JalaliCalendarPanel({
  selected,
  hour,
  minute,
  withTime,
  direction,
  onSelectDate,
  onChangeTime,
  onClear
}: JalaliCalendarPanelProps) {
  const { culture, t } = useLanguage();
  const [view, setView] = useState<JalaliDate>(() => selected ?? jalaliToday());
  const today = useMemo(() => jalaliToday(), []);

  const firstCellMs = useMemo(
    () => firstCellOfJalaliMonthGrid(view.year, view.month),
    [view.year, view.month]
  );

  const weekdayNames = useMemo(() => jalaliWeekdayNames(culture), [culture]);

  // سال‌ها باید بدون جداکننده‌ی هزارگان نمایش داده شوند (۱۴۰۵ نه ۱٬۴۰۵).
  // ارقام روز/ماه هرگز جداکننده نمی‌گیرند اما برای یکدستی همان تنظیم استفاده می‌شود.
  const dayFormatter = useMemo(
    () => new Intl.NumberFormat(culture === 'fa' ? 'fa-IR' : 'en-US', { useGrouping: false }),
    [culture]
  );

  const yearFormatter = useMemo(
    () => new Intl.NumberFormat(culture === 'fa' ? 'fa-IR' : 'en-US', { useGrouping: false }),
    [culture]
  );

  const days = useMemo(() => {
    if (firstCellMs === null) return [];
    return Array.from({ length: 42 }, (_, index) => firstCellMs + index * MS_PER_DAY);
  }, [firstCellMs]);

  function shiftMonth(delta: number) {
    setView((current) => {
      const absolute = (current.month - 1) + delta;
      const year = current.year + Math.floor(absolute / 12);
      const month = ((absolute % 12) + 12) % 12 + 1;
      return { year, month, day: 1 };
    });
  }

  function shiftYear(delta: number) {
    setView((current) => ({ ...current, year: current.year + delta }));
  }

  if (firstCellMs === null) return null;

  return (
    <div
      dir={direction}
      className="absolute z-50 mt-2 w-72 rounded-md border bg-card p-3 text-card-foreground shadow-md"
    >
      <div className="mb-2 flex items-center justify-between gap-1">
        <div className="flex items-center gap-1">
          <NavigationButton
            label={direction === 'rtl' ? t.common.datePicker.nextYear : t.common.datePicker.previousYear}
            onClick={() => shiftYear(direction === 'rtl' ? 1 : -1)}
            icon={direction === 'rtl' ? <ChevronsRight className="size-4" /> : <ChevronsLeft className="size-4" />}
          />
          <NavigationButton
            label={direction === 'rtl' ? t.common.datePicker.nextMonth : t.common.datePicker.previousMonth}
            onClick={() => shiftMonth(direction === 'rtl' ? 1 : -1)}
            icon={direction === 'rtl' ? <ChevronRight className="size-4" /> : <ChevronLeft className="size-4" />}
          />
        </div>

        <span className="px-1 text-sm font-medium">
          {jalaliMonthName(view.month, culture)} {yearFormatter.format(view.year)}
        </span>

        <div className="flex items-center gap-1">
          <NavigationButton
            label={direction === 'rtl' ? t.common.datePicker.previousMonth : t.common.datePicker.nextMonth}
            onClick={() => shiftMonth(direction === 'rtl' ? -1 : 1)}
            icon={direction === 'rtl' ? <ChevronLeft className="size-4" /> : <ChevronRight className="size-4" />}
          />
          <NavigationButton
            label={direction === 'rtl' ? t.common.datePicker.previousYear : t.common.datePicker.nextYear}
            onClick={() => shiftYear(direction === 'rtl' ? -1 : 1)}
            icon={direction === 'rtl' ? <ChevronsLeft className="size-4" /> : <ChevronsRight className="size-4" />}
          />
        </div>
      </div>

      <div className="mb-1 grid grid-cols-7 gap-0.5">
        {weekdayNames.map((name) => (
          <div
            key={name}
            className="flex h-6 items-center justify-center text-[0.65rem] text-muted-foreground"
          >
            {name}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-7 gap-0.5">
        {days.map((dayMs) => {
          const jalali = gregorianToJalali(dayMs);
          const isOtherMonth = jalali.month !== view.month;
          const isSelected = selected !== null && isEqualJalali(jalali, selected);
          const isToday = isEqualJalali(jalali, today);

          return (
            <button
              key={dayMs}
              type="button"
              onClick={() => onSelectDate(dayMs)}
              aria-current={isToday ? 'date' : undefined}
              aria-pressed={isSelected}
              className={cn(
                'flex h-8 items-center justify-center rounded-sm text-sm transition-colors',
                'hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                isOtherMonth && 'text-muted-foreground/40',
                isToday && !isSelected && 'border border-input',
                isSelected && 'bg-primary font-medium text-primary-foreground hover:bg-primary hover:text-primary-foreground'
              )}
            >
              {dayFormatter.format(jalali.day)}
            </button>
          );
        })}
      </div>

      {withTime && (
        <div className="mt-3 flex items-center justify-between gap-2 border-t pt-3">
          <label htmlFor="picker-time" className="text-xs text-muted-foreground">
            {t.common.datePicker.time}
          </label>
          <input
            id="picker-time"
            type="time"
            value={`${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`}
            onChange={(event) => {
              const [nextHour, nextMinute] = event.target.value.split(':').map(Number);
              onChangeTime(Number.isNaN(nextHour) ? 0 : nextHour, Number.isNaN(nextMinute) ? 0 : nextMinute);
            }}
            dir="ltr"
            className="h-8 rounded-md border border-input bg-background px-2 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
        </div>
      )}

      <div className="mt-3 flex items-center justify-between gap-2 border-t pt-3">
        <button
          type="button"
          onClick={() => {
            const todayMs = jalaliToGregorianMs(today);
            if (todayMs !== null) onSelectDate(todayMs);
          }}
          className="rounded-sm px-2 py-1 text-xs text-primary transition-colors hover:bg-primary/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {t.common.datePicker.today}
        </button>

        <button
          type="button"
          onClick={onClear}
          className="rounded-sm px-2 py-1 text-xs text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {t.common.datePicker.clear}
        </button>
      </div>
    </div>
  );
}

interface NavigationButtonProps {
  label: string;
  onClick: () => void;
  icon: ReactNode;
}

function NavigationButton({ label, onClick, icon }: NavigationButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      className="flex size-7 items-center justify-center rounded-sm text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      {icon}
    </button>
  );
}
