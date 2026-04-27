import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import BookingStep4Page from "./page";

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

describe("BookingStep4Page", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(console, "log").mockImplementation(() => undefined);
    pushMock.mockReset();
    searchParams = new URLSearchParams({
      vehicle: "economy",
      pickupDate: "2026-05-10",
      returnDate: "2026-05-13",
      extras: "gps,additional_driver",
    });
  });

  it("blocks card checkout until terms are accepted and valid card details are provided", async () => {
    const user = userEvent.setup();

    render(<BookingStep4Page />);

    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    expect(await screen.findByText("You must accept the terms and conditions")).toBeInTheDocument();
    expect(pushMock).not.toHaveBeenCalled();

    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    expect(pushMock).not.toHaveBeenCalled();
  });

  it("applies supported campaign codes and updates the price breakdown", async () => {
    const user = userEvent.setup();

    render(<BookingStep4Page />);

    await user.type(screen.getByPlaceholderText(/enter code/i), "summer15");
    await user.click(screen.getByRole("button", { name: "Apply" }));

    expect(await screen.findByText("Code SUMMER15 applied!")).toBeInTheDocument();
    expect(screen.getByText("Campaign Discount (15%)")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Applied" })).toBeDisabled();
  });

  it("allows PayPal checkout without card details and navigates to confirmation", async () => {
    const user = userEvent.setup();

    vi.spyOn(Date, "now").mockReturnValue(1714219200000);

    render(<BookingStep4Page />);

    await user.click(screen.getByRole("radio", { name: /paypal/i }));
    expect(screen.queryByLabelText("Card Number")).not.toBeInTheDocument();

    await user.click(screen.getByRole("checkbox"));
    await user.click(screen.getByRole("button", { name: /complete booking/i }));

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith(
        "/en/booking/confirmation?vehicle=economy&pickupDate=2026-05-10&returnDate=2026-05-13&extras=gps%2Cadditional_driver&code=ALN-1714219200000"
      );
    });
  });
});
