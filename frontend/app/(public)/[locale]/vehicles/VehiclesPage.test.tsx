import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";

import VehiclesPage from "./page";

const useSearchParamsMock = vi.fn();
const useParamsMock = vi.fn();
const useOfficesMock = vi.fn();
const usePublicVehiclesMock = vi.fn();
const translationHasMock = vi.fn();

vi.mock("next/navigation", () => ({
  useParams: () => useParamsMock(),
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
  Link: ({ href, children, ...props }: any) => {
    const resolvedHref =
      typeof href === "string"
        ? href
        : `${href.pathname}?${new URLSearchParams(href.query).toString()}`;
    return <a href={resolvedHref} {...props}>{children}</a>;
  },
}));

vi.mock("@/hooks/useVehicles", () => ({
  useOffices: () => useOfficesMock(),
  usePublicVehicles: () => usePublicVehiclesMock(),
}));

function createVehicle(overrides: Record<string, unknown> = {}) {
  return {
    id: "vehicle-1",
    plate: "07 ABC 001",
    brand: "Renault",
    model: "Clio",
    year: 2024,
    color: "White",
    groupId: "group-1",
    groupName: "Ekonomi",
    groupNameEn: "Economy",
    officeId: "office-1",
    status: "Available",
    photoUrl: null,
    dailyPrice: 1200,
    depositAmount: 5000,
    minAge: 21,
    minLicenseYears: 2,
    features: ["Bluetooth"],
    ...overrides,
  };
}

