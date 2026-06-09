import { NextRequest } from "next/server";
import { afterEach, describe, expect, it, vi } from "vitest";

import { GET, POST } from "./route";

const makeContext = (path: string[]) => ({
  params: Promise.resolve({ path }),
});

describe("/api/v1/[...path] route", () => {
  afterEach(() => {
    vi.restoreAllMocks();
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
