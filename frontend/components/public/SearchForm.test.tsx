import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "@/i18n/messages/en.json";

import SearchForm from "./SearchForm";

const pushMock = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: pushMock,
  }),
  useParams: () => ({
    locale: "en",
  }),
}));

function renderSearchForm() {
  return render(
    <NextIntlClientProvider locale="en" messages={messages}>
      <SearchForm />
    </NextIntlClientProvider>
  );
}

describe("SearchForm", () => {
  beforeEach(() => {
    pushMock.mockReset();
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-04-27T12:00:00.000Z"));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders deterministic default pickup and return dates", () => {
    renderSearchForm();

    expect(screen.getByLabelText("Pickup Date")).toHaveValue("2026-04-27");
    expect(screen.getByLabelText("Return Date")).toHaveValue("2026-05-04");
    expect(screen.getByLabelText("Pickup Time")).toHaveValue("10:00");
    expect(screen.getByLabelText("Return Time")).toHaveValue("10:00");
  });

  it("submits selected dates and locations to the vehicles page", async () => {
    renderSearchForm();

    fireEvent.change(screen.getByLabelText("Pickup Location"), { target: { value: "gzp" } });
    fireEvent.change(screen.getByLabelText("Pickup Date"), { target: { value: "2026-05-10" } });
    fireEvent.change(screen.getByLabelText("Pickup Time"), { target: { value: "12:30" } });
    fireEvent.change(screen.getByLabelText("Return Date"), { target: { value: "2026-05-14" } });
    fireEvent.change(screen.getByLabelText("Return Time"), { target: { value: "09:15" } });

    fireEvent.click(screen.getByRole("button", { name: "Search Vehicles" }));

    expect(pushMock).toHaveBeenCalledWith(
      "/en/vehicles?pickup=gzp&return=gzp&pickupDate=2026-05-10&pickupTime=12%3A30&returnDate=2026-05-14&returnTime=09%3A15"
    );
  });

  it("allows a different return location when same-location mode is disabled", async () => {
    renderSearchForm();

    const [sameLocationToggle] = screen.getAllByRole("button");

    expect(sameLocationToggle).not.toBeNull();
    fireEvent.click(sameLocationToggle!);

    fireEvent.change(screen.getByLabelText("Pickup Location"), { target: { value: "ala" } });
    fireEvent.change(screen.getByLabelText("Return Location"), { target: { value: "ayt" } });
    fireEvent.click(screen.getByRole("button", { name: "Search Vehicles" }));

    expect(pushMock).toHaveBeenCalledWith(
      "/en/vehicles?pickup=ala&return=ayt&pickupDate=2026-04-27&pickupTime=10%3A00&returnDate=2026-05-04&returnTime=10%3A00"
    );
  });
});
