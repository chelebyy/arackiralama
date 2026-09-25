import { describe, expect, it } from "vitest";
import { catalogueOffice, catalogueSearch, validCatalogueDates, vehiclePhotos } from "./vehicle-catalogue";
describe("catalogue context", () => {
  it("resolves office codes before names and supports legacy Turkish and ASCII names", () => {
    expect(catalogueOffice([{ id: "gzp-id", code: "gzp", name: "Renamed airport" }], "gzp")).toBe("gzp-id");
    for (const name of ["Gazipasa Airport", "Gazipaşa Havalimanı"]) {
      expect(catalogueOffice([{ id: "gzp-id", name }], "gzp")).toBe("gzp-id");
    }
    expect(catalogueOffice([{ id: "gzp-id", name: "Airport" }], "gzp-id")).toBe("gzp-id");
    expect(catalogueOffice([], "gzp")).toBeUndefined();
  });
  it("converts early morning rentals to the previous UTC day", () => {
    expect(validCatalogueDates(new URLSearchParams("pickupDate=2030-01-01&pickupTime=01:00&returnDate=2030-01-02&returnTime=01:00"), Date.parse("2029-01-01"))).toEqual({ pickup: "2029-12-31T22:00:00.000Z", returned: "2030-01-01T22:00:00.000Z" });
  });
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
