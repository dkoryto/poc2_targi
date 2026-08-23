import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, vi } from 'vitest';
import { server } from '@/mocks/server';
import { resetMockState } from '@/mocks/handlers';
import i18n, { setLocale } from '@/i18n';

// jsdom lacks these
class MemoryStorage implements Storage {
  private m = new Map<string, string>();
  get length() { return this.m.size; }
  clear() { this.m.clear(); }
  getItem(k: string) { return this.m.has(k) ? this.m.get(k)! : null; }
  key(i: number) { return [...this.m.keys()][i] ?? null; }
  removeItem(k: string) { this.m.delete(k); }
  setItem(k: string, v: string) { this.m.set(k, String(v)); }
}
for (const name of ['localStorage', 'sessionStorage'] as const) {
  let usable = false;
  try { usable = !!(window as unknown as Record<string, Storage>)[name]?.setItem; } catch { usable = false; }
  if (!usable) {
    const store = new MemoryStorage();
    Object.defineProperty(window, name, { value: store, writable: true, configurable: true });
    Object.defineProperty(globalThis, name, { value: store, writable: true, configurable: true });
  }
}

class RO { observe() {} unobserve() {} disconnect() {} }
Object.defineProperty(globalThis, 'ResizeObserver', { value: RO, writable: true });
Object.defineProperty(window, 'matchMedia', { writable: true, value: (q: string) => ({ matches: false, media: q, onchange: null, addListener: vi.fn(), removeListener: vi.fn(), addEventListener: vi.fn(), removeEventListener: vi.fn(), dispatchEvent: vi.fn() }) });
vi.mock('maplibre-gl', () => {
  class Map { on(ev: string, cb: () => void) { if (ev === 'load') setTimeout(cb, 0); return this; } addControl() { return this; } addSource() {} addLayer() {} getSource() { return { setData() {} }; } remove() {} resize() {} }
  class Marker { setLngLat() { return this; } setPopup() { return this; } addTo() { return this; } remove() {} constructor(_o?: unknown) {} }
  class Popup { setHTML() { return this; } setDOMContent() { return this; } constructor(_o?: unknown) {} }
  class NavigationControl { constructor(_o?: unknown) {} }
  return { default: { Map, Marker, Popup, NavigationControl }, Map, Marker, Popup, NavigationControl };
});
vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class { withUrl() { return this; } withAutomaticReconnect() { return this; } configureLogging() { return this; } build() { return { on() {}, onreconnecting() {}, onreconnected() {}, onclose() {}, start: () => Promise.resolve(), stop: () => Promise.resolve(), state: 'Disconnected' }; } },
  HubConnectionState: { Disconnected: 'Disconnected' },
  LogLevel: { Warning: 3 },
}));

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => {
  cleanup();
  server.resetHandlers();
  resetMockState();
  try { window.sessionStorage.clear(); window.localStorage.clear(); } catch { /* jsdom */ }
  setLocale('pl');
});
afterAll(() => server.close());
void i18n;
