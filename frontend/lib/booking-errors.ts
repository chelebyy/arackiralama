import { ApiError, NetworkError, TimeoutError } from "@/lib/api/client";

export function bookingErrorKey(error: unknown) {
  if (error instanceof NetworkError || error instanceof TimeoutError) return "bookingNetworkError";
  if (!(error instanceof ApiError)) return "reservationFailed";
  if (/Selected vehicle.*unavailable|Selected vehicle is no longer available|overlapping reservations/.test(error.message)) return "vehicleUnavailable";
  if (/not been configured|must be configured|No vehicle rate/.test(error.message)) return "rentalPolicyUnavailable";
  if (error.message === "Selected times do not meet office operating policies.") return "rentalPolicyMismatch";
  if (/Driver does not meet|Driver license expires/.test(error.message)) return "driverNotEligible";
  if (/already being used|was consumed|retry cannot be verified/.test(error.message)) return "bookingRetryPending";
  if (/quote.*expired|Price or rental conditions changed|inputs no longer match/.test(error.message)) return "quoteConfirmationRequired";
  return "reservationFailed";
}
