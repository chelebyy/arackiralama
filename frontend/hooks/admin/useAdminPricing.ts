'use client';

import useSWR from 'swr';
import {
  getPricingRules,
  getCampaigns,
  createPricingRule,
  updatePricingRule,
  deletePricingRule,
  createCampaign,
  updateCampaign,
  deleteCampaign,
} from '@/lib/api/admin/pricing';
import type { PricingRule, Campaign, PaginatedResponse } from '@/lib/api/admin';

const PRICING_KEYS = {
  rules: () => ['admin', 'pricingRules'] as const,
  rule: (id: string) => ['admin', 'pricingRules', id] as const,
  campaigns: () => ['admin', 'campaigns'] as const,
  campaign: (id: string) => ['admin', 'campaigns', id] as const,
};

export function usePricingRules(params?: Record<string, unknown>) {
  const { data, error, isLoading, mutate } = useSWR<
    PaginatedResponse<PricingRule>,
    Error
  >(PRICING_KEYS.rules(), () => getPricingRules(params), {
    revalidateOnFocus: false,
    dedupingInterval: 30000,
  });

  return {
    rules: data?.items ?? [],
    pagination: data
      ? {
          page: data.page,
          pageSize: data.pageSize,
          totalCount: data.totalCount,
          totalPages: data.totalPages,
        }
      : null,
    isLoading,
    isError: error,
    mutate,
  };
}

export function useCampaigns() {
  const { data, error, isLoading, mutate } = useSWR<
    PaginatedResponse<Campaign>,
    Error
  >(PRICING_KEYS.campaigns(), getCampaigns, {
    revalidateOnFocus: false,
    dedupingInterval: 30000,
  });

  return {
    campaigns: data?.items ?? [],
    isLoading,
    isError: error,
    mutate,
  };
}

export async function mutateCreatePricingRule(data: Parameters<typeof createPricingRule>[0]) {
  return createPricingRule(data);
}

export async function mutateUpdatePricingRule(
  id: string,
  data: Parameters<typeof updatePricingRule>[1]
) {
  return updatePricingRule(id, data);
}

export async function mutateDeletePricingRule(id: string) {
  await deletePricingRule(id);
}

export async function mutateCreateCampaign(data: Parameters<typeof createCampaign>[0]) {
  return createCampaign(data);
}

export async function mutateUpdateCampaign(
  id: string,
  data: Parameters<typeof updateCampaign>[1]
) {
  return updateCampaign(id, data);
}

export async function mutateDeleteCampaign(id: string) {
  await deleteCampaign(id);
}
