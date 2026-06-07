import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import BookingStep3Page from "./page";

const pushMock = vi.fn();
const updateCustomerDetailsMock = vi.fn();
const setDatesMock = vi.fn();
const useOfficesMock = vi.fn();
let searchParams = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useParams: () => ({ locale: "en" }),
  useRouter: () => ({ push: pushMock }),
  useSearchParams: () => searchParams,
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: { href: string; children: React.ReactNode }) => <a href={href} {...props}>{children}</a>,
}));

vi.mock("@/hooks/useBooking", () => ({
  useBookingActions: () => ({
    updateCustomerDetails: updateCustomerDetailsMock,
    setDates: setDatesMock,
  }),
}));

vi.mock("@/hooks/useVehicles", () => ({
  useOffices: () => useOfficesMock(),
}));

describe("BookingStep3Page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    searchParams = new URLSearchParams({ vehicle: "compact" });
    useOfficesMock.mockReturnValue({
      offices: [
        { id: "office-ala", name: "Alanya City Center" },
        { id: "office-gzp", name: "Gazipasa Airport" },
      ],
    });
  });

  it("validates required customer details before advancing", async () => {
    const user = userEvent.setup();

    render(<BookingStep3Page />);

    await user.click(screen.getByRole("button", { name: /continue to payment/i }));

    expect(await screen.findByText("First name is required")).toBeInTheDocument();
    expect(screen.getByText("Last name is required")).toBeInTheDocument();
    expect(screen.getByText("Invalid email address")).toBeInTheDocument();
    expect(pushMock).not.toHaveBeenCalled();
  });

  it("lets customers toggle paid extras while completing visible driver fields", async () => {
    const user = userEvent.setup();

    render(<BookingStep3Page />);

    await user.type(screen.getByLabelText("First Name"), "Jane");
    await user.type(screen.getByLabelText("Last Name"), "Doe");
    await user.type(screen.getByLabelText("Email"), "jane@example.com");
    await user.type(screen.getByLabelText("Phone"), "+905551234567");
    await user.type(screen.getByLabelText("Date of Birth"), "1990-05-10");
    await user.type(screen.getByLabelText("License Number"), "TR-12345");
    const childSeatOption = screen.getByRole("button", { name: /child seat/i });
    const gpsOption = screen.getByRole("button", { name: /gps navigation/i });

    await user.click(childSeatOption);
    await user.click(gpsOption);

    expect(childSeatOption).toHaveClass("border-sky-600");
    expect(gpsOption).toHaveClass("border-sky-600");
    expect(pushMock).not.toHaveBeenCalled();
  });

  it("stores resolved pickup and return offices before continuing from a direct vehicle selection", async () => {
    const user = userEvent.setup();
    searchParams = new URLSearchParams({
      pickup: "ala",
      return: "gzp",
      pickupDate: "2026-06-10",
      pickupTime: "10:00",
      returnDate: "2026-06-14",
      returnTime: "09:00",
      vehicle: "group-1",
      dailyPrice: "2500",
      vehicleName: "Nissan Qashqai",
    });

    render(<BookingStep3Page />);

    await user.type(screen.getByLabelText("First Name"), "Jane");
    await user.type(screen.getByLabelText("Last Name"), "Doe");
    await user.type(screen.getByLabelText("Email"), "jane@example.com");
    await user.type(screen.getByLabelText("Phone"), "+905551234567");
    await user.type(screen.getByLabelText("Date of Birth"), "1990-05-10");
    await user.type(screen.getByLabelText("License Number"), "TR-12345");
    await user.type(screen.getByLabelText("License Country"), "TR");
    await user.click(screen.getByRole("button", { name: /continue to payment/i }));

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith(
        "/en/booking/step4?pickup=ala&return=gzp&pickupDate=2026-06-10&pickupTime=10%3A00&returnDate=2026-06-14&returnTime=09%3A00&vehicle=group-1&dailyPrice=2500&vehicleName=Nissan+Qashqai&extras="
      );
    });
    expect(setDatesMock).toHaveBeenCalledWith({
      pickupOfficeId: "office-ala",
      pickupOfficeName: "Alanya City Center",
      pickupDate: "2026-06-10",
      pickupTime: "10:00",
      returnOfficeId: "office-gzp",
      returnOfficeName: "Gazipasa Airport",
      returnDate: "2026-06-14",
      returnTime: "09:00",
    });
    expect(updateCustomerDetailsMock).toHaveBeenCalled();
  });
});
