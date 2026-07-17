import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/lib/auth/backend", () => ({
  callRegisterEndpoint: vi.fn()
}));

import { POST } from "@/app/api/auth/register/route";
import { callRegisterEndpoint } from "@/lib/auth/backend";

describe("POST /api/auth/register", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("returns an empty not-found response without calling the backend", async () => {
    const response = await POST(new Request("http://localhost/api/auth/register", {
      method: "POST",
      headers: {
        "accept-language": "de-DE,de;q=0.9",
        "content-type": "application/json"
      },
      body: JSON.stringify({
        email: " new@example.test ",
        password: "secret",
        fullName: " New Customer ",
        phone: " +905551112233 "
      })
    }) as never);

    expect(response.status).toBe(404);
    await expect(response.text()).resolves.toBe("");
    expect(callRegisterEndpoint).not.toHaveBeenCalled();
  });
});
