import { updatesApi, type UpdateStatus } from './api';

const KEY = 'ys-dismissed-update';
const read = () => { try { return localStorage.getItem(KEY); } catch { return null; } };

export const updates = $state<{ status: UpdateStatus | null; checking: boolean; dismissed: string | null; error: string | null }>({
  status: null, checking: false, dismissed: read(), error: null,
});

export async function refresh() {
  try { updates.status = await updatesApi.status(); } catch { /* the banner is optional; stay quiet */ }
}

export async function checkNow() {
  updates.checking = true; updates.error = null;
  try { updates.status = await updatesApi.check(); }
  catch (e) { updates.error = (e as Error).message; }
  finally { updates.checking = false; }
}

export function dismiss(version: string) {
  updates.dismissed = version;
  try { localStorage.setItem(KEY, version); } catch { /* private mode */ }
}

export const bannerVisible = () =>
  !!updates.status?.hubUpdateAvailable && updates.status.latestVersion !== updates.dismissed;
