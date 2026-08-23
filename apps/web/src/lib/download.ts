import { buildUrl, getToken, ApiError } from '@/api/client';

/** Fetches a binary resource through the API with auth and opens / saves it client-side. */
export async function downloadFile(path: string, fileName: string, opts: { openInNewTab?: boolean } = {}): Promise<void> {
  const headers: Record<string, string> = {};
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(buildUrl(path), { headers });
  if (!res.ok) {
    let problem = null;
    try {
      problem = await res.json();
    } catch {
      problem = null;
    }
    throw new ApiError(res.status, problem);
  }
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  if (opts.openInNewTab) {
    window.open(url, '_blank', 'noopener');
  } else {
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
  }
  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

export async function copyText(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}
