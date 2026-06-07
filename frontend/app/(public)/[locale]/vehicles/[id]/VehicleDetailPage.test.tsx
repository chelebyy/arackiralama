import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import VehicleDetailPage from "./page";

const useParamsMock = vi.fn();
const useSearchParamsMock = vi.fn();
const useVehicleMock = vi.fn();

vi.mock("next/navigation", () => ({
  useParams: () => useParamsMock(),
  useSearchParams: () => useSearchParamsMock(),
}));

vi.mock("next/link", () => ({
  default: ({ children, ...props }: any) => <a {...props}>{children}</a>,
}));

vi.mock("@/hooks/useVehicles", () => ({
  useVehicle: (...args: unknown[]) => useVehicleMock(...args),
}));

function createVehicle(overrides: Record<string, unknown> = {}) {
  return {
    id: "vehicle-1",
    plate: "07 ABC 001",
    brand: "Nissan",
    model: "Qashqai",
    year: 2024,
    color: "White",
    groupId: "group-1",
    groupName: "SUV",
    groupNameEn: "SUV Elite",
    officeId: "office-1",
    status: "Available",
    photoUrl: null,
    dailyPrice: 2500,
    depositAmount: 5000,
    minAge: 24,
    minLicenseYears: 2,
    features: ["Bluetooth", "CarPlay"],
    ...overrides,
  };
}

describe("VehicleDetailPage", () => {
  beforeEach(() => {
    useParamsMock.mockReturnValue({ locale: "en", id: "group-1" });
    useSearchParamsMock.mockReturnValue(
      new URLSearchParams({
        pickup: "ala",
        return: "gzp",
        pickupDate: "2026-06-10",
        pickupTime: "10:00",
        returnDate: "2026-06-14",
        returnTime: "09:00",
      }),
    );
    useVehicleMock.mockReturnValue({
      vehicle: null,
      isLoading: false,
      isError: false,
    });
  });

  it("shows a loading state while vehicle detail data is being fetched", () => {
    useVehicleMock.mockReturnValue({
      vehicle: null,
      isLoading: true,
      isError: false,
    });

    render(<VehicleDetailPage />);

    expect(screen.getByText("Loading vehicle details...")).toBeInTheDocument();
  });

  it("renders the matched vehicle details and booking call to action", () => {
    useVehicleMock.mockReturnValue({
      vehicle: createVehicle(),
      isLoading: false,
      isError: false,
    });

    render(<VehicleDetailPage />);

    expect(screen.getByRole("heading", { name: "Nissan Qashqai" })).toBeInTheDocument();
    expect(screen.getByText("Bluetooth")).toBeInTheDocument();
    expect(screen.getByText("CarPlay")).toBeInTheDocument();
    expect(screen.getByText("₺10000")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Book Now" })).toHaveAttribute(
      "href",
      "/en/booking/step3?pickup=ala&return=gzp&pickupDate=2026-06-10&pickupTime=10%3A00&returnDate=2026-06-14&returnTime=09%3A00&vehicle=group-1&dailyPrice=2500&vehicleName=Nissan+Qashqai",
    );
  });

  it("uses the Turkish group name when the route locale is Turkish", () => {
    useParamsMock.mockReturnValue({ locale: "tr", id: "vehicle-1" });
    useVehicleMock.mockReturnValue({
      vehicle: createVehicle({ groupName: "SUV Türkçe", groupNameEn: "SUV English" }),
      isLoading: false,
      isError: false,
    });

    render(<VehicleDetailPage />);

    expect(screen.getByText("SUV Türkçe")).toBeInTheDocument();
    expect(screen.queryByText("SUV English")).not.toBeInTheDocument();
  });

  it("shows an error message when vehicle details fail to load", () => {
    useVehicleMock.mockReturnValue({
      vehicle: null,
      isLoading: false,
      isError: true,
    });

    render(<VehicleDetailPage />);

    expect(screen.getByText("Failed to load vehicle details. Please try again.")).toBeInTheDocument();
  });

  it("cycles vehicle gallery indicators when navigation buttons are clicked", () => {
    useVehicleMock.mockReturnValue({
      vehicle: createVehicle({ features: ["Bluetooth"] }),
      isLoading: false,
      isError: false,
    });

    render(<VehicleDetailPage />);

    const nextButton = screen.getByRole("button", { name: "Next image" });
    fireEvent.click(nextButton);

    const indicators = screen.getAllByRole("button", { name: /View image/i });
    expect(indicators[1].className).toContain("bg-sky-600");
  });
});
