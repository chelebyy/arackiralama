import { NextRequest } from "next/server";
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/lib/auth/backend", () => ({
  tryRefreshWithBackend: vi.fn()
}));

import { POST } from "./route";
import { GET } from "../me/route";
import { tryRefreshWithBackend } from "@/lib/auth/backend";

const makeToken = (exp: number) => `header.${Buffer.from(JSON.stringify({
  sub: "test-principal",
  principal_type: "Admin",
  role: "Admin",
  email: "admin@test.invalid",
  exp
})).toString("base64url")}.signature`;

const request = () => new NextRequest("https://example.test/api/auth/refresh", {
  headers: { cookie: `rac_access=${makeToken(1)}; __Host-rac_refresh=old-refresh` }
});

describe.each([
  ["explicit refresh", POST],
  ["expired identity lookup", GET]
] as const)("%s", (_name, handler) => {
  beforeEach(() => vi.resetAllMocks());

  it("does not erase the winning rotation's cookies when the losing response arrives last", async () => {
    const token = makeToken(Math.floor(Date.now() / 1000) + 900);
    const refreshMock = vi.mocked(tryRefreshWithBackend);
    let failLate!: () => void;
    const lateFailure = new Promise<null>((resolve) => { failLate = () => resolve(null); });
    refreshMock.mockReturnValueOnce(lateFailure).mockResolvedValueOnce({
      scope: "Admin",
      backendResponse: new Response(null, {
        headers: { "set-cookie": "__Host-rac_refresh=new-refresh; Path=/; Secure; HttpOnly; SameSite=Strict" }
      }),
      envelope: { success: true, message: "refreshed", data: { accessToken: token, tokenType: "Bearer", expiresAtUtc: "2030-01-01T00:00:00Z" } }
    });

    const losingRequest = handler(request());
    const winner = await handler(request());
    expect(winner.status).toBe(200);
    expect(winner.headers.get("set-cookie")).toContain(`rac_access=${token}`);
    expect(winner.headers.get("set-cookie")).toContain("__Host-rac_refresh=new-refresh");

    failLate();
    const loser = await losingRequest;
    expect(loser.status).toBe(401);
    expect((await loser.json()).success).toBe(false);
    expect(loser.headers.get("set-cookie")).toBeNull();
  });

  it("rejects unusable backend credentials without writing cookies", async () => {
    vi.mocked(tryRefreshWithBackend).mockResolvedValueOnce({
      scope: "Admin",
      backendResponse: new Response(null),
      envelope: { success: true, message: "refreshed", data: { accessToken: "", tokenType: "Bearer", expiresAtUtc: "2030-01-01T00:00:00Z" } }
    });

    const response = await handler(request());

    expect(response.status).toBe(401);
    expect(response.headers.get("set-cookie")).toBeNull();
  });
});
