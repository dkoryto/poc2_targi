import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '@/i18n';
import '@/styles/global.css';
import 'maplibre-gl/dist/maplibre-gl.css';
import { App } from './App';

async function bootstrap() {
  if (import.meta.env.VITE_USE_MOCKS === 'true') {
    const { worker } = await import('./mocks/browser');
    await worker.start({ onUnhandledRequest: 'bypass' });
  }
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
}
void bootstrap();
