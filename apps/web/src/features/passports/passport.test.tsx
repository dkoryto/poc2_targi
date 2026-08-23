import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { Route, Routes } from 'react-router';
import { PassportPage } from './PassportPage';
import { PassportsPage } from './PassportsPage';
import { renderWithProviders } from '@/test/utils';
import { server } from '@/mocks/server';
import { http, HttpResponse } from 'msw';
import { getPassport } from '@/mocks/wave2';

function App() {
  return (
    <Routes>
      <Route path="/passports" element={<PassportsPage />} />
      <Route path="/passports/:serial" element={<PassportPage />} />
    </Routes>
  );
}

describe('Passports', () => {
  it('incomplete passport: generate disabled, missing list shown; 422 from API renders its missing[]', async () => {
    const user = userEvent.setup();
    const first = renderWithProviders(<App />, { route: '/passports/SCM-2026-0103', auth: true });
    await waitFor(() => expect(screen.getByTestId('passport-status')).toHaveTextContent('Roboczy'));
    const missing = screen.getByTestId('passport-missing');
    expect(missing).toHaveTextContent('MCU-X7');
    expect(missing).toHaveTextContent('inspekcji');
    expect(screen.getByTestId('btn-generate-passport')).toBeDisabled();
    expect(screen.getByTestId('passport-req-CERTIFICATES_WITH_HASH')).toHaveTextContent('Certyfikaty');

    // simulate server-side disagreement: UI thinks complete, API answers 422
    server.use(
      http.get('/api/v1/passports/SCM-2026-0103', () => HttpResponse.json({ ...getPassport('PMV-2026-0007')!, serial: 'SCM-2026-0103', status: 'Approved' })),
      http.post('/api/v1/passports/SCM-2026-0103/generate', () => HttpResponse.json({ title: 'Passport incomplete', status: 422, missing: [{ code: 'QC_STATUS' }] }, { status: 422 })),
    );
    first.unmount();
    renderWithProviders(<App />, { route: '/passports/SCM-2026-0103', auth: true });
    await waitFor(() => expect(screen.getByTestId('passport-status')).toHaveTextContent('Zatwierdzony'));
    const btn = screen.getByTestId('btn-generate-passport');
    await waitFor(() => expect(btn).toBeEnabled(), { timeout: 3000 });
    await user.click(btn);
    await user.click(screen.getByTestId('confirm-button'));
    await waitFor(() => expect(screen.getByTestId('passport-missing')).toHaveTextContent('kontroli jakości'));
  });

  it('complete passport generates a new version with SHA-256', async () => {
    const user = userEvent.setup();
    renderWithProviders(<App />, { route: '/passports/PMV-2026-0007', auth: true });
    await waitFor(() => expect(screen.getByTestId('passport-complete')).toBeInTheDocument());
    expect(screen.getByTestId('passport-versions')).toHaveTextContent('v1');
    await waitFor(() => expect(screen.getByTestId('btn-generate-passport')).toBeEnabled());
    await user.click(screen.getByTestId('btn-generate-passport'));
    await user.click(screen.getByTestId('confirm-button'));
    await waitFor(() => expect(screen.getByTestId('passport-versions')).toHaveTextContent('v2'));
    expect(screen.getByTestId('passport-pdf-2')).toBeInTheDocument();
  });

  it('list filters by status', async () => {
    const user = userEvent.setup();
    renderWithProviders(<App />, { route: '/passports', auth: true });
    await waitFor(() => expect(screen.getByTestId('passports-table')).toHaveTextContent('PMV-2026-0007'));
    await user.click(screen.getByTestId('passport-filter-Draft'));
    await waitFor(() => expect(screen.getByTestId('passports-table')).not.toHaveTextContent('PMV-2026-0007'));
    expect(screen.getByTestId('passports-table')).toHaveTextContent('SCM-2026-0103');
  });
});
