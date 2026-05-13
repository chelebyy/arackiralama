import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import TrackReservationPage from "./page";

const mockedGetReservationByPublicCode = vi.fn();

vi.mock("next-intl", () => ({
  useTranslations: () => (key: string) => key,
}));

vi.mock("@/lib/api/reservations", () => ({
  getReservationByPublicCode: (...args: unknown[]) => mockedGetReservationByPublicCode(...args),
}));

vi.mock("@/components/public/ReservationTimeline", () => ({
  default: ({ status }: { status: string }) => <div data-testid="reservation-timeline">{status}</div>,
}));

describe("TrackReservationPage", () => {
  beforeEach(() => {
    mockedGetReservationByPublicCode.mockReset();
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText: vi.fn().mockResolvedValue(undefined) },
      configurable: true,
    });
  });

  it("loads and maps reservation details returned by the public tracking API", async () => {
    const user = userEvent.setup();

    mockedGetReservationByPublicCode.mockResolvedValue({
      id: "reservation-1",
      publicCode: "ALN-2026-1001",
      status: "ACTIVE",
      vehicleName: "Renault Clio",
      customer: {
        firstName: "Ada",
        lastName: "Lovelace",
        email: "ada@example.com",
        phone: "+44 7700 900123",
      },
      pickupOfficeName: "Gazipasa Airport",
      returnOfficeName: "Alanya Center",
      pickupDate: "2026-06-10",
      returnDate: "2026-06-14",
      pickupTime: "09:30",
      returnTime: "11:00",
      paymentStatus: "CAPTURED",
      createdAt: "2026-05-01T08:00:00Z",
      updatedAt: "2026-05-02T12:00:00Z",
      priceBreakdown: {
        totalAmount: 3200,
        depositAmount: 5000,
      },
    });

    render(<TrackReservationPage />);

    await user.type(screen.getByPlaceholderText(/enter reservation code/i), "aln-2026-1001");
    await user.click(screen.getByRole("button", { name: /track reservation/i }));

    await waitFor(() => {
      expect(mockedGetReservationByPublicCode).toHaveBeenCalledWith("ALN-2026-1001");
    });

    expect(await screen.findByText("ALN-2026-1001")).toBeInTheDocument();
    expect(screen.getByText("Ada Lovelace")).toBeInTheDocument();
    expect(screen.getByText("Renault Clio")).toBeInTheDocument();
    expect(screen.getByText("₺3200")).toBeInTheDocument();
    expect(screen.getByText("₺5000")).toBeInTheDocument();
    expect(screen.getByTestId("reservation-timeline")).toHaveTextContent("active");
    expect(screen.getByText("Paid")).toBeInTheDocument();
  });

  it("shows a friendly error when the reservation code lookup fails", async () => {
    const user = userEvent.setup();

    mockedGetReservationByPublicCode.mockRejectedValue(new Error("missing reservation"));

    render(<TrackReservationPage />);

    await user.type(screen.getByPlaceholderText(/enter reservation code/i), "missing");
    await user.click(screen.getByRole("button", { name: /track reservation/i }));

    expect(await screen.findByText("No reservation found with this code. Please check and try again.")).toBeInTheDocument();
    expect(screen.queryByTestId("reservation-timeline")).not.toBeInTheDocument();
  });
});
