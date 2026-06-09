import { NextRequest } from "next/server";
import { afterEach, describe, expect, it, vi } from "vitest";

import { GET } from "./route";

const makeContext = (path: string[]) => ({
  params: Promise.resolve({ path }),
});

describe("/uploads/[...path] route", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("proxies uploaded vehicle images from the backend", async () => {
    const fetchMock = vi
      .spyOn(globalThis, "fetch")
      .mockResolvedValue(new Response("image-bytes", {
        headers: { "content-type": "image/jpeg" },
        status: 200,
      }));

    const request = new NextRequest("http://localhost:3001/uploads/vehicles/car.jpg");

    const response = await GET(request, makeContext(["vehicles", "car.jpg"]));

    expect(response.status).toBe(200);
    expect(response.headers.get("content-type")).toBe("image/jpeg");
    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5135/uploads/vehicles/car.jpg",
      expect.objectContaining({
        method: "GET",
      })
    );
  });
});
