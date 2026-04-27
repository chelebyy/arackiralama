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
    expect(screen.getByText("€135")).toBeInTheDocument();
    expect(screen.getByText("€165")).toBeInTheDocument();
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
