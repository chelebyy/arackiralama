import { createElement, type ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";
import { SWRConfig } from "swr";

import { usePriceBreakdown, useValidateCampaign } from "./usePricing";
import { getPriceBreakdown, validateCampaign } from "@/lib/api/pricing";
import type { PriceBreakdownParams, ValidateCampaignParams, ValidateCampaignResponse } from "@/lib/api/types";

vi.mock("@/lib/api/pricing", () => ({
  getPriceBreakdown: vi.fn(),
  validateCampaign: vi.fn(),
}));

const mockedGetPriceBreakdown = vi.mocked(getPriceBreakdown);
const mockedValidateCampaign = vi.mocked(validateCampaign);

const wrapper = ({ children }: { children: ReactNode }) =>
  createElement(SWRConfig, { value: { provider: () => new Map() } }, children);

const pricingParams: PriceBreakdownParams = {
  vehicle_group_id: "vehicle-1",
  pickup_office_id: "ala",
  pickup_datetime: "2026-05-10T10:00:00",
  return_office_id: "gzp",
  return_datetime: "2026-05-12T09:00:00",
};

describe("usePricing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("fetches a calculated price breakdown", async () => {
    mockedGetPriceBreakdown.mockResolvedValue({
      basePrice: 90,
      rentalDays: 2,
      extraFees: [],
      extrasTotal: 0,
      insuranceTotal: 0,
      subtotal: 90,
      taxRate: 20,
      taxAmount: 18,
      discountAmount: 0,
      totalAmount: 108,
      currency: "TRY",
      depositAmount: 200,
    });

    const { result } = renderHook(() => usePriceBreakdown(pricingParams), { wrapper });

    await waitFor(() => {
      expect(result.current.priceBreakdown?.totalAmount).toBe(108);
    });
  });

  it("validates campaign codes and clears the validation state", async () => {
    const params: ValidateCampaignParams = {
      code: "SUMMER15",
      vehicleGroupId: "vehicle-group-1",
      rentalDays: 3,
      pickupDate: "2026-05-10",
    };
    const response: ValidateCampaignResponse = {
      valid: true,
    };
    mockedValidateCampaign.mockResolvedValue(response);

    const { result } = renderHook(() => useValidateCampaign(), { wrapper });

    await act(async () => {
      await result.current.validate(params);
    });

    expect(mockedValidateCampaign).toHaveBeenCalledWith(params);
    expect(result.current.campaign).toEqual(response);

    act(() => {
      result.current.clearValidation();
    });

    expect(result.current.campaign).toBeNull();
    expect(result.current.error).toBeNull();
  });

  it("stores an error when campaign validation fails", async () => {
    mockedValidateCampaign.mockRejectedValue(new Error("invalid campaign"));

    const { result } = renderHook(() => useValidateCampaign(), { wrapper });

    let validated: ValidateCampaignResponse | null = {
      valid: true,
    };
    await act(async () => {
      validated = await result.current.validate({
        code: "BADCODE",
        vehicleGroupId: "vehicle-group-1",
        rentalDays: 2,
        pickupDate: "2026-05-10",
      });
    });

    expect(validated).toBeNull();
    expect(result.current.campaign).toBeNull();
    expect(result.current.error?.message).toBe("invalid campaign");
  });
});
