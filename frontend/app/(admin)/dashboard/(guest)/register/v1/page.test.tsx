import { describe, expect, it, vi } from "vitest";

const { notFoundMock } = vi.hoisted(() => ({
  notFoundMock: vi.fn(() => {
    throw new Error("NEXT_NOT_FOUND");
  }),
}));

vi.mock("next/navigation", () => ({
  notFound: notFoundMock,
}));

import RegisterPage from "@/app/(admin)/dashboard/(guest)/register/v1/page";

describe("RegisterPage", () => {
  it("returns not found instead of rendering public registration", () => {
    expect(() => RegisterPage()).toThrow("NEXT_NOT_FOUND");
    expect(notFoundMock).toHaveBeenCalledOnce();
  });
});
