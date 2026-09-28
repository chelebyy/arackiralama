import { afterEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";
import { GET, POST } from "./route";

const upstream = vi.fn();
afterEach(() => { vi.unstubAllGlobals(); vi.clearAllMocks(); });
function request(path: string, method = "POST", extra: Record<string, string> = {}, body = "{}") {
  return new NextRequest("https://rental.test/api/guest/" + path, {
    method, headers: { origin: "https://rental.test", "content-type": "application/json", ...extra },
    ...(method === "GET" ? {} : { body }),
  });
}
const context = (path: string) => ({ params: Promise.resolve({ path: path.split("/") }) });
describe("guest BFF", () => {
  it("rejects unknown routes and GET verification before calling upstream", async () => {
    vi.stubGlobal("fetch", upstream);
    expect((await GET(request("verify", "GET"), context("verify"))).status).toBe(404);
    expect((await POST(request("../admin"), context("../admin"))).status).toBe(404);
    expect(upstream).not.toHaveBeenCalled();
  });
  it.each([
    { origin: "https://attacker.test", "content-type": "application/json" },
    { origin: "https://rental.test", "content-type": "text/plain" },
  ])("rejects cross-origin or simple form requests", async headers => {
    vi.stubGlobal("fetch", upstream);
    expect((await POST(request("request", "POST", headers), context("request"))).status).toBe(403);
    expect(upstream).not.toHaveBeenCalled();
  });
  it("requires matching CSRF and ignores caller-supplied authentication headers", async () => {
    vi.stubGlobal("fetch", upstream.mockResolvedValue(Response.json({ status: "Cancelled" })));
    expect((await POST(request("cancel"), context("cancel"))).status).toBe(403);
    const response = await POST(request("cancel", "POST", {
      cookie: "guest_reservation_session=stored; guest_reservation_csrf=csrf",
      "x-guest-csrf": "csrf", "x-guest-session": "forged", authorization: "Bearer forged",
      "x-forwarded-for": "198.51.100.1",
    }), context("cancel"));
    expect(response.status).toBe(200);
    expect(upstream.mock.calls[0][1].headers).toEqual({
      "Content-Type": "application/json", "X-Guest-Session": "stored", "X-Guest-CSRF": "csrf",
    });
    expect(response.headers.get("cache-control")).toBe("no-store");
  });
  it("keeps the verified session out of JSON and sets restricted HttpOnly cookies", async () => {
    vi.stubGlobal("fetch", upstream.mockResolvedValue(Response.json({ sessionToken: "secret", csrfToken: "csrf" })));
    const response = await POST(request("verify"), context("verify"));
    expect(await response.json()).toEqual({ success: true, csrfToken: "csrf" });
    const cookie = response.cookies.get("guest_reservation_session");
    expect(cookie).toMatchObject({ value: "secret", httpOnly: true, sameSite: "strict", path: "/api/guest", maxAge: 1200 });
    expect(response.headers.get("referrer-policy")).toBe("no-referrer");
  });
  it("bounds payloads and hides upstream error details", async () => {
    vi.stubGlobal("fetch", upstream.mockResolvedValue(Response.json({ stack: "private", email: "private" }, { status: 409 })));
    expect((await POST(request("request", "POST", {}, "a".repeat(8193)), context("request"))).status).toBe(413);
    expect(upstream).not.toHaveBeenCalled();
    const response = await POST(request("request"), context("request"));
    expect(await response.json()).toEqual({ code: "unavailable" });
  });
  it("restores CSRF on reload and clears cookies after logout", async () => {
    vi.stubGlobal("fetch", upstream.mockResolvedValue(Response.json({ publicCode: "REF" })));
    const headers = { cookie: "guest_reservation_session=stored; guest_reservation_csrf=csrf", "x-guest-csrf": "csrf" };
    const view = await GET(request("view", "GET", headers), context("view"));
    expect(await view.json()).toMatchObject({ publicCode: "REF", csrfToken: "csrf" });
    const logout = await POST(request("logout", "POST", headers), context("logout"));
    expect(logout.cookies.get("guest_reservation_session")?.maxAge).toBe(0);
    expect(logout.cookies.get("guest_reservation_csrf")?.maxAge).toBe(0);
  });
});
