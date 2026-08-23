import type { ReactNode } from 'react';
import { Sheet } from './Sheet';

/**
 * Side panel. Kept as a named component because several screens use it, but the
 * behaviour now comes from {@link Sheet}: a right-hand panel on desktop, a focus-trapped
 * bottom sheet on mobile (docs/architecture/responsive.md).
 */
export function Drawer({
  open,
  onClose,
  title,
  children,
  wide,
  actions,
}: {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  children: ReactNode;
  wide?: boolean;
  actions?: ReactNode;
}) {
  return (
    <Sheet open={open} onClose={onClose} title={title} actions={actions} wide={wide}>
      {children}
    </Sheet>
  );
}
