import { get, post } from './client';
import { API_ENDPOINTS } from './config';
import type { Campaign, PriceBreakdown, PriceBreakdownParams } from './types';

function buildQueryString(params: Record<string, string | number | boolean | undefined>): string {
  const searchParams = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      searchParams.append(key, String(value));
    }
  });
  const query = searchParams.toString();
  return query ? `?${query}` : '';
}

export async function getPriceBreakdown(params: PriceBreakdownParams): Promise<PriceBreakdown> {
  const queryString = buildQueryString({
    vehicleId: params.vehicleId,
    pickupOfficeId: params.pickupOfficeId,
    pickupDate: params.pickupDate,
    pickupTime: params.pickupTime,
    returnOfficeId: params.returnOfficeId,
    returnDate: params.returnDate,
    returnTime: params.returnTime,
    campaignCode: params.campaignCode,
  });

  return get<PriceBreakdown>(`${API_ENDPOINTS.pricing.breakdown}${queryString}`);
}

export async function validateCampaign(code: string): Promise<Campaign> {
  return post<Campaign>(API_ENDPOINTS.pricing.validateCampaign, { code });
}
