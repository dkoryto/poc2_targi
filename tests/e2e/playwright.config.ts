import { defineConfig, devices } from '@playwright/test';

const WEB = process.env.E2E_WEB_URL ?? 'http://localhost:5173';
export const API = process.env.E2E_API_URL ?? 'http://localhost:5080';

export default defineConfig({
  testDir: './specs',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: WEB,
    viewport: { width: 1920, height: 1080 },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    locale: 'pl-PL',
    timezoneId: 'Europe/Warsaw',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1920, height: 1080 } } }],
});
