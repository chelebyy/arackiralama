import { NextRequest } from "next/server";
import { afterEach, describe, expect, it, vi } from "vitest";

import { GET } from "./route";

const makeContext = (path: string[]) => ({
  params: Promise.resolve({ path }),
});

describe("/api/admin/[...path] route", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("forwards /api/admin/v1 requests without duplicating the version prefix", async () => {
    const fetchMock = vi
      .spyOn(globalThis, "fetch")
      .mockResolvedValue(new Response(JSON.stringify({ success: true }), {
        headers: { "content-type": "application/json" },
        status: 200,
      }));

    const request = new NextRequest(
      "http://localhost:3001/api/admin/v1/vehicles?pageSize=1",
      {
        headers: {
          cookie: "rac_access=test-token",
        },
      }
    );

    const response = await GET(request, makeContext(["v1", "vehicles"]));

    expect(response.status).toBe(200);
    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5135/api/admin/v1/vehicles?pageSize=1",
      expect.objectContaining({
        method: "GET",
      })
    );
  });

  it("does not call the backend when the access cookie is missing", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch");
    const request = new NextRequest("http://localhost:3001/api/admin/v1/vehicles");

    const response = await GET(request, makeContext(["v1", "vehicles"]));
    const body = await response.json();

    expect(response.status).toBe(401);
    expect(body).toEqual({ success: false, message: "Yetkisiz erişim" });
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
