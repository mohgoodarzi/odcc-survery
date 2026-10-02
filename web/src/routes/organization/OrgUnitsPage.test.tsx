import { fireEvent, screen, render as renderScreen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { ReactElement, ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';

import { OrgUnitsPage } from './OrgUnitsPage';
import { LanguageProvider } from '@/i18n/LanguageProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';

// داده‌ی واقعیِ درختِ سازمانی (همان ساختاری که API برمی‌گرداند)
const REAL_TREE = [
  {
    id: '01a0d412-c716-7c18-8d85-68187b170e3d',
    code: 'HQ',
    name: 'سازمان مرکزی',
    type: 1,
    level: 0,
    isActive: true,
    managerEmployeeId: '44444444-0000-0000-0000-000000000002',
    managerFullName: null,
    children: [
      { id: '11111111-0000-0000-0000-000000000002', code: 'HR', name: 'منابع انسانی', type: 3, level: 1, isActive: true, managerEmployeeId: null, managerFullName: null, children: [] },
      { id: '11111111-0000-0000-0000-000000000001', code: 'IT', name: 'فناوری اطلاعات', type: 3, level: 1, isActive: true, managerEmployeeId: null, managerFullName: null, children: [] },
      { id: '11111111-0000-0000-0000-000000000004', code: 'OPS', name: 'عملیات و پشتیبانی', type: 3, level: 1, isActive: true, managerEmployeeId: null, managerFullName: null, children: [] },
      { id: '11111111-0000-0000-0000-000000000003', code: 'SAL', name: 'فروش و بازاریابی', type: 3, level: 1, isActive: true, managerEmployeeId: null, managerFullName: null, children: [] }
    ]
  }
];

vi.mock('@/api/hooks', () => ({
  useOrgUnitTree: () => ({
    data: REAL_TREE,
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn()
  }),
  useDeleteOrgUnit: () => ({ mutate: vi.fn(), isPending: false, error: null })
}));

vi.mock('@/auth/AuthProvider', () => ({
  useAuth: () => ({ hasPermission: () => true, hasAnyPermission: () => true })
}));

function render(node: ReactElement) {
  return renderScreen(node, {
    wrapper: ({ children }: { children: ReactNode }) => (
      <MemoryRouter initialEntries={['/fa']}>
        <ThemeProvider>
          <LanguageProvider>{children}</LanguageProvider>
        </ThemeProvider>
      </MemoryRouter>
    )
  });
}

describe('OrgUnitsPage', () => {
  it('renders the root unit', () => {
    render(<OrgUnitsPage />);

    expect(screen.getByText('HQ')).toBeInTheDocument();
  });

  it('renders every child unit by default so the page matches the dropdown', () => {
    render(<OrgUnitsPage />);

    expect(screen.getByText('HQ')).toBeInTheDocument();
    expect(screen.getByText('HR')).toBeInTheDocument();
    expect(screen.getByText('IT')).toBeInTheDocument();
    expect(screen.getByText('OPS')).toBeInTheDocument();
    expect(screen.getByText('SAL')).toBeInTheDocument();
  });

  it('hides children when the branch is collapsed', () => {
    render(<OrgUnitsPage />);

    // درخت پیش‌فرض باز است؛ دکمه اکنون شاخه را جمع می‌کند.
    fireEvent.click(screen.getByRole('button', { name: 'بستن' }));

    expect(screen.getByText('HQ')).toBeInTheDocument();
    expect(screen.queryByText('HR')).not.toBeInTheDocument();
    expect(screen.queryByText('SAL')).not.toBeInTheDocument();
  });
});
