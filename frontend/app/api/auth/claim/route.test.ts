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

  it("returns not found for an invalid body without calling the backend", async () => {
    const response = await POST(claimRequest("not-json") as never);

    expect(response.status).toBe(404);
    await expect(response.text()).resolves.toBe("");
    expect(callAccountClaimBackendEndpoint).not.toHaveBeenCalled();
  });

  it.each(["null", "[]", '"claim-token"', "42"])(
    "returns not found for the JSON body %s without calling the backend",
    async body => {
      const response = await POST(claimRequest(body) as never);

      expect(response.status).toBe(404);
      await expect(response.text()).resolves.toBe("");
      expect(callAccountClaimBackendEndpoint).not.toHaveBeenCalled();
    }
  );

  it("returns not found for object bodies with non-string claim fields", async () => {
    const response = await POST(
      claimRequest(JSON.stringify({ token: 42, newPassword: [] })) as never
    );

    expect(response.status).toBe(404);
    await expect(response.text()).resolves.toBe("");
    expect(callAccountClaimBackendEndpoint).not.toHaveBeenCalled();
  });

  it("returns not found for a valid-looking claim without calling the backend", async () => {
    const response = await POST(
      claimRequest(
        JSON.stringify({
          token: "  claim-token  ",
          newPassword: "new-secret"
        })
      ) as never
    );

    expect(response.status).toBe(404);
    await expect(response.text()).resolves.toBe("");
    expect(callAccountClaimBackendEndpoint).not.toHaveBeenCalled();
  });
});
