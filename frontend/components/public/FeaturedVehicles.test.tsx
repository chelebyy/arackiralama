import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";

import FeaturedVehicles from "./FeaturedVehicles";

const usePublicVehiclesMock = vi.fn();
const useParamsMock = vi.fn();

vi.mock("next/navigation", () => ({
  useParams: () => useParamsMock(),
}));

vi.mock("next-intl", () => ({
  useTranslations: () => {
    return (key: string, values?: Record<string, unknown>) => {
      if (key === "freeKm") return `Günlük ${values?.km} km dahil`;
      if (key === "pricePerDay") return "Günlük";
      if (key === "bookNow") return "Hemen Rezerve Et";
      if (key === "freeCancellation") return "Ücretsiz iptal";
      if (key === "features.seats") return "Kişi";
      if (key === "features.automatic") return "Otomatik";
      if (key === "features.gasoline") return "Benzin";
      if (key === "features.airConditioning") return "Klima";
      return key;
    };
  },
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: any) => {
    const resolvedHref =
      typeof href === "string"
        ? href
        : `${href.pathname}?${new URLSearchParams(href.query).toString()}`;
    return <a href={resolvedHref} {...props}>{children}</a>;
  },
}));

vi.mock("@/hooks/useVehicles", () => ({
  usePublicVehicles: () => usePublicVehiclesMock(),
}));

function createVehicle(overrides: Record<string, unknown> = {}) {
  return {
    id: "vehicle-1",
    plate: "07 ABC 001",
    brand: "Dacia",
    model: "Duster",
    year: 2024,
    color: "White",
    groupId: "group-suv",
    groupName: "SUV",
    groupNameEn: "SUV",
    officeId: "office-1",
    status: "Available",
    photoUrl: null,
    dailyPrice: 1800,
    depositAmount: 5000,
    minAge: 21,
    minLicenseYears: 2,
    features: ["Bluetooth"],
    ...overrides,
  };
}

describe("FeaturedVehicles", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-06-07T09:00:00"));
    useParamsMock.mockReturnValue({ locale: "tr" });
    usePublicVehiclesMock.mockReturnValue({
      vehicles: [createVehicle()],
      isLoading: false,
      isError: false,
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it("renders featured vehicles from public API data with backend price and availability-checked booking link", () => {
    render(<FeaturedVehicles />);

    expect(screen.getByRole("heading", { name: "Dacia Duster" })).toBeInTheDocument();
    expect(screen.getByText("₺ 1800")).toBeInTheDocument();
    expect(screen.getByText("SUV")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Hemen Rezerve Et" })).toHaveAttribute(
      "href",
      "/tr/booking/step2?pickup=ala&return=ala&pickupDate=2026-06-07&pickupTime=10%3A00&returnDate=2026-06-14&returnTime=10%3A00&vehicle=group-suv&dailyPrice=1800&vehicleName=Dacia+Duster"
    );
  });

  it("resolves relative backend photo URLs before rendering images", () => {
    usePublicVehiclesMock.mockReturnValue({
      vehicles: [createVehicle({ photoUrl: "/uploads/vehicles/duster.jpg" })],
      isLoading: false,
      isError: false,
    });

    render(<FeaturedVehicles />);

    expect(screen.getByRole("img", { name: "Dacia Duster" })).toHaveAttribute(
      "src",
      "http://localhost:5000/uploads/vehicles/duster.jpg"
    );
  });

  it("does not render unavailable vehicles in the featured list", () => {
    usePublicVehiclesMock.mockReturnValue({
      vehicles: [createVehicle({ status: "Maintenance" })],
      isLoading: false,
      isError: false,
    });

    render(<FeaturedVehicles />);

    expect(screen.getByText("Şu anda gösterilecek araç bulunamadı.")).toBeInTheDocument();
    expect(screen.queryByText("Dacia Duster")).not.toBeInTheDocument();
  });
});
