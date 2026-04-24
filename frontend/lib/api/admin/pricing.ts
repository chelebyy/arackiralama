import { del, get, post, put } from '../client';
import { mockCampaigns, mockPricingRules } from './mock';
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

const USE_MOCK = false;

const PRICING_RULES_ENDPOINT = '/admin/v1/pricing-rules';
const CAMPAIGNS_ENDPOINT = '/admin/v1/campaigns';

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
  if (response && typeof response === 'object' && 'data' in response) {
    return (response as { data: PaginatedResponse<T> }).data;
  }
  return response as PaginatedResponse<T>;
}

function createMockPaginated<T>(items: T[]): PaginatedResponse<T> {
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
  if (USE_MOCK) {
    return createMockPaginated(mockPricingRules);
  }
  const response = await get<AdminPaginatedResponse<PricingRule>>(`${PRICING_RULES_ENDPOINT}${buildQueryString(params)}`);
  return unwrapPaginated(response);
}

export async function createPricingRule(data: CreatePricingRuleData) {
  if (USE_MOCK) {
    return mockPricingRules[0];
  }
  const response = await post<AdminResponse<PricingRule>>(PRICING_RULES_ENDPOINT, data);
  return unwrapResponse(response);
}

export async function updatePricingRule(id: string, data: UpdatePricingRuleData) {
  if (USE_MOCK) {
    return mockPricingRules[0];
  }
  const response = await put<AdminResponse<PricingRule>>(`${PRICING_RULES_ENDPOINT}/${id}`, data);
  return unwrapResponse(response);
}

export async function deletePricingRule(id: string): Promise<void> {
  if (USE_MOCK) {
    return Promise.resolve();
  }
  await del<void>(`${PRICING_RULES_ENDPOINT}/${id}`);
}

export async function getCampaigns() {
  if (USE_MOCK) {
    return createMockPaginated(mockCampaigns);
  }
  const response = await get<AdminPaginatedResponse<Campaign>>(CAMPAIGNS_ENDPOINT);
  return unwrapPaginated(response);
}

export async function createCampaign(data: CreateCampaignData) {
  if (USE_MOCK) {
    return mockCampaigns[0];
  }
  const response = await post<AdminResponse<Campaign>>(CAMPAIGNS_ENDPOINT, data);
  return unwrapResponse(response);
}

export async function updateCampaign(id: string, data: UpdateCampaignData) {
  if (USE_MOCK) {
    return mockCampaigns[0];
  }
  const response = await put<AdminResponse<Campaign>>(`${CAMPAIGNS_ENDPOINT}/${id}`, data);
  return unwrapResponse(response);
}

export async function deleteCampaign(id: string): Promise<void> {
  if (USE_MOCK) {
    return Promise.resolve();
  }
  await del<void>(`${CAMPAIGNS_ENDPOINT}/${id}`);
}
