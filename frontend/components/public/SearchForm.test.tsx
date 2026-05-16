import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import messages from "@/i18n/messages/en.json";

import SearchForm from "./SearchForm";

const pushMock = vi.fn();
const originalShowPicker = HTMLInputElement.prototype.showPicker;
const originalShowPickerDescriptor = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "showPicker");

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
    vi.restoreAllMocks();

    if (originalShowPickerDescriptor) {
      Object.defineProperty(HTMLInputElement.prototype, "showPicker", originalShowPickerDescriptor);
    } else if (originalShowPicker) {
      HTMLInputElement.prototype.showPicker = originalShowPicker;
    } else {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      Reflect.deleteProperty(HTMLInputElement.prototype as any, "showPicker");
    }
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

    const sameLocationToggle = screen.getByRole("button", { name: "Same pickup and return location" });

    fireEvent.click(sameLocationToggle);

    fireEvent.change(screen.getByLabelText("Pickup Location"), { target: { value: "ala" } });
    fireEvent.change(screen.getByLabelText("Return Location"), { target: { value: "ayt" } });
    fireEvent.click(screen.getByRole("button", { name: "Search Vehicles" }));

    expect(pushMock).toHaveBeenCalledWith(
      "/en/vehicles?pickup=ala&return=ayt&pickupDate=2026-04-27&pickupTime=10%3A00&returnDate=2026-05-04&returnTime=10%3A00"
    );
  });

  it("reuses the pickup location again when same-location mode is re-enabled", async () => {
    renderSearchForm();

    const sameLocationToggle = screen.getByRole("button", { name: "Same pickup and return location" });

    fireEvent.click(sameLocationToggle);
    fireEvent.change(screen.getByLabelText("Pickup Location"), { target: { value: "ala" } });
    fireEvent.change(screen.getByLabelText("Return Location"), { target: { value: "ayt" } });

    fireEvent.click(sameLocationToggle);
    fireEvent.click(screen.getByRole("button", { name: "Search Vehicles" }));

    expect(screen.queryByLabelText("Return Location")).not.toBeInTheDocument();
    expect(pushMock).toHaveBeenCalledWith(
      "/en/vehicles?pickup=ala&return=ala&pickupDate=2026-04-27&pickupTime=10%3A00&returnDate=2026-05-04&returnTime=10%3A00"
    );
  });

  it("opens the native pickers for date and time fields when showPicker is available", () => {
    const showPickerMock = vi.fn();
    HTMLInputElement.prototype.showPicker = showPickerMock;

    renderSearchForm();

    fireEvent.click(screen.getByLabelText("Pickup Date"));
    fireEvent.click(screen.getByLabelText("Pickup Time"));
    fireEvent.click(screen.getByLabelText("Return Date"));
    fireEvent.click(screen.getByLabelText("Return Time"));

    expect(showPickerMock).toHaveBeenCalledTimes(4);
  });

  it("logs picker errors without breaking the form when showPicker throws", () => {
    const consoleErrorMock = vi.spyOn(console, "error").mockImplementation(() => {});
    HTMLInputElement.prototype.showPicker = vi.fn(() => {
      throw new Error("picker unavailable");
    });

    renderSearchForm();

    fireEvent.click(screen.getByLabelText("Pickup Date"));
    fireEvent.click(screen.getByLabelText("Pickup Time"));
    fireEvent.click(screen.getByLabelText("Return Date"));
    fireEvent.click(screen.getByLabelText("Return Time"));

    expect(consoleErrorMock).toHaveBeenCalledTimes(4);
    expect(consoleErrorMock).toHaveBeenCalledWith("Picker error:", expect.any(Error));
  });
});
