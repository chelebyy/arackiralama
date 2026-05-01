import { createElement, type ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";
import { SWRConfig } from "swr";

import {
  useCreateReservation,
  useExtendHold,
  usePlaceHold,
  useReservation,
} from "./useReservations";
import {
  createReservation,
  extendHold,
  getReservationByPublicCode,
  placeHold,
} from "@/lib/api/reservations";
import type { CreateReservationData, Reservation } from "@/lib/api/types";

vi.mock("@/lib/api/reservations", () => ({
  createReservation: vi.fn(),
  extendHold: vi.fn(),
  getReservationByPublicCode: vi.fn(),
  placeHold: vi.fn(),
}));

const mockedCreateReservation = vi.mocked(createReservation);
const mockedExtendHold = vi.mocked(extendHold);
const mockedGetReservationByPublicCode = vi.mocked(getReservationByPublicCode);
const mockedPlaceHold = vi.mocked(placeHold);

const wrapper = ({ children }: { children: ReactNode }) =>
  createElement(SWRConfig, { value: { provider: () => new Map() } }, children);

const reservation: Reservation = {
  id: "reservation-1",
  publicCode: "ALN-123",
  status: "PENDING" as Reservation["status"],
  vehicleId: "vehicle-1",
  vehicleName: "Renault Clio",
  vehicleImage: "/images/clio.jpg",
  pickupOfficeId: "ala",
  pickupOfficeName: "Alanya",
  pickupDate: "2026-05-10",
  pickupTime: "10:00",
  returnOfficeId: "gzp",
  returnOfficeName: "Gazipasa",
  returnDate: "2026-05-12",
  returnTime: "09:00",
  customer: { firstName: "Jane", lastName: "Doe", email: "jane@example.com", phone: "+905551234567" },
  driver: {
    firstName: "Jane",
    lastName: "Doe",
    dateOfBirth: "1990-05-10",
    licenseNumber: "TR12345",
    licenseCountry: "TR",
    licenseIssueDate: "2015-05-10",
    licenseExpiryDate: "2030-05-10",
    isPrimaryDriver: true,
  },
  extras: [],
  priceBreakdown: {
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
  },
  createdAt: "2026-04-27T10:00:00.000Z",
  updatedAt: "2026-04-27T10:00:00.000Z",
  paymentStatus: "PENDING" as Reservation["paymentStatus"],
};

const createData: CreateReservationData = {
  vehicleGroupId: "vehicle-1",
  pickupOfficeId: "ala",
  returnOfficeId: "gzp",
  pickupDateTimeUtc: "2026-05-10T10:00:00Z",
  returnDateTimeUtc: "2026-05-12T09:00:00Z",
  customer: reservation.customer,
};

describe("useReservations", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("fetches a reservation by public code", async () => {
    mockedGetReservationByPublicCode.mockResolvedValue(reservation);

    const { result } = renderHook(() => useReservation("ALN-123"), { wrapper });

    await waitFor(() => {
      expect(result.current.reservation?.publicCode).toBe("ALN-123");
    });
  });

  it("creates reservations through the mutation hook", async () => {
    mockedCreateReservation.mockResolvedValue(reservation);

    const { result } = renderHook(() => useCreateReservation(), { wrapper });

    let created: Reservation | null = null;
    await act(async () => {
      created = await result.current.create(createData);
    });

    expect(mockedCreateReservation).toHaveBeenCalledWith(createData);
    expect(created).toEqual(reservation);
    expect(result.current.error).toBeNull();
  });

  it("returns null and stores the error when placing a hold fails", async () => {
    mockedPlaceHold.mockRejectedValue(new Error("hold failed"));

    const { result } = renderHook(() => usePlaceHold(), { wrapper });

    let holdResult: Reservation | null = reservation;
    await act(async () => {
      holdResult = await result.current.placeHold("reservation-1", { durationMinutes: 30 });
    });

    expect(holdResult).toBeNull();
    expect(result.current.error?.message).toBe("hold failed");
  });

  it("extends an existing hold and returns the updated reservation", async () => {
    mockedExtendHold.mockResolvedValue({ ...reservation, expiresAt: "2026-04-27T10:45:00.000Z" });

    const { result } = renderHook(() => useExtendHold(), { wrapper });

    let extended: Reservation | null = null;
    await act(async () => {
      extended = await result.current.extend("reservation-1", { additionalMinutes: 15 });
    });

    expect(mockedExtendHold).toHaveBeenCalledWith("reservation-1", { additionalMinutes: 15 });
    expect(extended).not.toBeNull();
    expect(extended!.expiresAt).toBe("2026-04-27T10:45:00.000Z");
  });
});
