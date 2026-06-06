import { get } from './client';
import type { PublicSiteSettings } from './admin/types';

export async function getPublicSiteSettings() {
  return get<PublicSiteSettings>('/public-site-settings');
}
