import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import VehicleDetailPage from "./page";

const useParamsMock = vi.fn();
const useSearchParamsMock = vi.fn();
const useOfficesMock = vi.fn();
const useAvailableVehiclesMock = vi.fn();

vi.mock("next/navigation", () => ({
  useParams: () => useParamsMock(),
  useSearchParams: () => useSearchParamsMock(),
}));

vi.mock("next/link", () => ({
  default: ({ children, ...props }: any) => <a {...props}>{children}</a>,
}));

vi.mock("@/hooks/useVehicles", () => ({
  useOffices: () => useOfficesMock(),
  useAvailableVehicles: (...args: unknown[]) => useAvailableVehiclesMock(...args),
}));

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
    useOfficesMock.mockReturnValue({
      offices: [{ id: "office-1", name: "Alanya City Center" }],
    });
    useAvailableVehiclesMock.mockReturnValue({
      vehicles: [],
      isLoading: false,
      isError: false,
    });
  });

  it("shows a loading state while vehicle detail data is being fetched", () => {
    useAvailableVehiclesMock.mockReturnValue({
      vehicles: [],
      isLoading: true,
      isError: false,
    });

    render(<VehicleDetailPage />);

    expect(screen.getByText("Loading vehicle details...")).toBeInTheDocument();
  });

  it("renders the matched vehicle details and booking call to action", () => {
    useAvailableVehiclesMock.mockReturnValue({
      vehicles: [
        {
          groupId: "group-1",
          groupName: "SUV",
          groupNameEn: "SUV Elite",
          imageUrl: "",
          dailyPrice: 2500,
          features: ["Bluetooth", "CarPlay"],
          availableCount: 2,
          minAge: 24,
        },
      ],
      isLoading: false,
      isError: false,
    });

    render(<VehicleDetailPage />);

    expect(screen.getByRole("heading", { name: "SUV Elite" })).toBeInTheDocument();
    expect(screen.getByText("Bluetooth")).toBeInTheDocument();
    expect(screen.getByText("CarPlay")).toBeInTheDocument();
    expect(screen.getByText("₺10000")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Book Now" })).toHaveAttribute(
      "href",
      "/en/booking/step2?vehicle=group-1&pickup=ala&return=gzp&pickupDate=2026-06-10&returnDate=2026-06-14",
    );
  });

  it("shows an error message when vehicle details fail to load", () => {
    useAvailableVehiclesMock.mockReturnValue({
      vehicles: [],
      isLoading: false,
      isError: true,
    });

    render(<VehicleDetailPage />);

    expect(screen.getByText("Failed to load vehicle details. Please try again.")).toBeInTheDocument();
  });

  it("cycles vehicle gallery indicators when navigation buttons are clicked", () => {
    useAvailableVehiclesMock.mockReturnValue({
      vehicles: [
        {
          groupId: "group-1",
          groupName: "SUV",
          groupNameEn: "SUV Elite",
          imageUrl: "",
          dailyPrice: 2500,
          features: ["Bluetooth"],
          availableCount: 2,
          minAge: 24,
        },
      ],
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
