import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readGuestChallenge, storeGuestChallenge } from "./guest-access-challenge";

describe("pending guest access", () => {
  beforeEach(() => { sessionStorage.clear(); vi.useFakeTimers(); vi.setSystemTime(new Date("2026-09-28T12:00:00Z")); });
  afterEach(() => { vi.restoreAllMocks(); vi.useRealTimers(); sessionStorage.clear(); });

  it.each(["not-json", "null", '{"challengeId":42,"requestedAt":1}', '{"challengeId":"x","requestedAt":"today"}'])("ignores malformed storage: %s", value => {
    sessionStorage.setItem("guest-access-challenge", value);
    expect(readGuestChallenge()).toBeNull();
    expect(sessionStorage.getItem("guest-access-challenge")).toBeNull();
  });

  it("restores only the pending challenge until its original expiry", () => {
    const pending = { challengeId: "pending", requestedAt: Date.now() };
    storeGuestChallenge(pending);
    vi.advanceTimersByTime(599000);
    expect(readGuestChallenge()).toEqual(pending);
    vi.advanceTimersByTime(1000);
    expect(readGuestChallenge()).toBeNull();
  });

  it("rejects future-dated storage and allows explicit removal", () => {
    storeGuestChallenge({ challengeId: "pending", requestedAt: Date.now() + 1000 });
    expect(readGuestChallenge()).toBeNull();
    storeGuestChallenge({ challengeId: "pending", requestedAt: Date.now() });
    storeGuestChallenge(null);
    expect(readGuestChallenge()).toBeNull();
  });

  it("keeps storage failures from breaking in-memory access", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => { throw new DOMException("Blocked", "SecurityError"); });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => { throw new DOMException("Blocked", "SecurityError"); });
    vi.spyOn(Storage.prototype, "removeItem").mockImplementation(() => { throw new DOMException("Blocked", "SecurityError"); });
    expect(() => storeGuestChallenge({ challengeId: "pending", requestedAt: Date.now() })).not.toThrow();
    expect(readGuestChallenge()).toBeNull();
  });
});
