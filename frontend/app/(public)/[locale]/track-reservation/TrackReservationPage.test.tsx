import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import TrackReservationPage from "./page";

const mockedGetReservationByPublicCode = vi.fn();

const messages: Record<string, string> = {
  "codePlaceholder": "Enter reservation code (e.g., ALN-2025-8842)",
  "copyCode": "Copy reservation code",
  "errors.notFound": "No reservation found with this code. Please check and try again.",
  "status.active": "Active",
  "status.cancelled": "Cancelled",
  "status.pending": "Pending",
  "trackButton": "Track Reservation",
};

vi.mock("next-intl", () => ({
  useLocale: () => "en",
  useTranslations: () => (key: string) => messages[key] ?? key,
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

  it("loads the public tracking summary without rendering surplus customer data", async () => {
    const user = userEvent.setup();

    mockedGetReservationByPublicCode.mockResolvedValue({
      publicCode: "ALN-2026-1001",
      status: "ACTIVE",
      vehicleGroupName: "Economy",
      customer: {
        firstName: "Ada",
        lastName: "Lovelace",
        email: "ada@example.com",
        phone: "+44 7700 900123",
      },
      pickupOfficeName: "Gazipasa Airport",
      returnOfficeName: "Alanya Center",
      pickupDateTime: "2026-06-10T09:30:00Z",
      returnDateTime: "2026-06-14T11:00:00Z",
      paymentStatus: "CAPTURED",
      totalAmount: 3200,
      depositAmount: 5000,
      currency: "TRY",
    });

    render(<TrackReservationPage />);

    await user.type(screen.getByPlaceholderText(/enter reservation code/i), "aln-2026-1001");
    await user.click(screen.getByRole("button", { name: /track reservation/i }));

    await waitFor(() => {
      expect(mockedGetReservationByPublicCode).toHaveBeenCalledWith("ALN-2026-1001");
    });

    expect(await screen.findByText("ALN-2026-1001")).toBeInTheDocument();
    expect(screen.getByText("Economy")).toBeInTheDocument();
    expect(screen.getByText("₺3200")).toBeInTheDocument();
    expect(screen.getByText("₺5000")).toBeInTheDocument();
    expect(screen.getByTestId("reservation-timeline")).toHaveTextContent("active");
    expect(screen.queryByText("Ada Lovelace")).not.toBeInTheDocument();
    expect(screen.queryByText("ada@example.com")).not.toBeInTheDocument();
    expect(screen.queryByText("Paid")).not.toBeInTheDocument();
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

  it("maps cancelled backend states and copies the reservation code", async () => {
    const user = userEvent.setup();

    mockedGetReservationByPublicCode.mockResolvedValue({
      publicCode: "ALN-2026-2002",
      status: "EXPIRED",
      vehicleGroupName: "SUV",
      pickupOfficeName: "Alanya Center",
      returnOfficeName: "Antalya Airport",
      pickupDateTime: "2026-07-10T08:00:00Z",
      returnDateTime: "2026-07-11T09:00:00Z",
      totalAmount: 1800,
      depositAmount: 2500,
      currency: "TRY",
    });

    render(<TrackReservationPage />);

    const submitButton = screen.getByRole("button", { name: /track reservation/i });
    expect(submitButton).toBeEnabled();

    await user.click(submitButton);
    expect(screen.getByPlaceholderText(/enter reservation code/i)).toBeInvalid();
    expect(mockedGetReservationByPublicCode).not.toHaveBeenCalled();

    await user.type(screen.getByPlaceholderText(/enter reservation code/i), "aln-2026-2002");
    expect(submitButton).toBeEnabled();

    await user.click(submitButton);

    expect(await screen.findByText("ALN-2026-2002")).toBeInTheDocument();
    expect(screen.getByTestId("reservation-timeline")).toHaveTextContent("cancelled");

    await user.click(screen.getByRole("button", { name: /copy reservation code/i }));

    const copyButton = screen.getByRole("button", { name: /copy reservation code/i });
    expect(copyButton.querySelector(".text-emerald-500")).toBeInTheDocument();
  });

  it("clears the previous error and falls back unknown status values to pending", async () => {
    const user = userEvent.setup();

    mockedGetReservationByPublicCode
      .mockRejectedValueOnce(new Error("missing reservation"))
      .mockResolvedValueOnce({
        publicCode: "ALN-2026-3003",
        status: "SOMETHING_NEW",
        vehicleGroupName: "Compact",
        pickupOfficeName: "Gazipasa Airport",
        returnOfficeName: "Gazipasa Airport",
        pickupDateTime: "2026-08-01T10:00:00Z",
        returnDateTime: "2026-08-03T10:00:00Z",
        totalAmount: 2100,
        depositAmount: 3000,
        currency: "TRY",
      });

    render(<TrackReservationPage />);

    const input = screen.getByPlaceholderText(/enter reservation code/i);
    const submitButton = screen.getByRole("button", { name: /track reservation/i });

    await user.type(input, "missing");
    await user.click(submitButton);

    expect(await screen.findByText("No reservation found with this code. Please check and try again.")).toBeInTheDocument();

    await user.clear(input);
    await user.type(input, "aln-2026-3003");
    await user.click(submitButton);

    expect(await screen.findByText("ALN-2026-3003")).toBeInTheDocument();
    expect(screen.queryByText("No reservation found with this code. Please check and try again.")).not.toBeInTheDocument();
    expect(screen.getByTestId("reservation-timeline")).toHaveTextContent("pending");
    expect(screen.getByText("Pending")).toBeInTheDocument();
  });
});
