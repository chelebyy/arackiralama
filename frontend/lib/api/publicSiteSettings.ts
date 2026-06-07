import { get } from './client';
import type { PublicSiteSettings } from './admin/types';

export type PublicRuntimeSiteSettings = PublicSiteSettings & {
  onlinePaymentEnabled: boolean;
};

export async function getPublicSiteSettings() {
  return get<PublicRuntimeSiteSettings>('/public-site-settings');
}
