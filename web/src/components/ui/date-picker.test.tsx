import { fireEvent, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { DatePicker, DateTimePicker } from './date-picker';
import { render } from './test-utils';

describe('DatePicker', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders an empty field when there is no value', () => {
    render(<DatePicker value="" onChange={() => {}} />);

    expect(screen.getByPlaceholderText('۱۴۰۵/۰۷/۰۸')).toHaveValue('');
    expect(screen.queryByRole('grid')).toBeNull();
  });

  it('shows the Jalali date for a stored Gregorian value', () => {
    render(<DatePicker value="2026-09-30" onChange={() => {}} />);

    expect(screen.getByDisplayValue('۱۴۰۵/۰۷/۰۸')).toBeInTheDocument();
    expect(screen.getByText('۸ مهر ۱۴۰۵')).toBeInTheDocument();
  });

  it('renders the calendar year without a thousands separator', async () => {
    render(<DatePicker value="2026-09-30" onChange={() => {}} />);

    fireEvent.click(screen.getByRole('button', { name: 'باز کردن تقویم شمسی' }));

    // سرِ تقویم باید «مهر ۱۴۰۵» باشد، نه «مهر ۱٬۴۰۵» (سال بدون جداکننده‌ی هزارگان).
    const header = await screen.findByText(
      (content, element) => element?.tagName === 'SPAN' && content.includes('مهر')
    );

    expect(header).toHaveTextContent('مهر ۱۴۰۵');
    expect(header).not.toHaveTextContent('٬');
  });

  it('opens a Persian month grid and emits the Gregorian date on selection', async () => {
    const handleChange = vi.fn();

    render(<DatePicker value="" onChange={handleChange} />);

    fireEvent.click(screen.getByRole('button', { name: 'باز کردن تقویم شمسی' }));

    const grid = await screen.findByRole('grid');
    expect(grid).toBeInTheDocument();
    expect(screen.getByText('مهر')).toBeInTheDocument();

    // ۸ مهر ۱۴۰۵ = ۲۰۲۶-۰۹-۳۰
    fireEvent.click(screen.getByRole('button', { name: '۸' }));

    expect(handleChange).toHaveBeenCalledWith('2026-09-30');
  });

  it('starts the week with Saturday', async () => {
    render(<DatePicker value="" onChange={() => {}} />);

    fireEvent.click(screen.getByRole('button', { name: 'باز کردن تقویم شمسی' }));

    const header = (await screen.findByRole('grid')).querySelectorAll('div')[0];
    expect(header?.textContent).toBe('شنبه');
  });

  it('navigates to the previous month', async () => {
    render(<DatePicker value="2026-09-30" onChange={() => {}} />);

    fireEvent.click(screen.getByRole('button', { name: 'باز کردن تقویم شمسی' }));
    await screen.findByText('مهر');

    fireEvent.click(screen.getByRole('button', { name: 'ماه قبل' }));

    await waitFor(() => expect(screen.getByText('شهریور')).toBeInTheDocument());
  });

  it('accepts a typed Jalali date', () => {
    const handleChange = vi.fn();
    const input = render(<DatePicker value="" onChange={handleChange} />)
      .getByPlaceholderText('۱۴۰۵/۰۷/۰۸') as HTMLInputElement;

    fireEvent.change(input, { target: { value: '۱۴۰۳/۰۱/۰۱' } });
    fireEvent.blur(input);

    expect(handleChange).toHaveBeenCalledWith('2024-03-20');
  });

  it('reverts an invalid typed date', () => {
    const handleChange = vi.fn();
    const { getByPlaceholderText } = render(<DatePicker value="2026-09-30" onChange={handleChange} />);
    const input = getByPlaceholderText('۱۴۰۵/۰۷/۰۸') as HTMLInputElement;

    fireEvent.change(input, { target: { value: 'نان' } });
    fireEvent.blur(input);

    expect(input.value).toBe('۱۴۰۵/۰۷/۰۸');
    expect(handleChange).not.toHaveBeenCalled();
  });

  it('can be cleared from the calendar', async () => {
    const handleChange = vi.fn();

    render(<DatePicker value="2026-09-30" onChange={handleChange} />);

    fireEvent.click(screen.getByRole('button', { name: 'باز کردن تقویم شمسی' }));
    fireEvent.click(await screen.findByRole('button', { name: 'پاک کردن' }));

    expect(handleChange).toHaveBeenCalledWith('');
  });
});

describe('DateTimePicker', () => {
  it('shows both the Jalali date and the time', () => {
    render(<DateTimePicker value="2026-09-30T14:30" onChange={() => {}} />);

    expect(screen.getByDisplayValue('۱۴۰۵/۰۷/۰۸')).toBeInTheDocument();
    expect(screen.getByText('۸ مهر ۱۴۰۵، ۱۴:۳۰')).toBeInTheDocument();
  });

  it('keeps the time when another date is picked', async () => {
    const handleChange = vi.fn();

    render(<DateTimePicker value="2026-09-30T14:30" onChange={handleChange} />);

    fireEvent.click(screen.getByRole('button', { name: 'باز کردن تقویم شمسی' }));
    fireEvent.click(await screen.findByRole('button', { name: '۹' }));

    expect(handleChange).toHaveBeenCalledWith('2026-10-01T14:30');
  });

  it('updates the time without touching the date', async () => {
    const handleChange = vi.fn();

    render(<DateTimePicker value="2026-09-30T14:30" onChange={handleChange} />);

    fireEvent.click(screen.getByRole('button', { name: 'باز کردن تقویم شمسی' }));
    const timeInput = await screen.findByLabelText('ساعت');

    fireEvent.change(timeInput, { target: { value: '09:15' } });

    expect(handleChange).toHaveBeenCalledWith('2026-09-30T09:15');
  });
});
