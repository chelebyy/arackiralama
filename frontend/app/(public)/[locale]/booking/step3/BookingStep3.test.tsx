import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import BookingStep3Page from "./page";

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

describe("BookingStep3Page", () => {
  beforeEach(() => {
    pushMock.mockReset();
    searchParams = new URLSearchParams({ vehicle: "compact" });
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
    await user.type(screen.getByLabelText("Email Address"), "jane@example.com");
    await user.type(screen.getByLabelText("Phone Number"), "+905551234567");
    await user.type(screen.getByLabelText("Date of Birth"), "1990-05-10");
    await user.type(screen.getByLabelText("Driver License Number"), "TR-12345");
    const childSeatOption = screen.getByRole("button", { name: /child safety seat/i });
    const gpsOption = screen.getByRole("button", { name: /gps navigation/i });

    await user.click(childSeatOption);
    await user.click(gpsOption);

    expect(childSeatOption).toHaveClass("border-sky-600");
    expect(gpsOption).toHaveClass("border-sky-600");
    expect(pushMock).not.toHaveBeenCalled();
  });
});
