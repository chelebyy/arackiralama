import { describe, expect, it } from "vitest";
import { rentalDateTimeLocal, rentalDateTimeUtc } from "./rental-datetime";

describe("rental timezone display", () => {
  it.each([
    ["2030-01-01", "00:00"],
    ["2030-01-01", "01:00"],
    ["2030-06-10", "10:00"],
    ["2030-06-14", "23:30"]
  ])("round trips %s %s through stored UTC", (date, time) => {
    expect(rentalDateTimeLocal(rentalDateTimeUtc(date, time))).toEqual({ date, time });
  });
  it("handles explicit offsets as the same instant", () => {
    expect(rentalDateTimeLocal("2030-06-09T18:00:00-04:00")).toEqual({ date: "2030-06-10", time: "01:00" });
  });
});
