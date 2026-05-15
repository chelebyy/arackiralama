import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import VehiclesPage from "./page";

const useSearchParamsMock = vi.fn();
const useOfficesMock = vi.fn();
const useAvailableVehiclesMock = vi.fn();
const translationHasMock = vi.fn();

vi.mock("next/navigation", () => ({
  useSearchParams: () => useSearchParamsMock(),
}));

vi.mock("next-intl", () => ({
  useTranslations: () => {
    const t = ((key: string) => key) as ((key: string) => string) & { has: (key: string) => boolean };
    t.has = (key: string) => translationHasMock(key);
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
    translationHasMock.mockReset();
    translationHasMock.mockReturnValue(false);
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

  it("shows an error state when vehicle loading fails", () => {
    useAvailableVehiclesMock.mockReturnValue({
      vehicles: [],
      isLoading: false,
      isError: true,
    });

    render(<VehiclesPage />);

    expect(screen.getByText("Failed to load vehicles. Please try again.")).toBeInTheDocument();
  });

  it("filters vehicles by selected group and uses translated category labels when available", () => {
    translationHasMock.mockImplementation((key: string) => key === "categories.suv");
    useAvailableVehiclesMock.mockReturnValue({
      vehicles: [
        {
          groupId: "group-1",
          groupName: "SUV",
          groupNameEn: "SUV",
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
          availableCount: 1,
          minAge: 21,
        },
      ],
      isLoading: false,
      isError: false,
    });

    render(<VehiclesPage />);

    fireEvent.click(screen.getByRole("button", { name: "categories.suv" }));

    expect(screen.getByText("SUV")).toBeInTheDocument();
    expect(screen.queryByText("Economy")).not.toBeInTheDocument();
  });

  it("switches to list view and renders pagination controls for long result sets", () => {
    useAvailableVehiclesMock.mockReturnValue({
      vehicles: Array.from({ length: 7 }, (_, index) => ({
        groupId: `group-${index + 1}`,
        groupName: "Economy",
        groupNameEn: `Economy ${index + 1}`,
        imageUrl: "",
        dailyPrice: 1000 + index,
        features: ["Air Conditioning"],
        availableCount: 1,
        minAge: 21,
      })),
      isLoading: false,
      isError: false,
    });

    render(<VehiclesPage />);

    fireEvent.click(screen.getByRole("button", { name: "List view" }));

    expect(screen.getByRole("button", { name: "buttons.back" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "buttons.next" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "1" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "2" })).toBeInTheDocument();
  });
});
