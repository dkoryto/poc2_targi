import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { afterEach, describe, expect, it } from 'vitest';
import { DataTable, type Column } from './DataTable';
import { OverflowMenu, OverflowItem } from './OverflowMenu';
import { Sheet } from './Sheet';
import { FilterBar } from './FilterBar';
import { renderWithProviders } from '@/test/utils';
import { DESKTOP_WIDTH, MOBILE_WIDTH, setViewportWidth } from '@/test/viewport';

interface Row {
  code: string;
  supplier: string;
  status: string;
}
const ROWS: Row[] = [
  { code: 'PO-2026-0007', supplier: 'Hydromech', status: 'Wysłane' },
  { code: 'PO-2026-0009', supplier: 'Vistula', status: 'Otwarte' },
];
const COLUMNS: Column<Row>[] = [
  { key: 'code', header: 'Zamówienie', render: (r) => r.code, sortValue: (r) => r.code, card: 'title' },
  { key: 'supplier', header: 'Dostawca', render: (r) => r.supplier, sortValue: (r) => r.supplier },
  { key: 'status', header: 'Status', render: (r) => r.status },
];

afterEach(() => setViewportWidth(DESKTOP_WIDTH));

describe('DataTable responsive="cards"', () => {
  it('renders a table on the trade-show monitor', () => {
    setViewportWidth(DESKTOP_WIDTH);
    renderWithProviders(<DataTable columns={COLUMNS} rows={ROWS} rowKey={(r) => r.code} />);
    expect(screen.getByRole('table')).toBeInTheDocument();
    expect(screen.queryAllByTestId('row-card')).toHaveLength(0);
  });

  it('renders one card per row on a phone, keeping every column readable', () => {
    setViewportWidth(MOBILE_WIDTH);
    renderWithProviders(<DataTable columns={COLUMNS} rows={ROWS} rowKey={(r) => r.code} />);
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    const cards = screen.getAllByTestId('row-card');
    expect(cards).toHaveLength(2);
    // the column that used to fall off the right edge is present as a label/value pair
    expect(within(cards[0]!).getByText('Status')).toBeInTheDocument();
    expect(within(cards[0]!).getByText('Wysłane')).toBeInTheDocument();
    expect(within(cards[0]!).getByText('PO-2026-0007')).toBeInTheDocument();
  });

  it('keeps rows clickable and offers sorting through a select', async () => {
    setViewportWidth(MOBILE_WIDTH);
    const user = userEvent.setup();
    const seen: string[] = [];
    renderWithProviders(<DataTable columns={COLUMNS} rows={ROWS} rowKey={(r) => r.code} onRowClick={(r) => seen.push(r.code)} />);
    await user.click(screen.getAllByTestId('row-card')[0]!);
    expect(seen).toEqual(['PO-2026-0007']);

    await user.selectOptions(screen.getByTestId('card-sort'), 'supplier:asc');
    const titles = screen.getAllByTestId('row-card').map((c) => c.textContent);
    expect(titles[0]).toContain('Hydromech');
  });

  it('keeps the empty state on a phone', () => {
    setViewportWidth(MOBILE_WIDTH);
    renderWithProviders(<DataTable columns={COLUMNS} rows={[]} rowKey={(r) => r.code} />);
    expect(screen.getByTestId('empty-state')).toBeInTheDocument();
  });

  it('can keep the table and scroll it instead when asked', () => {
    setViewportWidth(MOBILE_WIDTH);
    renderWithProviders(<DataTable columns={COLUMNS} rows={ROWS} rowKey={(r) => r.code} responsive="scroll" />);
    expect(screen.getByRole('table')).toBeInTheDocument();
  });
});

describe('OverflowMenu', () => {
  it('keeps every action reachable behind the ⋯ button', async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <OverflowMenu>
        <OverflowItem label="Motyw">
          <button type="button">theme</button>
        </OverflowItem>
        <OverflowItem label="Język">
          <button type="button">lang</button>
        </OverflowItem>
      </OverflowMenu>,
    );
    expect(screen.queryByText('theme')).not.toBeInTheDocument();
    await user.click(screen.getByTestId('overflow-menu'));
    expect(screen.getByText('theme')).toBeInTheDocument();
    expect(screen.getByText('lang')).toBeInTheDocument();
    expect(screen.getByText('Motyw')).toBeInTheDocument();
  });
});

describe('Sheet', () => {
  function Harness() {
    const [open, setOpen] = useState(true);
    return (
      <Sheet open={open} onClose={() => setOpen(false)} title="Szczegóły" data-testid="sheet">
        <button type="button">first</button>
        <button type="button">last</button>
      </Sheet>
    );
  }

  it('traps focus and closes on Escape', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);
    await waitFor(() => expect(screen.getByTestId('sheet')).toBeInTheDocument());

    // Tab from the genuinely last control (the close button) wraps back to the first.
    const focusables = Array.from(screen.getByTestId('sheet').querySelectorAll('button'));
    focusables[focusables.length - 1]!.focus();
    await user.tab();
    expect(document.activeElement).toBe(focusables[0]);

    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByTestId('sheet')).not.toBeInTheDocument());
  });
});

describe('FilterBar', () => {
  it('collapses filters behind a button with an active count on a phone', async () => {
    setViewportWidth(MOBILE_WIDTH);
    const user = userEvent.setup();
    renderWithProviders(
      <FilterBar activeCount={2}>
        <input aria-label="szukaj" />
      </FilterBar>,
    );
    expect(screen.queryByLabelText('szukaj')).not.toBeInTheDocument();
    const toggle = screen.getByTestId('filter-toggle');
    expect(toggle).toHaveTextContent('2');
    await user.click(toggle);
    expect(screen.getByLabelText('szukaj')).toBeInTheDocument();
  });

  it('shows filters inline on the desktop', () => {
    setViewportWidth(DESKTOP_WIDTH);
    renderWithProviders(
      <FilterBar>
        <input aria-label="szukaj" />
      </FilterBar>,
    );
    expect(screen.getByLabelText('szukaj')).toBeInTheDocument();
    expect(screen.queryByTestId('filter-toggle')).not.toBeInTheDocument();
  });
});
