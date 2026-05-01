import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import BookingStep2Page from "./page";

const pushMock = vi.fn();
let searchParams = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useParams: () => ({ locale: "en" }),
  useRouter: () => ({ push: pushMock }),
  useSearchParams: () => searchParams,
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: { href: string; children: React.ReactNode }) => <a href={href} {...props}>{children}</a>,
}));

vi.mock("@/hooks/useVehicles", () => ({
  useAvailableVehicles: () => ({
    vehicles: [
      { groupId: "economy", groupName: "Fiat Egea Or Similar", groupNameEn: "Fiat Egea Or Similar", availableCount: 5, dailyPrice: 45, currency: "TRY", depositAmount: 500, minAge: 21, minLicenseYears: 2, features: ["A/C", "Bluetooth", "GPS"], imageUrl: null },
      { groupId: "compact", groupName: "Renault Megane Or Similar", groupNameEn: "Renault Megane Or Similar", availableCount: 3, dailyPrice: 55, currency: "TRY", depositAmount: 600, minAge: 21, minLicenseYears: 2, features: ["A/C", "Cruise Control", "Parking Sensors"], imageUrl: null },
      { groupId: "midsize", groupName: "VW Passat Or Similar", groupNameEn: "VW Passat Or Similar", availableCount: 2, dailyPrice: 75, currency: "TRY", depositAmount: 800, minAge: 23, minLicenseYears: 3, features: ["Leather Seats", "Sunroof", "Navigation"], imageUrl: null },
      { groupId: "luxury", groupName: "BMW 3 Series Or Similar", groupNameEn: "BMW 3 Series Or Similar", availableCount: 2, dailyPrice: 95, currency: "TRY", depositAmount: 1000, minAge: 25, minLicenseYears: 5, features: ["Leather Seats", "Premium Sound", "Parking Assistant"], imageUrl: null },
      { groupId: "minivan", groupName: "Mercedes Vito Or Similar", groupNameEn: "Mercedes Vito Or Similar", availableCount: 1, dailyPrice: 120, currency: "TRY", depositAmount: 1200, minAge: 25, minLicenseYears: 5, features: ["Extra Space", "Dual A/C", "Rear Camera"], imageUrl: null },
      { groupId: "suv", groupName: "Audi Q5 Or Similar", groupNameEn: "Audi Q5 Or Similar", availableCount: 2, dailyPrice: 110, currency: "TRY", depositAmount: 1000, minAge: 25, minLicenseYears: 5, features: ["4WD", "Panoramic Roof", "Virtual Cockpit"], imageUrl: null },
    ],
    isLoading: false,
    isError: null,
    mutate: vi.fn(),
  }),
  useOffices: () => ({
    offices: [
      { id: "11111111-1111-1111-1111-111111111111", name: "Alanya Şehir Merkezi" },
      { id: "22222222-2222-2222-2222-222222222222", name: "Gazipaşa Havalimanı" },
      { id: "33333333-3333-3333-3333-333333333333", name: "Antalya Havalimanı" },
    ],
    isLoading: false,
    isError: null,
  }),
}));

describe("BookingStep2Page", () => {
  beforeEach(() => {
    pushMock.mockReset();
    searchParams = new URLSearchParams({
      pickup: "ala",
      return: "gzp",
      pickupDate: "2026-05-10",
      pickupTime: "10:00",
      returnDate: "2026-05-13",
      returnTime: "09:00",
    });
  });

  it("displays rental duration pricing for each vehicle group", () => {
    render(<BookingStep2Page />);

    expect(screen.getAllByText("Total for 3 days:")).toHaveLength(6);
    expect(screen.getByText("₺135")).toBeInTheDocument();
    expect(screen.getByText("₺165")).toBeInTheDocument();
  });

  it("keeps the continue button disabled until a vehicle is selected", async () => {
    const user = userEvent.setup();

    render(<BookingStep2Page />);

    const continueButton = screen.getByRole("button", { name: /^continue$/i });
    expect(continueButton).toBeDisabled();

    await user.click(screen.getByRole("button", { name: /fiat egea or similar/i }));

    expect(continueButton).toBeEnabled();
  });

  it("navigates to step 3 with the selected vehicle id", async () => {
    const user = userEvent.setup();

    render(<BookingStep2Page />);

    await user.click(screen.getByRole("button", { name: /renault megane or similar/i }));
    await user.click(screen.getByRole("button", { name: /^continue$/i }));

    expect(pushMock).toHaveBeenCalledWith(
      "/en/booking/step3?pickup=ala&return=gzp&pickupDate=2026-05-10&pickupTime=10%3A00&returnDate=2026-05-13&returnTime=09%3A00&vehicle=compact"
    );
  });
});
