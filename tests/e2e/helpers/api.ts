import { request, type APIRequestContext } from '@playwright/test';
import { API } from '../playwright.config';

export type Role =
  | 'DemoPresenter' | 'ProductionPlanner' | 'InboundCoordinator' | 'QualityInspector'
  | 'OperationsDirector' | 'Auditor' | 'Administrator' | 'SupplierUser';

export async function apiAs(role: Role, supplierCode?: string): Promise<APIRequestContext> {
  const anon = await request.newContext({ baseURL: API });
  const q = supplierCode ? `&supplierCode=${supplierCode}` : '';
  const res = await anon.get(`/api/v1/auth/demo-login?role=${role}${q}`);
  if (!res.ok()) throw new Error(`demo-login failed: ${res.status()} ${await res.text()}`);
  const { accessToken } = await res.json();
  await anon.dispose();
  return request.newContext({ baseURL: API, extraHTTPHeaders: { Authorization: `Bearer ${accessToken}` } });
}

export async function resetDemo(): Promise<void> {
  const api = await apiAs('DemoPresenter');
  const res = await api.post('/api/v1/demo/reset');
  if (!res.ok()) throw new Error(`reset failed: ${res.status()}`);
  await api.dispose();
}

export async function kpi(code: string): Promise<number> {
  const api = await apiAs('OperationsDirector');
  const res = await api.get('/api/v1/dashboard/kpis');
  const body = await res.json();
  await api.dispose();
  const item = body.items.find((k: { code: string }) => k.code === code);
  if (!item) throw new Error(`KPI ${code} missing`);
  return item.value as number;
}
