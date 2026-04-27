import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { ApiError, apiClient } from "./client";

describe("apiClient", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it("adds auth headers and returns parsed JSON data", async () => {
    localStorage.setItem("auth_token", "secret-token");
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: { "content-type": "application/json" },
      })
    );
    vi.stubGlobal("fetch", fetchMock);

    const result = await apiClient<{ ok: boolean }>("/vehicles");

    expect(result).toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5000/api/vehicles",
      expect.objectContaining({
        headers: expect.objectContaining({
          Authorization: "Bearer secret-token",
          Accept: "application/json",
          "Content-Type": "application/json",
        }),
      })
    );
  });

  it("retries transient network failures before succeeding", async () => {
    vi.useFakeTimers();
    const fetchMock = vi
      .fn()
      .mockRejectedValueOnce(new Error("first failure"))
      .mockRejectedValueOnce(new Error("second failure"))
      .mockResolvedValue(
        new Response(JSON.stringify({ ok: true }), {
          status: 200,
          headers: { "content-type": "application/json" },
        })
      );
    vi.stubGlobal("fetch", fetchMock);

    const promise = apiClient<{ ok: boolean }>("/pricing", { retries: 3, retryDelay: 10 });
    await vi.runAllTimersAsync();

    await expect(promise).resolves.toEqual({ ok: true });
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("throws ApiError instances for non-JSON error responses", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(null, { status: 404, statusText: "Not Found" }))
    );

    await expect(apiClient("/missing-resource")).rejects.toBeInstanceOf(ApiError);
    await expect(apiClient("/missing-resource")).rejects.toMatchObject({
      statusCode: 404,
      code: "NOT_FOUND",
      path: "/missing-resource",
    });
  });

  it("returns undefined for successful no-content responses", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 204 })));

    await expect(apiClient("/reservations/cleanup")).resolves.toBeUndefined();
  });
});
