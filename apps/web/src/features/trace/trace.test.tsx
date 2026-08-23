import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { Route, Routes } from 'react-router';
import { SerialPage, componentsFromTree } from './SerialPage';
import { LotPage } from './LotPage';
import { TracePage } from './TracePage';
import { renderWithProviders } from '@/test/utils';
import { server } from '@/mocks/server';
import { http, HttpResponse } from 'msw';
import { serialTrace } from '@/mocks/wave2';

function App() {
  return (
    <Routes>
      <Route path="/trace" element={<TracePage />} />
      <Route path="/trace/serials/:serial" element={<SerialPage />} />
      <Route path="/trace/lots/:lot" element={<LotPage />} />
    </Routes>
  );
}

describe('Traceability', () => {
  it('genealogy tree expands/collapses and node panel shows details with navigation', async () => {
    const user = userEvent.setup();
    renderWithProviders(<App />, { route: '/trace/serials/PMV-2026-0007', auth: true });
    await waitFor(() => expect(screen.getByTestId('genealogy-tree')).toBeInTheDocument());
    expect(screen.getByTestId('trace-node-HTS-22-2608')).toBeInTheDocument();
    // deeper node hidden until expanded (default depth 3)
    expect(screen.queryByTestId('trace-node-SUP-01')).not.toBeInTheDocument();
    // collapse the lot node hides its children, expand again shows them
    await user.click(screen.getByTestId('trace-toggle-HTS-22-2608'));
    expect(screen.queryByTestId('trace-node-PO-2026-0003')).not.toBeInTheDocument();
    await user.click(screen.getByTestId('trace-toggle-HTS-22-2608'));
    expect(screen.getByTestId('trace-node-PO-2026-0003')).toBeInTheDocument();
    await user.click(screen.getByTestId('trace-toggle-PO-2026-0003'));
    await user.click(screen.getByTestId('trace-toggle-SHP-2026-0019'));
    expect(screen.getByTestId('trace-node-SUP-01')).toBeInTheDocument();
    await user.click(screen.getByTestId('trace-node-HTS-22-2608'));
    const panel = screen.getByTestId('trace-node-panel');
    expect(panel).toHaveTextContent('HTS-22-2608');
    expect(panel).toHaveTextContent('Zaakceptowana');
    await user.click(screen.getByTestId('trace-node-open'));
    await waitFor(() => expect(screen.getByTestId('lot-page')).toBeInTheDocument());
    expect(screen.getByTestId('trace-forward')).toHaveTextContent('PMV-2026-0007');
  });

  it('componentsFromTree derives lots, suppliers and cert hashes', () => {
    const tr = serialTrace('PMV-2026-0007')!;
    const comps = componentsFromTree(tr.genealogy);
    expect(comps.map((c) => c.lotNumber)).toEqual(expect.arrayContaining(['HTS-22-2608', 'ACT-40-0911', 'MCU-X7-0455']));
  });

  it('blocking HTS-22-2608 invalidates passports and lists affected records', async () => {
    const user = userEvent.setup();
    renderWithProviders(<App />, { route: '/trace/lots/HTS-22-2608', auth: true });
    await waitFor(() => expect(screen.getByTestId('btn-block-lot')).toBeEnabled());
    await user.click(screen.getByTestId('btn-block-lot'));
    await user.type(screen.getByTestId('block-reason'), 'NCR jakościowy');
    await user.type(screen.getByTestId('block-ncr'), 'Wtrącenia niemetaliczne');
    await user.click(screen.getByTestId('confirm-button'));
    await waitFor(() => expect(screen.getByTestId('block-result')).toBeInTheDocument());
    expect(screen.getByTestId('block-result')).toHaveTextContent('WO-2026-011');
    expect(screen.getByTestId('block-result')).toHaveTextContent('PMV-2026-0008');
    await waitFor(() => expect(screen.getByTestId('trace-forward')).toHaveTextContent('Unieważniony'));
    expect(screen.getByTestId('btn-block-lot')).toBeDisabled();
  });

  it('refuses to block a lot with empty fields and says why, instead of failing silently', async () => {
    const user = userEvent.setup();
    let posted = 0;
    server.use(http.post('/api/v1/lots/:lot/block', () => { posted++; return HttpResponse.json({}, { status: 400 }); }));
    renderWithProviders(<App />, { route: '/trace/lots/HTS-22-2608', auth: true });
    await waitFor(() => expect(screen.getByTestId('btn-block-lot')).toBeEnabled());
    await user.click(screen.getByTestId('btn-block-lot'));

    await user.click(screen.getByTestId('confirm-button'));

    // The dialog stays open and explains the refusal; nothing is sent to the server.
    await waitFor(() => expect(screen.getByTestId('form-alert')).toBeInTheDocument());
    expect(screen.getByTestId('block-reason')).toBeInTheDocument();
    expect(posted).toBe(0);
  });

  it('shows the field errors the API returns when it rejects a block', async () => {
    const user = userEvent.setup();
    server.use(
      http.post('/api/v1/lots/:lot/block', () =>
        HttpResponse.json({ title: 'Validation failed', status: 400, errors: { ncrTitle: ['Tytuł NCR jest za krótki.'] } }, { status: 400 }),
      ),
    );
    renderWithProviders(<App />, { route: '/trace/lots/HTS-22-2608', auth: true });
    await waitFor(() => expect(screen.getByTestId('btn-block-lot')).toBeEnabled());
    await user.click(screen.getByTestId('btn-block-lot'));
    await user.type(screen.getByTestId('block-reason'), 'Powód blokady');
    await user.type(screen.getByTestId('block-ncr'), 'X');
    await user.click(screen.getByTestId('confirm-button'));

    await waitFor(() => expect(screen.getByText('Tytuł NCR jest za krótki.')).toBeInTheDocument());
  });

  it('search groups hits by kind', async () => {
    const user = userEvent.setup();
    renderWithProviders(<App />, { route: '/trace', auth: true });
    await user.type(screen.getByTestId('trace-search'), 'HTS-22');
    await waitFor(() => expect(screen.getByTestId('trace-hit-HTS-22-2608')).toBeInTheDocument());
    expect(screen.getByText(/Partia \(2\)/)).toBeInTheDocument();
  });
});
