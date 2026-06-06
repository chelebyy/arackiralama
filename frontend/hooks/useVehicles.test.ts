import { createElement, type ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { SWRConfig } from "swr";

import { useAvailableVehicles, usePublicVehicles, useVehicle, useVehicleGroups } from "./useVehicles";
import {
  getAvailableVehicles,
  getPublicVehicles,
  getVehicleById,
  getVehicleGroups,
} from "@/lib/api/vehicles";
import type { AvailableVehicleGroup, AvailableVehiclesParams, PublicVehicle } from "@/lib/api/types";

vi.mock("@/lib/api/vehicles", () => ({
  getAvailableVehicles: vi.fn(),
  getPublicVehicles: vi.fn(),
  getVehicleById: vi.fn(),
  getVehicleGroups: vi.fn(),
  getOffices: vi.fn(),
}));

const mockedGetAvailableVehicles = vi.mocked(getAvailableVehicles);
const mockedGetPublicVehicles = vi.mocked(getPublicVehicles);
const mockedGetVehicleById = vi.mocked(getVehicleById);
const mockedGetVehicleGroups = vi.mocked(getVehicleGroups);

const wrapper = ({ children }: { children: ReactNode }) =>
  createElement(SWRConfig, { value: { provider: () => new Map() } }, children);

const params: AvailableVehiclesParams = {
  office_id: "ala",
  pickup_datetime: "2026-05-10T10:00:00",
  return_datetime: "2026-05-12T09:00:00",
};

const availableGroups: AvailableVehicleGroup[] = [
  {
    groupId: "group-1",
    groupName: "Economy",
    groupNameEn: "Economy",
    availableCount: 3,
    dailyPrice: 45,
    currency: "TRY",
    depositAmount: 500,
    minAge: 21,
    minLicenseYears: 2,
    features: ["A/C", "Bluetooth", "GPS"],
    imageUrl: "/images/clio.jpg",
  },
];

const sampleVehicle: PublicVehicle = {
  id: "vehicle-1",
  plate: "07 ABC 001",
  brand: "Renault",
  model: "Clio",
  year: 2024,
  color: "White",
  groupId: "group-1",
  groupName: "Economy",
  groupNameEn: "Economy",
  officeId: "office-1",
  status: "Available",
  photoUrl: "/uploads/vehicles/clio.jpg",
  dailyPrice: 45,
  depositAmount: 500,
  minAge: 21,
  minLicenseYears: 2,
  features: ["A/C"],
};

describe("useVehicles", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("fetches available vehicles and reuses cached data across rerenders", async () => {
    mockedGetAvailableVehicles.mockResolvedValue(availableGroups);

    const { result, rerender } = renderHook(({ currentParams }) => useAvailableVehicles(currentParams), {
      initialProps: { currentParams: params },
      wrapper,
    });

    await waitFor(() => {
      expect(result.current.vehicles).toHaveLength(1);
    });

    rerender({ currentParams: params });

    expect(mockedGetAvailableVehicles).toHaveBeenCalledTimes(1);
    expect(result.current.vehicles[0].groupName).toBe("Economy");
  });

  it("returns vehicle details for an individual vehicle request", async () => {
    mockedGetVehicleById.mockResolvedValue(sampleVehicle);

    const { result } = renderHook(() => useVehicle("vehicle-1"), { wrapper });

    await waitFor(() => {
      expect(result.current.vehicle?.brand).toBe("Renault");
    });
  });

  it("fetches physical public vehicles for the fleet screen", async () => {
    mockedGetPublicVehicles.mockResolvedValue([sampleVehicle]);

    const { result } = renderHook(() => usePublicVehicles(), { wrapper });

    await waitFor(() => {
      expect(result.current.vehicles).toHaveLength(1);
    });

    expect(result.current.vehicles[0].plate).toBe("07 ABC 001");
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
