import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import ReservationExtrasPage from "./page";

const mocks = vi.hoisted(() => ({
  useExtras: vi.fn(),
  useGroups: vi.fn(),
  updateStatus: vi.fn(),
  remove: vi.fn(),
  restore: vi.fn(),
  toastSuccess: vi.fn(),
  toastError: vi.fn()
}));

vi.mock("@/hooks/admin", () => ({
  useAdminReservationExtras: (...args: unknown[]) => mocks.useExtras(...args),
  useAdminVehicleGroups: (...args: unknown[]) => mocks.useGroups(...args),
  mutateUpdateReservationExtraOptionStatus: (...args: unknown[]) => mocks.updateStatus(...args),
  mutateDeleteReservationExtraOption: (...args: unknown[]) => mocks.remove(...args),
  mutateRestoreReservationExtraOption: (...args: unknown[]) => mocks.restore(...args)
}));

vi.mock("sonner", () => ({
  toast: {
    success: (...args: unknown[]) => mocks.toastSuccess(...args),
    error: (...args: unknown[]) => mocks.toastError(...args)
  }
}));

vi.mock("@/components/admin/dialogs/ReservationExtraOptionDialog", () => ({
  default: ({ open, option, onSaved }: any) =>
    open ? (
      <div role="dialog" aria-label="rezervasyon ekstra editörü">
        <span>{option?.code ?? "new option"}</span>
        <button type="button" onClick={() => onSaved({ id: "saved" })}>
          editörü kaydet
        </button>
      </div>
    ) : null
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
  translations: [{ locale: "tr" as const, name: "Çocuk Koltuğu", description: "Açıklama" }]
};

describe("ReservationExtrasPage", () => {
  let focusSpy: ReturnType<typeof vi.spyOn>;
  const mutate = vi.fn();

  beforeAll(() => {
    focusSpy = vi.spyOn(HTMLElement.prototype, "focus").mockImplementation(() => undefined);
  });

  afterAll(() => {
    focusSpy.mockRestore();
  });

  beforeEach(() => {
    Object.values(mocks).forEach((mock) => mock.mockReset());
    mutate.mockReset();
    mutate.mockResolvedValue(undefined);
    mocks.useGroups.mockReturnValue({
      groups: [{ id: "group-1", nameTr: "Ekonomi" }],
      isLoading: false,
      isError: undefined
    });
    mocks.useExtras.mockReturnValue({
      options: [],
      pagination: null,
      isLoading: false,
      isError: undefined,
      mutate
    });
  });

  it("renders loading, error, and empty states", () => {
    mocks.useExtras.mockReturnValueOnce({
      options: [],
      pagination: null,
      isLoading: true,
      isError: undefined,
      mutate
    });
    const { rerender } = render(<ReservationExtrasPage />);
    expect(screen.getByLabelText("Rezervasyon ekstraları yükleniyor")).toBeInTheDocument();

    mocks.useExtras.mockReturnValueOnce({
      options: [],
      pagination: null,
      isLoading: false,
      isError: new Error("Bağlantı yok"),
      mutate
    });
    rerender(<ReservationExtrasPage />);
    expect(screen.getByText("Rezervasyon ekstraları yüklenemedi")).toBeInTheDocument();

    mocks.useExtras.mockReturnValue({
      options: [],
      pagination: null,
      isLoading: false,
      isError: undefined,
      mutate
    });
    rerender(<ReservationExtrasPage />);
    expect(screen.getByText("Kayıt bulunamadı")).toBeInTheDocument();
  });

  it("uses filter input in the stable list request and opens create/edit flows", async () => {
    const user = userEvent.setup();
    mocks.useExtras.mockReturnValue({
      options: [option],
      pagination: { page: 1, pageSize: 20, totalCount: 1, totalPages: 1 },
      isLoading: false,
      isError: undefined,
      mutate
    });
    render(<ReservationExtrasPage />);

    expect(screen.getByText("Çocuk Koltuğu")).toBeInTheDocument();
    fireEvent.change(screen.getByRole("textbox", { name: "Rezervasyon ekstralarında ara" }), {
      target: { value: "koltuk" }
    });
    await waitFor(() =>
      expect(mocks.useExtras).toHaveBeenCalledWith(
        expect.objectContaining({ search: "koltuk", page: 1 })
      )
    );

    await user.click(screen.getByRole("button", { name: "Yeni Ekstra" }));
    expect(screen.getByRole("dialog", { name: "rezervasyon ekstra editörü" })).toHaveTextContent(
      "new option"
    );
    await user.click(screen.getByRole("button", { name: "editörü kaydet" }));
    await waitFor(() => expect(mutate).toHaveBeenCalled());

    await user.click(screen.getByRole("button", { name: "Çocuk Koltuğu düzenle" }));
    expect(screen.getByRole("dialog", { name: "rezervasyon ekstra editörü" })).toHaveTextContent(
      "extra-option-1"
    );
  });

  it("delegates status and confirmed delete actions with the server version", async () => {
    const user = userEvent.setup();
    mocks.useExtras.mockReturnValue({
      options: [option],
      pagination: null,
      isLoading: false,
      isError: undefined,
      mutate
    });
    mocks.updateStatus.mockResolvedValue({ ...option, isActive: false, version: 8 });
    mocks.remove.mockResolvedValue({ disposition: "Archived" });
    render(<ReservationExtrasPage />);

    await user.click(screen.getByRole("button", { name: "Çocuk Koltuğu pasif et" }));
    await waitFor(() =>
      expect(mocks.updateStatus).toHaveBeenCalledWith("option-1", { version: 7, isActive: false })
    );

    await user.click(screen.getByRole("button", { name: "Çocuk Koltuğu sil veya arşivle" }));
    await user.click(screen.getByRole("button", { name: "Devam Et" }));
    await waitFor(() => expect(mocks.remove).toHaveBeenCalledWith("option-1", 7));
  });

  it("offers restore only for archived records", async () => {
    const user = userEvent.setup();
    const archived = { ...option, isActive: false, isArchived: true, version: 9 };
    mocks.useExtras.mockReturnValue({
      options: [archived],
      pagination: null,
      isLoading: false,
      isError: undefined,
      mutate
    });
    mocks.restore.mockResolvedValue({ ...archived, isArchived: false, version: 10 });
    render(<ReservationExtrasPage />);

    expect(screen.queryByRole("button", { name: "Çocuk Koltuğu düzenle" })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Çocuk Koltuğu taslağa geri yükle" }));
    await user.click(screen.getByRole("button", { name: "Geri Yükle" }));
    await waitFor(() => expect(mocks.restore).toHaveBeenCalledWith("option-1", 9));
  });
});
