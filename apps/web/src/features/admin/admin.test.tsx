import { screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { AdminPage } from './AdminPage';
import { renderWithProviders } from '@/test/utils';

describe('Admin', () => {
  it('renders service cards, settings tables and demo status', async () => {
    renderWithProviders(<AdminPage />, { route: '/admin', auth: true });
    await waitFor(() => expect(screen.getByTestId('service-planning-engine')).toHaveTextContent('Działa'));
    expect(screen.getByTestId('service-local-ai')).toHaveTextContent('Wyłączona');
    await waitFor(() => expect(screen.getByTestId('settings-tables')).toHaveTextContent('Odchylenie ETA'));
    expect(screen.getByTestId('settings-tables')).toHaveTextContent('0,35');
  });
});
