import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import VehiclesPage from "./page";

const useSearchParamsMock = vi.fn();
const useOfficesMock = vi.fn();
const useAvailableVehiclesMock = vi.fn();

vi.mock("next/navigation", () => ({
  useSearchParams: () => useSearchParamsMock(),
}));

vi.mock("next-intl", () => ({
  useTranslations: () => {
    const t = ((key: string) => key) as ((key: string) => string) & { has: (key: string) => boolean };
    t.has = () => false;
    return t;
  },
}));

vi.mock("@/i18n/routing", () => ({
  Link: ({ children, ...props }: any) => <a {...props}>{children}</a>,
}));

vi.mock("@/hooks/useVehicles", () => ({
  useOffices: () => useOfficesMock(),
  useAvailableVehicles: (...args: unknown[]) => useAvailableVehiclesMock(...args),
}));

describe("VehiclesPage", () => {
  beforeEach(() => {
    useSearchParamsMock.mockReturnValue(
      new URLSearchParams({
        pickup: "ala",
        pickupDate: "2026-06-10",
        pickupTime: "10:00",
        returnDate: "2026-06-14",
        returnTime: "09:00",
      }),
    );
    useOfficesMock.mockReturnValue({
      offices: [{ id: "office-1", name: "Alanya City Center" }],
      isLoading: false,
    });
    useAvailableVehiclesMock.mockReturnValue({
      vehicles: [],
      isLoading: false,
      isError: false,
    });
  });

  it("shows a loading state while available vehicles are being fetched", () => {
    useAvailableVehiclesMock.mockReturnValue({
      vehicles: [],
      isLoading: true,
      isError: false,
    });

    render(<VehiclesPage />);

    expect(screen.getByText("Loading available vehicles...")).toBeInTheDocument();
  });

  it("renders fetched vehicles, resolved office label, and unavailable state", () => {
    useAvailableVehiclesMock.mockReturnValue({
      vehicles: [
        {
          groupId: "group-1",
          groupName: "SUV",
          groupNameEn: "SUV Elite",
          imageUrl: "",
          dailyPrice: 2500,
          features: ["Bluetooth"],
          availableCount: 1,
          minAge: 24,
        },
        {
          groupId: "group-2",
          groupName: "Economy",
          groupNameEn: "Economy",
          imageUrl: "",
          dailyPrice: 1200,
          features: ["Air Conditioning"],
          availableCount: 0,
          minAge: 21,
        },
      ],
      isLoading: false,
      isError: false,
    });

    render(<VehiclesPage />);

    expect(screen.getByText("Alanya City Center")).toBeInTheDocument();
    expect(screen.getByText("SUV Elite")).toBeInTheDocument();
    expect(screen.getByText("Economy")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "bookNow" })).toBeInTheDocument();
    expect(screen.getByText("unavailable")).toBeInTheDocument();
  });

  it("opens the mobile filters drawer when filter button is clicked", () => {
    render(<VehiclesPage />);

    fireEvent.click(screen.getAllByRole("button", { name: "buttons.filter" })[0]);

    expect(screen.getAllByText("buttons.filter").length).toBeGreaterThan(1);
  });
});
