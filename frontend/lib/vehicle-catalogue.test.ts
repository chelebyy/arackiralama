import { describe, expect, it } from "vitest";
import { catalogueSearch, validCatalogueDates, vehiclePhotos } from "./vehicle-catalogue";
describe("catalogue context", () => {
  it("never invents dates and removes unrelated query data", () => {
    expect(catalogueSearch(new URLSearchParams()).toString()).toBe("");
    expect(
      catalogueSearch(new URLSearchParams("pickup=ala&vehicle=other&dailyPrice=1")).toString()
    ).toBe("pickup=ala");
  });
  it("rejects missing, past, reversed and invalid calendar dates", () => {
    const now = Date.parse("2029-01-01");
    for (const value of [
      "",
      "pickupDate=2025-01-01&pickupTime=10:00&returnDate=2025-01-02&returnTime=10:00",
      "pickupDate=2030-02-30&pickupTime=10:00&returnDate=2030-03-03&returnTime=10:00",
      "pickupDate=2030-03-03&pickupTime=10:00&returnDate=2030-03-01&returnTime=10:00"
    ])
      expect(validCatalogueDates(new URLSearchParams(value), now)).toBeNull();
  });
  it("interprets Turkish rental times consistently", () => {
    expect(
      validCatalogueDates(
        new URLSearchParams(
          "pickupDate=2030-01-01&pickupTime=10:00&returnDate=2030-01-02&returnTime=10:00"
        ),
        Date.parse("2029-01-01")
      )?.pickup
    ).toBe("2030-01-01T07:00:00.000Z");
  });
  it("preserves legacy photos and rejects unsupported URL schemes", () => {
    expect(vehiclePhotos({ photoUrl: "/uploads/vehicles/old.png" })).toHaveLength(1);
    expect(vehiclePhotos({ photoUrl: "javascript:alert(1)" })).toEqual([]);
  });
});
