import { describe, expect, it, vi } from "vitest";

import BookingPage from "./page";

const redirectMock = vi.fn();

vi.mock("next/navigation", () => ({
  redirect: (url: string) => redirectMock(url),
}));

describe("BookingPage", () => {
  it("redirects localized booking entry requests to step 1", async () => {
    await BookingPage({
      params: Promise.resolve({ locale: "en" }),
    });

    expect(redirectMock).toHaveBeenCalledWith("/en/booking/step1");
  });
});
