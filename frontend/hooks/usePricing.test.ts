import { createElement, type ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";
import { SWRConfig } from "swr";

import { usePriceBreakdown, useValidateCampaign } from "./usePricing";
import { getPriceBreakdown, validateCampaign } from "@/lib/api/pricing";
import type { Campaign, PriceBreakdownParams } from "@/lib/api/types";

vi.mock("@/lib/api/pricing", () => ({
  getPriceBreakdown: vi.fn(),
  validateCampaign: vi.fn(),
}));

const mockedGetPriceBreakdown = vi.mocked(getPriceBreakdown);
const mockedValidateCampaign = vi.mocked(validateCampaign);

const wrapper = ({ children }: { children: ReactNode }) =>
  createElement(SWRConfig, { value: { provider: () => new Map() } }, children);

const pricingParams: PriceBreakdownParams = {
  vehicleId: "vehicle-1",
  pickupOfficeId: "ala",
  pickupDate: "2026-05-10",
  pickupTime: "10:00",
  returnOfficeId: "gzp",
  returnDate: "2026-05-12",
  returnTime: "09:00",
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
      currency: "EUR",
      depositAmount: 200,
    });

    const { result } = renderHook(() => usePriceBreakdown(pricingParams), { wrapper });

    await waitFor(() => {
      expect(result.current.priceBreakdown?.totalAmount).toBe(108);
    });
  });

  it("validates campaign codes and clears the validation state", async () => {
    const campaign: Campaign = {
      id: "campaign-1",
      code: "SUMMER15",
      name: "Summer",
      description: "15% off",
      discountType: "PERCENTAGE",
      discountValue: 15,
      validFrom: "2026-05-01",
      validUntil: "2026-05-31",
      isActive: true,
    };
    mockedValidateCampaign.mockResolvedValue(campaign);

    const { result } = renderHook(() => useValidateCampaign(), { wrapper });

    await act(async () => {
      await result.current.validate("SUMMER15");
    });

    expect(result.current.campaign).toEqual(campaign);

    act(() => {
      result.current.clearValidation();
    });

    expect(result.current.campaign).toBeNull();
    expect(result.current.error).toBeNull();
  });

  it("stores an error when campaign validation fails", async () => {
    mockedValidateCampaign.mockRejectedValue(new Error("invalid campaign"));

    const { result } = renderHook(() => useValidateCampaign(), { wrapper });

    let validated: Campaign | null = {
      id: "x",
      code: "x",
      name: "x",
      description: "x",
      discountType: "PERCENTAGE",
      discountValue: 1,
      validFrom: "x",
      validUntil: "x",
      isActive: true,
    };
    await act(async () => {
      validated = await result.current.validate("BADCODE");
    });

    expect(validated).toBeNull();
    expect(result.current.campaign).toBeNull();
    expect(result.current.error?.message).toBe("invalid campaign");
  });
});
