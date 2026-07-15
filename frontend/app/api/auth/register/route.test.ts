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

  it("forwards the browser language preference to the customer auth backend", async () => {
    vi.mocked(callRegisterEndpoint).mockResolvedValueOnce({
      backendResponse: new Response(null, { status: 200 }),
      envelope: {
        success: true,
        data: {},
        message: "Kayıt başarılı."
      }
    });

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

    expect(callRegisterEndpoint).toHaveBeenCalledWith({
      email: "new@example.test",
      password: "secret",
      fullName: "New Customer",
      phone: "+905551112233"
    }, "de-DE,de;q=0.9");
    expect(response.status).toBe(200);
  });
});
