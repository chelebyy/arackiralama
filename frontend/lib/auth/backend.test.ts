import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  buildBackendUrl,
  callAccountClaimEndpoint,
  callLoginEndpoint,
  callLogoutEndpoint,
  callPasswordResetConfirm,
  callPasswordResetRequest,
  callRegisterEndpoint,
  tryRefreshWithBackend,
  validateAccessTokenWithBackend,
} from "./backend";
import { isExpired, normalizePrincipalScope, parseAccessTokenClaims } from "./jwt";

function jsonResponse(body: unknown, init?: ResponseInit) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "content-type": "application/json", ...(init?.headers ?? {}) },
    ...init,
  });
}

function tokenWithPayload(payload: unknown) {
  const encoded = Buffer.from(JSON.stringify(payload)).toString("base64url");
  return `header.${encoded}.signature`;
}

describe("auth backend helpers", () => {
  beforeEach(() => {
    vi.stubEnv("AUTH_BACKEND_URL", "https://api.example.test/");
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it("builds backend URLs from configured base values", () => {
    expect(buildBackendUrl("api/admin/v1/auth/me")).toBe(
      "https://api.example.test/api/admin/v1/auth/me"
    );
    expect(buildBackendUrl("/api/customer/v1/auth/me")).toBe(
      "https://api.example.test/api/customer/v1/auth/me"
    );
  });

  it("posts login, registration, and password reset payloads", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse({ success: true, data: { accessToken: "token" } }));
    vi.stubGlobal("fetch", fetchMock);

    await callLoginEndpoint({
      principalScope: "Admin",
      email: "admin@example.test",
      password: "secret",
    });
    await callLoginEndpoint({
      principalScope: "Customer",
      email: "customer@example.test",
      password: "secret",
    });
    await callRegisterEndpoint({
      email: "new@example.test",
      password: "secret",
      fullName: "New Customer",
      phone: "+905551112233",
    }, "de-DE,de;q=0.9");
    await callPasswordResetRequest({ email: "new@example.test", principalScope: "Customer" });
    await callPasswordResetConfirm({
      token: "reset-token",
      newPassword: "new-secret",
      principalScope: "Admin",
    });

    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      "https://api.example.test/api/admin/v1/auth/login",
      expect.objectContaining({ method: "POST" })
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      "https://api.example.test/api/customer/v1/auth/login",
      expect.objectContaining({ method: "POST" })
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      3,
      "https://api.example.test/api/customer/v1/auth/register",
      expect.objectContaining({
        body: JSON.stringify({
          email: "new@example.test",
          password: "secret",
          fullName: "New Customer",
          phone: "+905551112233",
        }),
        headers: expect.objectContaining({
          "accept-language": "de-DE,de;q=0.9",
        }),
      })
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      4,
      "https://api.example.test/api/v1/auth/password-reset/request",
      expect.objectContaining({ body: JSON.stringify({ email: "new@example.test", principalScope: "Customer" }) })
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      5,
      "https://api.example.test/api/v1/auth/password-reset/confirm",
      expect.objectContaining({
        body: JSON.stringify({
          token: "reset-token",
          newPassword: "new-secret",
          principalScope: "Admin",
        }),
      })
    );
  });

  it("posts account claims through the same-origin auth route", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ success: true }));
    vi.stubGlobal("fetch", fetchMock);

    await callAccountClaimEndpoint({
      token: "claim-token",
      newPassword: "new-secret",
    });

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/auth/claim",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({
          token: "claim-token",
          newPassword: "new-secret",
        }),
      })
    );
  });

  it("returns the first successful refresh or validation scope", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse({ success: false }, { status: 401 }))
      .mockResolvedValueOnce(jsonResponse({ success: true, data: { accessToken: "fresh" } }))
      .mockResolvedValueOnce(jsonResponse({ success: false }, { status: 401 }))
      .mockResolvedValueOnce(jsonResponse({ success: true, data: { email: "user@example.test" } }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      tryRefreshWithBackend({ preferredScope: "Admin", cookieHeader: "rac_refresh=abc" })
    ).resolves.toMatchObject({ scope: "Customer" });
    await expect(
      validateAccessTokenWithBackend({ preferredScope: "Customer", accessToken: "access" })
    ).resolves.toMatchObject({ scope: "Admin" });

    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      "https://api.example.test/api/admin/v1/auth/refresh",
      expect.objectContaining({ headers: { cookie: "rac_refresh=abc" } })
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      4,
      "https://api.example.test/api/admin/v1/auth/me",
      expect.objectContaining({ headers: { authorization: "Bearer access" } })
    );
  });

  it("returns null for missing refresh cookies or failed backend checks", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(jsonResponse({ success: false }, { status: 401 }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(tryRefreshWithBackend({ cookieHeader: null })).resolves.toBeNull();
    await expect(
      tryRefreshWithBackend({ cookieHeader: "rac_refresh=abc", preferredScope: null })
    ).resolves.toBeNull();
    await expect(validateAccessTokenWithBackend({ accessToken: "bad" })).resolves.toBeNull();
  });

  it("forwards logout credentials and tolerates non-json envelopes", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response("not json", { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      callLogoutEndpoint({
        principalScope: "Admin",
        accessToken: "access-token",
        cookieHeader: "rac_refresh=abc",
      })
    ).resolves.toMatchObject({ envelope: null });

    const headers = fetchMock.mock.calls[0][1].headers as Headers;
    expect(headers.get("authorization")).toBe("Bearer access-token");
    expect(headers.get("cookie")).toBe("rac_refresh=abc");
  });
});

describe("auth JWT helpers", () => {
  it("parses token claims and normalizes scopes", () => {
    expect(parseAccessTokenClaims(null)).toBeNull();
    expect(parseAccessTokenClaims("invalid")).toBeNull();
    expect(parseAccessTokenClaims(tokenWithPayload({ exp: 200, scope: "Admin" }))).toMatchObject({
      exp: 200,
      scope: "Admin",
    });
    expect(parseAccessTokenClaims("a.invalid-payload.c")).toBeNull();

    expect(normalizePrincipalScope("admin")).toBe("Admin");
    expect(normalizePrincipalScope("CUSTOMER")).toBe("Customer");
    expect(normalizePrincipalScope("unknown")).toBeNull();
    expect(normalizePrincipalScope(undefined)).toBeNull();
  });

  it("treats missing and past expirations as expired", () => {
    expect(isExpired(null, 100)).toBe(true);
    expect(isExpired({}, 100)).toBe(true);
    expect(isExpired({ exp: 100 }, 100)).toBe(true);
    expect(isExpired({ exp: 101 }, 100)).toBe(false);
  });
});
