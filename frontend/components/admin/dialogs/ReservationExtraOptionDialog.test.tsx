import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import ReservationExtraOptionDialog, {
  getReservationExtraReadiness
} from "./ReservationExtraOptionDialog";
import { ApiError } from "@/lib/api/client";

const mutations = vi.hoisted(() => ({
  create: vi.fn(),
  update: vi.fn(),
  status: vi.fn()
}));

vi.mock("@/hooks/admin", () => ({
  mutateCreateReservationExtraOption: (...args: unknown[]) => mutations.create(...args),
  mutateUpdateReservationExtraOption: (...args: unknown[]) => mutations.update(...args),
  mutateUpdateReservationExtraOptionStatus: (...args: unknown[]) => mutations.status(...args)
}));

vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const translations = (["tr", "en", "de", "ru", "ar"] as const).map((locale) => ({
  locale,
  name: `${locale} name`,
  description: `${locale} description`
}));

const option = {
  id: "option-1",
  code: "extra-option-1",
  unitPrice: 75,
  pricingMode: "PER_DAY" as const,
  maxQuantity: 3,
  iconKey: "baby" as const,
  sortOrder: 1,
  isActive: true,
  isArchived: false,
  version: 7,
  updatedAt: "2026-07-10T00:00:00Z",
  vehicleGroupIds: ["group-1"],
  translations
};

const groups = [
  {
    id: "group-1",
    nameTr: "Ekonomi",
    depositAmount: 0,
    minAge: 21,
    minLicenseYears: 1,
    isActive: true,
    features: []
  }
];

describe("ReservationExtraOptionDialog", () => {
  let focusSpy: ReturnType<typeof vi.spyOn>;

  beforeAll(() => {
    focusSpy = vi.spyOn(HTMLElement.prototype, "focus").mockImplementation(() => undefined);
    vi.stubGlobal(
      "ResizeObserver",
      class {
        observe() {}
        unobserve() {}
        disconnect() {}
      }
    );
  });

  afterAll(() => {
    focusSpy.mockRestore();
    vi.unstubAllGlobals();
  });

  beforeEach(() => {
    Object.values(mutations).forEach((mock) => mock.mockReset());
  });

  it("requires all five locales and a vehicle group for activation readiness", () => {
    const incomplete = {
      unitPrice: 0,
      pricingMode: "PER_RENTAL" as const,
      maxQuantity: 1,
      iconKey: "baby" as const,
      sortOrder: 0,
      vehicleGroupIds: [],
      translations: {
        tr: { name: "Çocuk koltuğu", description: "Açıklama" },
        en: { name: "", description: "" },
        de: { name: "", description: "" },
        ru: { name: "", description: "" },
        ar: { name: "", description: "" }
      }
    };

    expect(getReservationExtraReadiness(incomplete)).toEqual({
      missingLocales: ["en", "de", "ru", "ar"],
      hasVehicleGroup: false,
      isReady: false
    });

    expect(
      getReservationExtraReadiness({
        ...incomplete,
        vehicleGroupIds: ["group-1"],
        translations: Object.fromEntries(
          (["tr", "en", "de", "ru", "ar"] as const).map((locale) => [
            locale,
            { name: `${locale} name`, description: `${locale} description` }
          ])
        ) as typeof incomplete.translations
      }).isReady
    ).toBe(true);
  });

  it("saves an incomplete new option as draft and closes only with the returned DTO", async () => {
    const user = userEvent.setup();
    const onSaved = vi.fn();
    mutations.create.mockResolvedValue({ ...option, isActive: false, translations: [] });

    render(
      <ReservationExtraOptionDialog
        open
        onOpenChange={vi.fn()}
        vehicleGroups={groups}
        onSaved={onSaved}
        onReload={vi.fn()}
      />
    );

    expect(screen.getByRole("button", { name: "Kaydet ve Aktifleştir" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "Taslak Kaydet" }));

    await waitFor(() => expect(mutations.create).toHaveBeenCalledTimes(1));
    expect(mutations.status).not.toHaveBeenCalled();
    expect(onSaved).toHaveBeenCalledWith(expect.objectContaining({ id: "option-1" }));
  });

  it("updates an active option without a client-side optimistic status write", async () => {
    const user = userEvent.setup();
    const onSaved = vi.fn();
    mutations.update.mockResolvedValue({ ...option, version: 8 });

    render(
      <ReservationExtraOptionDialog
        open
        onOpenChange={vi.fn()}
        option={option}
        vehicleGroups={groups}
        onSaved={onSaved}
        onReload={vi.fn()}
      />
    );

    await user.click(screen.getByRole("button", { name: "Değişiklikleri Kaydet" }));

    await waitFor(() =>
      expect(mutations.update).toHaveBeenCalledWith(
        "option-1",
        expect.objectContaining({ version: 7, unitPrice: 75 })
      )
    );
    expect(mutations.status).not.toHaveBeenCalled();
    expect(onSaved).toHaveBeenCalledWith(expect.objectContaining({ version: 8 }));
  });

  it("preserves the saved DTO when the follow-up activation request fails", async () => {
    const user = userEvent.setup();
    const onSaved = vi.fn();
    const inactiveOption = { ...option, isActive: false };
    mutations.update.mockResolvedValue({ ...inactiveOption, version: 8 });
    mutations.status.mockRejectedValue(new Error("Activation rejected"));

    render(
      <ReservationExtraOptionDialog
        open
        onOpenChange={vi.fn()}
        option={inactiveOption}
        vehicleGroups={groups}
        onSaved={onSaved}
        onReload={vi.fn()}
      />
    );

    await user.click(screen.getByRole("button", { name: "Kaydet ve Aktifleştir" }));

    await waitFor(() =>
      expect(mutations.status).toHaveBeenCalledWith("option-1", {
        version: 8,
        isActive: true
      })
    );
    expect(onSaved).toHaveBeenCalledWith(expect.objectContaining({ version: 8, isActive: false }));
  });

  it("offers an explicit reload choice after a stale-version conflict", async () => {
    const user = userEvent.setup();
    const onReload = vi.fn();
    mutations.update.mockRejectedValue(
      new ApiError({
        statusCode: 409,
        message: "Changed",
        code: "CONFLICT",
        timestamp: new Date().toISOString(),
        path: "/v1/reservation-extra-options/option-1"
      })
    );

    render(
      <ReservationExtraOptionDialog
        open
        onOpenChange={vi.fn()}
        option={option}
        vehicleGroups={groups}
        onSaved={vi.fn()}
        onReload={onReload}
      />
    );

    await user.click(screen.getByRole("button", { name: "Değişiklikleri Kaydet" }));
    expect(await screen.findByText("Sürüm çakışması")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Güncel veriyi yükle" }));
    expect(onReload).toHaveBeenCalledTimes(1);
  });
});
