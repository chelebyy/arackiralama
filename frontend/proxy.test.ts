import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { NextRequest } from "next/server";

import { proxy } from "@/proxy";
import { ACCESS_COOKIE_NAME } from "@/lib/auth/constants";

function createToken(payload: Record<string, unknown>) {
  const header = Buffer.from(JSON.stringify({ alg: "HS256", typ: "JWT" })).toString("base64url");
  const body = Buffer.from(JSON.stringify(payload)).toString("base64url");
  return `${header}.${body}.signature`;
}

function createRequest(url: string, cookie?: string) {
  const headers = new Headers();
  if (cookie) {
    headers.set("cookie", cookie);
  }

  return new NextRequest(url, { headers });
}

describe("proxy auth guard", () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    global.fetch = originalFetch;
  });

  it("redirects unauthenticated users to admin login for protected routes", async () => {
    const request = createRequest("http://localhost:3000/dashboard/default");

    const response = await proxy(request);

    expect(response.status).toBe(307);
    expect(response.headers.get("location")).toContain("/dashboard/login/v2");
    expect(response.headers.get("location")).toContain("next=%2Fdashboard%2Fdefault");
  });

  it("allows customer scope on customer portal route", async () => {
    const token = createToken({
      sub: "customer-1",
      role: "Customer",
      principal_type: "Customer",
      exp: Math.floor(Date.now() / 1000) + 3600
    });

    const request = createRequest(
      "http://localhost:3000/dashboard/customer-portal",
      `${ACCESS_COOKIE_NAME}=${token}`
    );

    const response = await proxy(request);

    expect(response.status).toBe(200);
    expect(response.headers.get("location")).toBeNull();
  });

  it("redirects customer scope away from admin dashboard routes", async () => {
    const token = createToken({
      sub: "customer-1",
      role: "Customer",
      principal_type: "Customer",
      exp: Math.floor(Date.now() / 1000) + 3600
    });

    const request = createRequest(
      "http://localhost:3000/dashboard/default",
      `${ACCESS_COOKIE_NAME}=${token}`
    );

    const response = await proxy(request);

    expect(response.status).toBe(307);
    expect(response.headers.get("location")).toContain("/dashboard/forbidden");
  });

  it("redirects Admin role from superadmin-only routes", async () => {
    const token = createToken({
      sub: "admin-1",
      role: "Admin",
      principal_type: "Admin",
      exp: Math.floor(Date.now() / 1000) + 3600
    });

    const request = createRequest(
      "http://localhost:3000/dashboard/pages/users",
      `${ACCESS_COOKIE_NAME}=${token}`
    );

    const response = await proxy(request);

    expect(response.status).toBe(307);
    expect(response.headers.get("location")).toContain("/dashboard/forbidden");
  });

  it("refreshes expired access token using refresh cookie and continues", async () => {
    const expiredToken = createToken({
      sub: "admin-1",
      role: "Admin",
      principal_type: "Admin",
      exp: Math.floor(Date.now() / 1000) - 10
    });

    const refreshedToken = createToken({
      sub: "admin-1",
      role: "Admin",
      principal_type: "Admin",
      exp: Math.floor(Date.now() / 1000) + 3600
    });

    global.fetch = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          success: true,
          message: "Oturum yenilendi.",
          data: {
            accessToken: refreshedToken,
            tokenType: "Bearer",
            expiresAtUtc: new Date().toISOString()
          }
        }),
        {
          status: 200,
          headers: {
            "content-type": "application/json",
            "set-cookie": "rac_refresh=new-refresh-token; Path=/; HttpOnly; SameSite=Strict"
          }
        }
      )
    ) as typeof fetch;

    const request = createRequest(
      "http://localhost:3000/dashboard/default",
      `${ACCESS_COOKIE_NAME}=${expiredToken}; rac_refresh=refresh-token`
    );

    const response = await proxy(request);

    expect(response.status).toBe(200);
    expect(global.fetch).toHaveBeenCalled();
    expect(response.headers.get("set-cookie")).toContain(`${ACCESS_COOKIE_NAME}=`);
  });
});
