import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";

import ThreeDsReturnPage from "./page";

const replaceMock = vi.fn();
const pushMock = vi.fn();
const complete3dsReturnMock = vi.fn();
let searchParams = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock, push: pushMock }),
  useSearchParams: () => searchParams,
  useParams: () => ({ locale: "en" }),
}));

vi.mock("@/lib/api/payments", () => ({
  complete3dsReturn: (...args: unknown[]) => complete3dsReturnMock(...args),
}));

describe("ThreeDsReturnPage", () => {
  beforeEach(() => {
    replaceMock.mockReset();
    pushMock.mockReset();
    complete3dsReturnMock.mockReset();
    complete3dsReturnMock.mockResolvedValue(undefined);
    searchParams = new URLSearchParams();
    sessionStorage.clear();
  });

  it("completes the return flow, clears session storage, and redirects to confirmation", async () => {
    sessionStorage.setItem("pendingPaymentIntentId", "pi_123");
    sessionStorage.setItem("pendingReservationPublicCode", "ALN-REAL-123");
    sessionStorage.setItem("pendingBankResponse", "approved");

    render(<ThreeDsReturnPage />);

    await waitFor(() => {
      expect(complete3dsReturnMock).toHaveBeenCalledWith("pi_123", { bankResponse: "approved" });
    });

    await waitFor(() => {
      expect(replaceMock).toHaveBeenCalledWith("/en/booking/confirmation?code=ALN-REAL-123");
    });

    expect(sessionStorage.getItem("pendingPaymentIntentId")).toBeNull();
    expect(sessionStorage.getItem("pendingReservationPublicCode")).toBeNull();
    expect(sessionStorage.getItem("pendingBankResponse")).toBeNull();
  });

  it("shows an error state and returns to payment when required session data is missing", async () => {
    render(<ThreeDsReturnPage />);

    expect(await screen.findByText("Failed to process payment. Please try again.")).toBeInTheDocument();
    expect(complete3dsReturnMock).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Return to Payment" }));
    expect(pushMock).toHaveBeenCalledWith("/en/booking/step4");
  });
});