describe("VehiclesPage", () => {
  beforeEach(() => {
    translationHasMock.mockReset();
    translationHasMock.mockReturnValue(false);
    useParamsMock.mockReturnValue({ locale: "tr" });
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
    usePublicVehiclesMock.mockReturnValue({
      vehicles: [],
      isLoading: false,
      isError: false,
    });
  });

  it("shows a loading state while available vehicles are being fetched", () => {
    usePublicVehiclesMock.mockReturnValue({
      vehicles: [],
      isLoading: true,
      isError: false,
    });

    render(<VehiclesPage />);

    expect(screen.getByText("Araçlar yükleniyor...")).toBeInTheDocument();
  });

  it("renders fetched vehicles, resolved office label, and unavailable state", () => {
    usePublicVehiclesMock.mockReturnValue({
      vehicles: [
        createVehicle({ id: "vehicle-1", brand: "Nissan", model: "Qashqai", groupNameEn: "SUV", dailyPrice: 2500 }),
        createVehicle({ id: "vehicle-2", brand: "Fiat", model: "Egea", status: "Maintenance" }),
      ],
      isLoading: false,
      isError: false,
    });

    render(<VehiclesPage />);

    expect(screen.getByText("Alanya City Center")).toBeInTheDocument();
    expect(screen.getByText("Nissan Qashqai")).toBeInTheDocument();
    expect(screen.getByText("Fiat Egea")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "bookNow" })).toHaveAttribute(
      "href",
      "/tr/booking/step3?pickup=ala&pickupDate=2026-06-10&pickupTime=10%3A00&returnDate=2026-06-14&returnTime=09%3A00&return=ala&vehicle=group-1&dailyPrice=2500&vehicleName=Nissan+Qashqai",
    );
    expect(screen.getByText("unavailable")).toBeInTheDocument();
  });

  it("opens the mobile filters drawer when filter button is clicked", () => {
    render(<VehiclesPage />);

    fireEvent.click(screen.getAllByRole("button", { name: "buttons.filter" })[0]);

    expect(screen.getAllByText("buttons.filter").length).toBeGreaterThan(1);
  });

  it("shows an error state when vehicle loading fails", () => {
    usePublicVehiclesMock.mockReturnValue({
      vehicles: [],
      isLoading: false,
      isError: true,
    });

    render(<VehiclesPage />);

    expect(screen.getByText("Araçlar yüklenemedi. Lütfen tekrar deneyin.")).toBeInTheDocument();
  });

  it("filters vehicles by selected group and uses translated category labels when available", () => {
    translationHasMock.mockImplementation((key: string) => key === "categories.suv");
    usePublicVehiclesMock.mockReturnValue({
      vehicles: [
        createVehicle({ id: "vehicle-1", brand: "Nissan", model: "Qashqai", groupName: "SUV", groupNameEn: "SUV" }),
        createVehicle({ id: "vehicle-2", brand: "Fiat", model: "Egea", groupName: "Ekonomi", groupNameEn: "Economy" }),
      ],
      isLoading: false,
      isError: false,
    });

    render(<VehiclesPage />);

    fireEvent.click(screen.getByRole("button", { name: "SUV" }));

    expect(screen.getByText("Nissan Qashqai")).toBeInTheDocument();
    expect(screen.queryByText("Fiat Egea")).not.toBeInTheDocument();
  });

  it("uses English group labels outside Turkish locale", () => {
    useParamsMock.mockReturnValue({ locale: "en" });
    usePublicVehiclesMock.mockReturnValue({
      vehicles: [createVehicle()],
      isLoading: false,
      isError: false,
    });

    render(<VehiclesPage />);

    expect(screen.getAllByText("Economy").length).toBeGreaterThan(0);
    expect(screen.queryByText("Ekonomi")).not.toBeInTheDocument();
  });

  it("switches to list view and renders pagination controls for long result sets", () => {
    usePublicVehiclesMock.mockReturnValue({
      vehicles: Array.from({ length: 7 }, (_, index) => ({
        ...createVehicle({
          id: `vehicle-${index + 1}`,
          plate: `07 ABC 00${index + 1}`,
          model: `Clio ${index + 1}`,
          dailyPrice: 1000 + index,
        }),
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

  it("uses fallback search params when required query params are missing", () => {
    useSearchParamsMock.mockReturnValue(new URLSearchParams());

    render(<VehiclesPage />);

    expect(screen.getByText("Alanya City Center")).toBeInTheDocument();
  });

  it("keeps a guid pickup value when office resolution cannot map it", () => {
    const guid = "123e4567-e89b-12d3-a456-426614174000";
    useSearchParamsMock.mockReturnValue(
      new URLSearchParams({
        pickup: guid,
        pickupDate: "2026-06-10",
        pickupTime: "10:00",
        returnDate: "2026-06-14",
        returnTime: "09:00",
      }),
    );
    usePublicVehiclesMock.mockReturnValue({
      vehicles: [createVehicle()],
      isLoading: false,
      isError: false,
    });

    render(<VehiclesPage />);

    expect(screen.getByText(guid)).toBeInTheDocument();
  });

  it("updates pagination controls as the active page changes", () => {
    usePublicVehiclesMock.mockReturnValue({
      vehicles: Array.from({ length: 7 }, (_, index) => ({
        ...createVehicle({
          id: `vehicle-${index + 1}`,
          plate: `07 ABC 00${index + 1}`,
          model: `Clio ${index + 1}`,
          dailyPrice: 1000 + index,
        }),
      })),
      isLoading: false,
      isError: false,
    });

    render(<VehiclesPage />);

    fireEvent.click(screen.getByRole("button", { name: "List view" }));
    fireEvent.click(screen.getByRole("button", { name: "buttons.next" }));

    expect(screen.getByRole("button", { name: "buttons.back" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "buttons.next" })).toBeDisabled();

    fireEvent.click(screen.getByRole("button", { name: "1" }));

    expect(screen.getByRole("button", { name: "buttons.back" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "buttons.next" })).toBeEnabled();
  });

  it("hides a broken vehicle image and keeps the fallback visible", () => {
    usePublicVehiclesMock.mockReturnValue({
      vehicles: [
        createVehicle({ brand: "Nissan", model: "Qashqai", photoUrl: "https://example.test/car.png" }),
      ],
      isLoading: false,
      isError: false,
    });

    render(<VehiclesPage />);

    const image = screen.getByRole("img", { name: "Nissan Qashqai" });
    fireEvent.error(image);

    expect(image).toHaveStyle({ display: "none" });
    expect(image.parentElement?.querySelector(".fallback")?.className).toContain("flex");
  });
});
