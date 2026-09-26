import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import type { OperatingPolicy } from "@/lib/rental-policy";
import { OperatingPolicyEditor } from "./OperatingPolicyEditor";
import { RentalTermsEditor } from "./RentalTermsEditor";

vi.mock("swr", () => ({ default: () => ({ data: { items: [{ id: "extra-1", code: "seat", translations: [{ locale: "tr", name: "Çocuk koltuğu" }] }] } }) }));

describe("Rental policy editors", () => {
  it("preserves the remaining window DOM and values after editing and deleting another row", () => {
    const initial = { minimumNoticeMinutes: 0, preparationMinutes: 60, pickupWindows: [
      { day: 1, startMinute: 540, endMinute: 720 },
      { day: 2, startMinute: 600, endMinute: 1080 }
    ], returnWindows: [], closedDates: [] };
    function Editor() {
      const [value, setValue] = useState<OperatingPolicy>(initial);
      return <OperatingPolicyEditor value={value} onChange={setValue} />;
    }
    render(<Editor />);
    const remaining = screen.getByLabelText("Teslim başlangıç 2");
    fireEvent.change(remaining, { target: { value: "11:00" } });
    expect(screen.getByLabelText("Teslim başlangıç 2")).toBe(remaining);
    fireEvent.click(screen.getAllByRole("button", { name: /^Sil$/ })[0]);
    expect(screen.getByLabelText("Teslim başlangıç 1")).toBe(remaining);
    expect(remaining).toHaveValue("11:00");
    expect(screen.getByLabelText("Teslim günü 1")).toHaveValue("2");
  });

  it("allows removing an existing extra outside the loaded catalogue page", () => {
    const value = { depositAmount: 2000, minAge: 23, minLicenseYears: 3, extraOptionIds: ["unlisted-extra"], rates: [] };
    const change = vi.fn();
    render(<RentalTermsEditor value={value} onChange={change} />);
    fireEvent.click(screen.getByRole("button", { name: "Seçimi kaldır" }));
    expect(change).toHaveBeenLastCalledWith({ ...value, extraOptionIds: [] });
  });
  it("does not invent notice, preparation or operating hours when configuring an office", () => {
    const change = vi.fn();
    const { rerender } = render(<OperatingPolicyEditor onChange={change} />);
    expect(screen.getByText(/otomatik rezervasyon alınamaz/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Ayarları yapılandır" }));
    const empty = change.mock.calls[0][0];
    expect(empty).toMatchObject({ minimumNoticeMinutes: null, preparationMinutes: null, pickupWindows: [], returnWindows: [] });
    rerender(<OperatingPolicyEditor value={empty} onChange={change} />);
    fireEvent.click(screen.getAllByRole("button", { name: "Saat aralığı ekle" })[0]);
    expect(change.mock.calls.at(-1)?.[0].pickupWindows[0]).toMatchObject({ day: 1, startMinute: -1, endMinute: -1 });
  });

  it("round-trips numeric API weekdays and sends numeric changes", () => {
    const value = { minimumNoticeMinutes: 0, preparationMinutes: 60, pickupWindows: [{ day: 6, startMinute: 540, endMinute: 1080 }], returnWindows: [{ day: 2, startMinute: 540, endMinute: 1080 }], closedDates: [] };
    const change = vi.fn();
    render(<OperatingPolicyEditor value={value} onChange={change} />);
    expect(screen.getByLabelText("Teslim günü 1")).toHaveValue("6");
    expect(screen.getByLabelText("İade günü 1")).toHaveValue("2");
    fireEvent.change(screen.getByLabelText("Teslim günü 1"), { target: { value: "0" } });
    expect(change).toHaveBeenLastCalledWith({ ...value, pickupWindows: [{ day: 0, startMinute: 540, endMinute: 1080 }] });
  });

  it("edits vehicle conditions without losing the copied seasonal rules and eligible extras", () => {
    const value = { depositAmount: 2000, minAge: 23, minLicenseYears: 3, extraOptionIds: ["extra-1"], rates: [
      { id: "rate-1", startDate: "2026-10-01", endDate: "2026-10-31", dailyPrice: 1800, multiplier: 1.2, weekdayMultiplier: 1, weekendMultiplier: 1.1, calculationType: "multiplier" as const, priority: 5, createdAt: "2026-09-01T00:00:00Z" }
    ] };
    const change = vi.fn();
    render(<RentalTermsEditor value={value} onChange={change} />);
    fireEvent.change(screen.getByLabelText("Asgari yaş"), { target: { value: "25" } });
    expect(change).toHaveBeenLastCalledWith({ ...value, minAge: 25 });
    expect(screen.getByLabelText("Çocuk koltuğu")).toBeChecked();
    fireEvent.click(screen.getByLabelText("Çocuk koltuğu"));
    expect(change).toHaveBeenLastCalledWith({ ...value, extraOptionIds: [] });
  });
});
