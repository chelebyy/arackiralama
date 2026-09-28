import { afterEach, describe, expect, it, vi } from "vitest";
import { createHmac } from "node:crypto";
import { guestProxyHeaders } from "./guest-proxy";

afterEach(() => vi.unstubAllEnvs());
describe("guest proxy identity", () => {
  const secret = "test-only-guest-proxy-secret-at-least-32-characters";
  it("signs only the configured edge address and binds the method and path", () => {
    vi.stubEnv("GUEST_PROXY_SECRET", secret);
    vi.stubEnv("GUEST_CLIENT_IP_HEADER", "x-edge-client");
    const headers = new Headers({ "x-edge-client": "192.0.2.1", "x-forwarded-for": "forged", "x-guest-client": "forged" });
    const first = guestProxyHeaders(headers, "POST", "/api/guest/v1/reservation/request");
    expect(first["X-Guest-Client"]).toBe(createHmac("sha256", secret).update("192.0.2.1").digest("hex"));
    expect(first["X-Guest-Signature"]).toBe(createHmac("sha256", secret).update(
      `${first["X-Guest-Timestamp"]}\n${first["X-Guest-Client"]}\nPOST\n/api/guest/v1/reservation/request`).digest("hex"));
    headers.set("x-edge-client", "192.0.2.2");
    expect(guestProxyHeaders(headers, "POST", "/api/guest/v1/reservation/request")["X-Guest-Client"]).not.toBe(first["X-Guest-Client"]);
  });
  it.each(["", "forged", "192.0.2.1, 192.0.2.2"])("rejects missing or ambiguous edge addresses: %s", ip => {
    vi.stubEnv("GUEST_PROXY_SECRET", secret);
    vi.stubEnv("GUEST_CLIENT_IP_HEADER", "x-edge-client");
    expect(() => guestProxyHeaders(new Headers({ "x-edge-client": ip }), "GET", "/api/guest/v1/reservation")).toThrow();
  });
  it("fails closed when production has no trusted edge configuration", () => {
    vi.stubEnv("NODE_ENV", "production");
    vi.stubEnv("GUEST_PROXY_SECRET", "");
    vi.stubEnv("GUEST_CLIENT_IP_HEADER", "");
    expect(() => guestProxyHeaders(new Headers(), "GET", "/api/guest/v1/reservation")).toThrow();
  });
});
