import { adminDel, adminGet, adminPost, adminPut } from '../client';
import type {
  AdminPaginatedResponse,
  AdminResponse,
  Campaign,
  CreateCampaignData,
  CreatePricingRuleData,
  PaginatedResponse,
  PricingRule,
  PricingRuleListParams,
  UpdateCampaignData,
  UpdatePricingRuleData,
} from './types';

const PRICING_RULES_ENDPOINT = '/v1/pricing-rules';
const CAMPAIGNS_ENDPOINT = '/v1/campaigns';

type CampaignApiPayload = Omit<CreateCampaignData, 'name' | 'description' | 'minRentalDays' | 'minDays'> & {
  minDays: number;
  allowedVehicleGroupIds?: string[];
};

function toCampaignApiPayload(data: CreateCampaignData): CampaignApiPayload {
  const payload: CampaignApiPayload = {
    code: data.code,
    discountType: data.discountType,
    discountValue: data.discountValue,
    minDays: data.minDays ?? data.minRentalDays,
    validFrom: data.validFrom,
    validUntil: data.validUntil,
    isActive: data.isActive,
  };

  if (data.allowedVehicleGroupIds !== undefined) {
    payload.allowedVehicleGroupIds = data.allowedVehicleGroupIds;
  }

  return payload;
}

function normalizeCampaign(campaign: Campaign): Campaign {
  return {
    ...campaign,
    minRentalDays: campaign.minRentalDays ?? campaign.minDays,
    name: campaign.name ?? campaign.code,
    description: campaign.description ?? '',
  };
}

function normalizeCampaignResponse(response: Campaign): Campaign {
  return normalizeCampaign(response);
}

function normalizeCampaignPage(response: PaginatedResponse<Campaign>): PaginatedResponse<Campaign> {
  return {
    ...response,
    items: response.items.map(normalizeCampaign),
  };
}

function buildQueryString(params?: object): string {
  if (!params) return '';
  const searchParams = new URLSearchParams();

  Object.entries(params).forEach(([key, value]) => {
    if (
      value !== undefined &&
      value !== null &&
      value !== '' &&
      (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean')
    ) {
      searchParams.append(key, String(value));
    }
  });

  const queryString = searchParams.toString();
  return queryString ? `?${queryString}` : '';
}

function unwrapResponse<T>(response: AdminResponse<T>): T {
  if (response && typeof response === 'object' && 'data' in response) {
    return (response as { data: T }).data;
  }
  return response as T;
}

function unwrapPaginated<T>(response: AdminPaginatedResponse<T>) {
  if (Array.isArray(response)) {
    return createPaginated(response);
  }

  if (response && typeof response === 'object' && 'data' in response) {
    const data = (response as { data: PaginatedResponse<T> | T[] }).data;
    return Array.isArray(data) ? createPaginated(data) : data;
  }
  return response as PaginatedResponse<T>;
}

function createPaginated<T>(items: T[]): PaginatedResponse<T> {
  return {
    items,
    page: 1,
    pageSize: items.length,
    totalCount: items.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  };
}


export async function getPricingRules(params?: PricingRuleListParams | Record<string, unknown>) {
  const response = await adminGet<AdminPaginatedResponse<PricingRule>>(`${PRICING_RULES_ENDPOINT}${buildQueryString(params)}`);
  return unwrapPaginated(response);
}

export async function createPricingRule(data: CreatePricingRuleData) {
  const response = await adminPost<AdminResponse<PricingRule>>(PRICING_RULES_ENDPOINT, data);
  return unwrapResponse(response);
}

export async function updatePricingRule(id: string, data: UpdatePricingRuleData) {
  const response = await adminPut<AdminResponse<PricingRule>>(`${PRICING_RULES_ENDPOINT}/${id}`, data);
  return unwrapResponse(response);
}

export async function deletePricingRule(id: string): Promise<void> {
  await adminDel<void>(`${PRICING_RULES_ENDPOINT}/${id}`);
}

export async function getCampaigns() {
  const response = await adminGet<AdminPaginatedResponse<Campaign>>(CAMPAIGNS_ENDPOINT);
  return normalizeCampaignPage(unwrapPaginated(response));
}

export async function createCampaign(data: CreateCampaignData) {
  const response = await adminPost<AdminResponse<Campaign>>(CAMPAIGNS_ENDPOINT, toCampaignApiPayload(data));
  return normalizeCampaignResponse(unwrapResponse(response));
}

export async function updateCampaign(id: string, data: UpdateCampaignData) {
  const response = await adminPut<AdminResponse<Campaign>>(`${CAMPAIGNS_ENDPOINT}/${id}`, toCampaignApiPayload(data));
  return normalizeCampaignResponse(unwrapResponse(response));
}

export async function deleteCampaign(id: string): Promise<void> {
  await adminDel<void>(`${CAMPAIGNS_ENDPOINT}/${id}`);
}
