import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import BookingStep1Page from "./page";

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

describe("BookingStep1Page", () => {
  beforeEach(() => {
    pushMock.mockReset();
    searchParams = new URLSearchParams();
  });

  it("validates required date fields before navigation", async () => {
    const user = userEvent.setup();

    render(<BookingStep1Page />);

    await user.click(screen.getByRole("button", { name: /continue to vehicle selection/i }));

    expect(await screen.findByText("Pickup date is required")).toBeInTheDocument();
    expect(screen.getByText("Return date is required")).toBeInTheDocument();
    expect(pushMock).not.toHaveBeenCalled();
  });

  it("navigates to step 2 with the pickup office reused when same-office mode is enabled", async () => {
    const user = userEvent.setup();

    searchParams = new URLSearchParams({ pickupDate: "2026-05-10", returnDate: "2026-05-12" });
    render(<BookingStep1Page />);

    await user.selectOptions(screen.getByLabelText("Office Location"), "gzp");
    await user.selectOptions(screen.getByLabelText("Pickup Time"), "12:30");
    await user.selectOptions(screen.getByLabelText("Return Time"), "18:00");
    await user.click(screen.getByRole("button", { name: /continue to vehicle selection/i }));

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith(
        "/en/booking/step2?pickup=gzp&return=gzp&pickupDate=2026-05-10&pickupTime=12%3A30&returnDate=2026-05-12&returnTime=18%3A00"
      );
    });
  });

  it("navigates with a distinct return office when same-office mode is disabled", async () => {
    const user = userEvent.setup();

    searchParams = new URLSearchParams({ pickupDate: "2026-05-10", returnDate: "2026-05-12" });
    render(<BookingStep1Page />);

    await user.click(screen.getByRole("checkbox", { name: /same as pickup/i }));
    await user.selectOptions(screen.getByLabelText("Office Location"), "ala");
    await user.selectOptions(screen.getByLabelText("Return Office"), "ayt");
    await user.click(screen.getByRole("button", { name: /continue to vehicle selection/i }));

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith(
        "/en/booking/step2?pickup=ala&return=ayt&pickupDate=2026-05-10&pickupTime=10%3A00&returnDate=2026-05-12&returnTime=10%3A00"
      );
    });
  });
});
