import { NextRequest } from "next/server";
import { afterEach, describe, expect, it, vi } from "vitest";

import { GET, OPTIONS, POST } from "./route";

const makeContext = (path: string[]) => ({
  params: Promise.resolve({ path }),
});

describe("/api/v1/[...path] route", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it.each([["pricing", "quote"], ["reservations"]])("preserves booking ownership and retry headers for %s", async (...path) => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response("{}"));
    const request = new NextRequest(`http://localhost:3001/api/v1/${path.join("/")}`, {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "X-Session-Id": "browser-session",
        "Idempotency-Key": "booking-attempt",
        "authorization": "Bearer must-not-forward",
        "cookie": "admin=must-not-forward",
        "x-forwarded-for": "untrusted",
      },
      body: "{}",
    });
    await POST(request, makeContext(path));
    const headers = fetchMock.mock.calls[0][1]?.headers as Headers;
    expect(headers.get("x-session-id")).toBe("browser-session");
    expect(headers.get("idempotency-key")).toBe("booking-attempt");
    expect(headers.get("authorization")).toBeNull();
    expect(headers.get("cookie")).toBeNull();
    expect(headers.get("x-forwarded-for")).toBeNull();
  });

  it("allows the booking headers in preflight", async () => {
    const response = await OPTIONS();
    expect(response.headers.get("Access-Control-Allow-Headers")).toBe("Content-Type, X-Session-Id, Idempotency-Key");
  });

  it("forwards public GET requests to the backend API", async () => {
    const fetchMock = vi
      .spyOn(globalThis, "fetch")
      .mockResolvedValue(new Response(JSON.stringify({ success: true, data: [] }), {
        headers: { "content-type": "application/json" },
        status: 200,
      }));

    const request = new NextRequest("http://localhost:3001/api/v1/vehicles?group=economy");

    const response = await GET(request, makeContext(["vehicles"]));

    expect(response.status).toBe(200);
    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5135/api/v1/vehicles?group=economy",
      expect.objectContaining({
        method: "GET",
      })
    );
  });

  it("forwards request bodies for public mutation endpoints", async () => {
    const fetchMock = vi
      .spyOn(globalThis, "fetch")
      .mockResolvedValue(new Response(JSON.stringify({ success: true }), {
        headers: { "content-type": "application/json" },
        status: 200,
      }));

    const request = new NextRequest("http://localhost:3001/api/v1/reservations", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ vehicleId: "vehicle-1" }),
    });

    const response = await POST(request, makeContext(["reservations"]));

    expect(response.status).toBe(200);
    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5135/api/v1/reservations",
      expect.objectContaining({
        body: expect.any(ArrayBuffer),
        method: "POST",
      })
    );
  });
});
