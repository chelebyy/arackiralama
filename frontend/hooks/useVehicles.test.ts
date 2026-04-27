import { createElement, type ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { SWRConfig } from "swr";

import { useAvailableVehicles, useVehicle, useVehicleGroups } from "./useVehicles";
import { getAvailableVehicles, getVehicleById, getVehicleGroups } from "@/lib/api/vehicles";
import type { AvailableVehiclesParams, PaginatedResponse, Vehicle } from "@/lib/api/types";

vi.mock("@/lib/api/vehicles", () => ({
  getAvailableVehicles: vi.fn(),
  getVehicleById: vi.fn(),
  getVehicleGroups: vi.fn(),
  getOffices: vi.fn(),
}));

const mockedGetAvailableVehicles = vi.mocked(getAvailableVehicles);
const mockedGetVehicleById = vi.mocked(getVehicleById);
const mockedGetVehicleGroups = vi.mocked(getVehicleGroups);

const wrapper = ({ children }: { children: ReactNode }) =>
  createElement(SWRConfig, { value: { provider: () => new Map() } }, children);

const params: AvailableVehiclesParams = {
  pickupOfficeId: "ala",
  pickupDate: "2026-05-10",
  pickupTime: "10:00",
  returnOfficeId: "gzp",
  returnDate: "2026-05-12",
  returnTime: "09:00",
};

const vehiclePage: PaginatedResponse<Vehicle> = {
  items: [
    {
      id: "vehicle-1",
      name: "Renault Clio",
      description: "Economy hatchback",
      imageUrl: "/images/clio.jpg",
      images: [],
      groupId: "group-1",
      groupName: "Economy",
      transmission: "AUTOMATIC" as Vehicle["transmission"],
      fuelType: "PETROL" as Vehicle["fuelType"],
      seatCount: 5,
      luggageCapacity: 2,
      hasAirConditioning: true,
      minDriverAge: 21,
      minLicenseYears: 2,
      dailyPrice: 45,
      weeklyPrice: 280,
      monthlyPrice: 900,
      features: [],
      insuranceIncluded: true,
      mileageLimit: null,
      extraMileagePrice: null,
      availableExtras: [],
    },
  ],
  page: 1,
  pageSize: 10,
  totalCount: 1,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

describe("useVehicles", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("fetches available vehicles and reuses cached data across rerenders", async () => {
    mockedGetAvailableVehicles.mockResolvedValue(vehiclePage);

    const { result, rerender } = renderHook(({ currentParams }) => useAvailableVehicles(currentParams), {
      initialProps: { currentParams: params },
      wrapper,
    });

    await waitFor(() => {
      expect(result.current.vehicles).toHaveLength(1);
    });

    rerender({ currentParams: params });

    expect(mockedGetAvailableVehicles).toHaveBeenCalledTimes(1);
    expect(result.current.pagination?.totalCount).toBe(1);
  });

  it("returns vehicle details for an individual vehicle request", async () => {
    mockedGetVehicleById.mockResolvedValue(vehiclePage.items[0]);

    const { result } = renderHook(() => useVehicle("vehicle-1"), { wrapper });

    await waitFor(() => {
      expect(result.current.vehicle?.name).toBe("Renault Clio");
    });
  });

  it("surfaces fetch errors from vehicle group lookups", async () => {
    mockedGetVehicleGroups.mockRejectedValue(new Error("groups failed"));

    const { result } = renderHook(() => useVehicleGroups(), { wrapper });

    await waitFor(() => {
      expect(result.current.isError).toBeInstanceOf(Error);
    });

    expect(result.current.groups).toEqual([]);
  });
});
