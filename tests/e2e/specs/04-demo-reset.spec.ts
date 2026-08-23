import { test, expect } from '@playwright/test';
import { apiAs } from '../helpers/api';

test('demo reset restores an identical state in under 10 s @smoke', async () => {
  const presenter = await apiAs('DemoPresenter');
  const director = await apiAs('OperationsDirector');

  const snapshot = async () => {
    const kpis = await (await director.get('/api/v1/dashboard/kpis')).json();
    return kpis.items.map((k: { code: string; value: number }) => `${k.code}=${k.value}`).join('|');
  };

  const first = await snapshot();
  const started = Date.now();
  const res = await presenter.post('/api/v1/demo/reset');
  expect(res.ok()).toBeTruthy();
  const duration = Date.now() - started;
  expect(duration).toBeLessThan(10_000);
  expect(await snapshot()).toBe(first);

  await presenter.dispose();
  await director.dispose();
});
