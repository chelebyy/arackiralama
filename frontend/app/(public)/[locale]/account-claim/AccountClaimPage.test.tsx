import { describe, expect, it, vi } from "vitest";

const { notFoundMock } = vi.hoisted(() => ({
  notFoundMock: vi.fn(() => {
    throw new Error("NEXT_NOT_FOUND");
  }),
}));

vi.mock("next/navigation", () => ({
  notFound: notFoundMock,
}));

import AccountClaimPage from "@/app/(public)/[locale]/account-claim/page";

describe("AccountClaimPage", () => {
  it("returns not found instead of rendering the account claim surface", () => {
    expect(() => AccountClaimPage()).toThrow("NEXT_NOT_FOUND");
    expect(notFoundMock).toHaveBeenCalledOnce();
  });
});
