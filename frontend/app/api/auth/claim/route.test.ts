import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/lib/auth/backend", () => ({
  callAccountClaimBackendEndpoint: vi.fn()
}));

import { POST } from "@/app/api/auth/claim/route";
import { callAccountClaimBackendEndpoint } from "@/lib/auth/backend";

function claimRequest(body: BodyInit) {
  return new Request("http://localhost/api/auth/claim", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body
  });
}

describe("POST /api/auth/claim", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("rejects an invalid request body without calling the backend", async () => {
    const response = await POST(claimRequest("not-json") as never);

    expect(response.status).toBe(400);
    await expect(response.json()).resolves.toMatchObject({ success: false });
    expect(callAccountClaimBackendEndpoint).not.toHaveBeenCalled();
  });

  it("forwards a valid claim and preserves the backend status", async () => {
    vi.mocked(callAccountClaimBackendEndpoint).mockResolvedValueOnce({
      backendResponse: new Response(null, { status: 400 }),
      envelope: {
        success: false,
        data: null,
        message: "Hesap talep bağlantısı geçersiz veya süresi dolmuş."
      }
    });

    const response = await POST(
      claimRequest(
        JSON.stringify({
          token: "  claim-token  ",
          newPassword: "new-secret"
        })
      ) as never
    );

    expect(callAccountClaimBackendEndpoint).toHaveBeenCalledWith({
      token: "claim-token",
      newPassword: "new-secret"
    });
    expect(response.status).toBe(400);
    await expect(response.json()).resolves.toEqual({
      success: false,
      message: "Hesap talep bağlantısı geçersiz veya süresi dolmuş."
    });
  });
});
