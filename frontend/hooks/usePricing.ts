import useSWR from 'swr';
import { useCallback, useState } from 'react';
import { getPriceBreakdown, validateCampaign } from '@/lib/api/pricing';
import type {
  PriceBreakdown,
  PriceBreakdownParams,
  ValidateCampaignParams,
  ValidateCampaignResponse,
} from '@/lib/api/types';

const PRICING_KEYS = {
  all: ['pricing'] as const,
  breakdown: (params: PriceBreakdownParams) => [...PRICING_KEYS.all, 'breakdown', params] as const,
  campaign: (params: ValidateCampaignParams) => [...PRICING_KEYS.all, 'campaign', params] as const,
};

export function usePriceBreakdown(params: PriceBreakdownParams | null) {
  const { data, error, isLoading, mutate } = useSWR<PriceBreakdown, Error>(
    params ? PRICING_KEYS.breakdown(params) : null,
    () => getPriceBreakdown(params!),
    {
      revalidateOnFocus: false,
      revalidateIfStale: false,
      dedupingInterval: 10000,
    }
  );

  return {
    priceBreakdown: data,
    isLoading,
    isError: error,
    mutate,
  };
}

export function useValidateCampaign() {
  const [isValidating, setIsValidating] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [validatedCampaign, setValidatedCampaign] = useState<ValidateCampaignResponse | null>(null);

  const validate = useCallback(async (params: ValidateCampaignParams): Promise<ValidateCampaignResponse | null> => {
    setIsValidating(true);
    setError(null);
    try {
      const campaign = await validateCampaign(params);
      setValidatedCampaign(campaign);
      return campaign;
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Failed to validate campaign'));
      setValidatedCampaign(null);
      return null;
    } finally {
      setIsValidating(false);
    }
  }, []);

  const clearValidation = useCallback(() => {
    setValidatedCampaign(null);
    setError(null);
  }, []);

  return {
    validate,
    isValidating,
    error,
    campaign: validatedCampaign,
    clearValidation,
  };
}
