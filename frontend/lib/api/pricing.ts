import { get, post } from './client';
import { API_ENDPOINTS } from './config';
import type {
  PriceBreakdown,
  PriceBreakdownParams,
  ValidateCampaignParams,
  ValidateCampaignResponse,
} from './types';

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
    vehicle_group_id: params.vehicle_group_id,
    pickup_office_id: params.pickup_office_id,
    return_office_id: params.return_office_id,
    pickup_datetime: params.pickup_datetime,
    return_datetime: params.return_datetime,
    campaign_code: params.campaign_code,
    extra_driver_count: params.extra_driver_count,
    child_seat_count: params.child_seat_count,
    driver_age: params.driver_age,
    full_coverage_waiver: params.full_coverage_waiver,
  });

  return get<PriceBreakdown>(`${API_ENDPOINTS.pricing.breakdown}${queryString}`);
}

export async function validateCampaign(
  params: ValidateCampaignParams
): Promise<ValidateCampaignResponse> {
  return post<ValidateCampaignResponse>(API_ENDPOINTS.pricing.validateCampaign, params);
}
