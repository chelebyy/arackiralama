import { describe, expect, it } from "vitest";
import { ApiError, NetworkError, TimeoutError } from "./api/client";
import { bookingErrorKey } from "./booking-errors";
import en from "@/i18n/messages/en.json";
import tr from "@/i18n/messages/tr.json";
import de from "@/i18n/messages/de.json";
import ru from "@/i18n/messages/ru.json";
import ar from "@/i18n/messages/ar.json";

describe("booking recovery messages", () => {
  it.each([
    ["Selected vehicle is no longer available.", "vehicleUnavailable"],
    ["Office operating policies have not been configured.", "rentalPolicyUnavailable"],
    ["Selected times do not meet office operating policies.", "rentalPolicyMismatch"],
    ["Driver does not meet the minimum age.", "driverNotEligible"],
    ["Reservation quote is already being used or was consumed.", "bookingRetryPending"],
    ["Price or rental conditions changed. Request a new quote.", "quoteConfirmationRequired"],
    ["Reservation quote has expired. Request a new quote.", "quoteConfirmationRequired"],
    ["Internal host or private diagnostic", "reservationFailed"],
  ])("maps %s to a translated recovery message", (message, key) => {
    const error = new ApiError({ statusCode: 409, message, code: "CONFLICT", timestamp: "2026-09-26", path: "/reservations" });
    expect(bookingErrorKey(error)).toBe(key);
    for (const messages of [en, tr, de, ru, ar]) {
      expect(messages.booking[key as keyof typeof messages.booking]).toBeTruthy();
    }
  });

  it("distinguishes response loss without exposing unknown error details", () => {
    expect(bookingErrorKey(new NetworkError())).toBe("bookingNetworkError");
    expect(bookingErrorKey(new TimeoutError())).toBe("bookingNetworkError");
    expect(bookingErrorKey(new Error("private diagnostic"))).toBe("reservationFailed");
  });
});
