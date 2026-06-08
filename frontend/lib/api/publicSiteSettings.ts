import { get } from './client';
import type { PublicPaymentMethods, PublicSiteSettings } from './admin/types';

export type { PublicPaymentMethods };

export type PublicRuntimeSiteSettings = PublicSiteSettings & {
  onlinePaymentEnabled: boolean;
  paymentMethods: PublicPaymentMethods;
};

export async function getPublicSiteSettings() {
  return get<PublicRuntimeSiteSettings>('/public-site-settings');
}
