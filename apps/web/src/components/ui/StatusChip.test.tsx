import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { RiskBadge, PoStatusChip, DocStatusChip } from './StatusChip';
import { setLocale } from '@/i18n';

describe('StatusChip / RiskBadge', () => {
  it('renders risk label + score in PL', () => {
    render(<RiskBadge category="Critical" score={79} />);
    expect(screen.getByText('Krytyczne · 79')).toBeInTheDocument();
  });
  it('renders risk label in EN after locale switch', () => {
    setLocale('en');
    render(<RiskBadge category="Medium" score={44} />);
    expect(screen.getByText('Medium · 44')).toBeInTheDocument();
  });
  it('always includes an icon (never colour-only)', () => {
    const { container } = render(<PoStatusChip status="Shipped" />);
    expect(container.querySelector('svg')).not.toBeNull();
    expect(screen.getByText('Wysłane')).toBeInTheDocument();
  });
  it('document statuses', () => {
    render(<DocStatusChip status="RequiresCompletion" />);
    expect(screen.getByText('Wymaga uzupełnienia')).toBeInTheDocument();
  });
});
