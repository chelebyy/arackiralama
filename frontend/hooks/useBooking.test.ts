import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { act, renderHook } from "@testing-library/react";

import { useBookingActions, useBookingState, useBookingStore } from "./useBooking";
import type { Customer, Driver, Office, ReservationExtra, Vehicle } from "@/lib/api/types";

const sampleVehicle: Vehicle = {
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
};

const sampleOffice: Office = {
  id: "ala",
  name: "Alanya",
  code: "ALA",
  address: "Center",
  city: "Alanya",
  district: "Center",
  phone: "+90 555 000 0000",
  email: "office@example.com",
  coordinates: { latitude: 36.5, longitude: 32 },
  openingHours: {
    monday: { open: "08:00", close: "22:00", isClosed: false },
    tuesday: { open: "08:00", close: "22:00", isClosed: false },
    wednesday: { open: "08:00", close: "22:00", isClosed: false },
    thursday: { open: "08:00", close: "22:00", isClosed: false },
    friday: { open: "08:00", close: "22:00", isClosed: false },
    saturday: { open: "08:00", close: "22:00", isClosed: false },
    sunday: { open: "08:00", close: "22:00", isClosed: false },
  },
  isAirport: false,
  isHotel: false,
  services: [],
  isActive: true,
};

const sampleCustomer: Customer = {
  firstName: "Jane",
  lastName: "Doe",
  email: "jane@example.com",
  phone: "+905551234567",
};

const sampleDriver: Driver = {
  firstName: "Jane",
  lastName: "Doe",
  dateOfBirth: "1990-05-10",
  licenseNumber: "TR12345",
  licenseCountry: "TR",
  licenseIssueDate: "2015-05-10",
  licenseExpiryDate: "2030-05-10",
  isPrimaryDriver: true,
};

function resetBookingStore() {
  act(() => {
    useBookingStore.getState().clearBooking();
    localStorage.clear();
  });
}

describe("useBooking", () => {
  beforeEach(() => {
    resetBookingStore();
  });

  afterEach(() => {
    resetBookingStore();
  });

  it("stores selected dates and vehicle details through booking actions", () => {
    const { result: state } = renderHook(() => useBookingState());
    const { result: actions } = renderHook(() => useBookingActions());

    act(() => {
      actions.current.setDates({
        pickupOfficeId: "ala",
        pickupOfficeName: "Alanya",
        pickupDate: "2026-05-10",
        pickupTime: "10:00",
        returnOfficeId: "gzp",
        returnOfficeName: "Gazipasa",
        returnDate: "2026-05-12",
        returnTime: "09:00",
      });
      actions.current.selectVehicle(sampleVehicle, sampleOffice, sampleOffice);
    });

    expect(state.current.dates?.pickupOfficeId).toBe("ala");
    expect(state.current.vehicle).toMatchObject({
      vehicleId: "vehicle-1",
      vehicleName: "Renault Clio",
      dailyPrice: 45,
      groupName: "Economy",
    });
    expect(state.current.step).toBe("vehicles");
  });

  it("calculates extras totals from reservation extras", () => {
    const { result: actions } = renderHook(() => useBookingActions());
    const extras: ReservationExtra[] = [
      { extraId: "gps", name: "GPS", quantity: 1, dailyPrice: 8, totalPrice: 24 },
      { extraId: "wifi", name: "WiFi", quantity: 1, dailyPrice: 12, totalPrice: 36 },
    ];

    act(() => {
      actions.current.updateExtras(extras);
    });

    expect(useBookingStore.getState().extras).toEqual({
      items: extras,
      total: 60,
    });
  });

  it("marks booking complete once dates, vehicle, customer, and driver are present", () => {
    const { result: state } = renderHook(() => useBookingState());
    const { result: actions } = renderHook(() => useBookingActions());

    act(() => {
      actions.current.setDates({
        pickupOfficeId: "ala",
        pickupOfficeName: "Alanya",
        pickupDate: "2026-05-10",
        pickupTime: "10:00",
        returnOfficeId: "gzp",
        returnOfficeName: "Gazipasa",
        returnDate: "2026-05-12",
        returnTime: "09:00",
      });
      actions.current.selectVehicle(sampleVehicle, sampleOffice, sampleOffice);
      actions.current.updateCustomerDetails(sampleCustomer, sampleDriver);
      actions.current.applyCampaign("SUMMER15", 15);
    });

    expect(state.current.customer).toEqual(sampleCustomer);
    expect(state.current.driver).toEqual(sampleDriver);
    expect(state.current.campaignCode).toBe("SUMMER15");
    expect(state.current.campaignDiscount).toBe(15);
    expect(state.current.isComplete).toBe(true);
  });

  it("moves between steps and resets booking state", () => {
    const { result: actions } = renderHook(() => useBookingActions());

    act(() => {
      actions.current.goToNextStep();
      actions.current.goToNextStep();
      actions.current.goToPreviousStep();
    });

    expect(useBookingStore.getState().step).toBe("vehicles");

    act(() => {
      actions.current.resetBooking();
    });

    expect(useBookingStore.getState()).toMatchObject({
      dates: null,
      vehicle: null,
      customer: null,
      driver: null,
      campaignCode: null,
      campaignDiscount: null,
      step: "search",
    });
  });
});
