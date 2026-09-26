import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { AdminVehicle } from "@/lib/api/admin/types";
import VehicleDialog from "./VehicleDialog";

const api = vi.hoisted(() => ({
  createVehicle: vi.fn(),
  updateVehicle: vi.fn(),
  uploadVehiclePhoto: vi.fn(),
  updateVehiclePhotos: vi.fn()
}));
vi.mock("@/lib/api/admin/vehicles", () => api);
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
beforeEach(() => vi.clearAllMocks());

describe("Vehicle catalogue editing", () => {
  it.each(["Reserved", "Rented"] as const)("preserves %s status and unknown specifications on save", async status => {
    const vehicle: AdminVehicle = {
      id: "vehicle-1", plate: "07CAT001", brand: "Fiat", model: "Egea", year: 2024,
      color: "White", groupId: "group-1", officeId: "office-1", status,
      photoUrl: "/uploads/vehicles/legacy.png", transmission: null, fuelType: null,
      seatCount: null, luggageCapacity: null
    };
    api.updateVehicle.mockResolvedValue(vehicle);
    api.updateVehiclePhotos.mockResolvedValue(vehicle);
    render(<VehicleDialog open onOpenChange={vi.fn()} onSuccess={vi.fn()} vehicle={vehicle} groups={[]} offices={[]} />);
    fireEvent.click(screen.getByRole("button", { name: "Güncelle" }));
    await waitFor(() => expect(api.updateVehicle).toHaveBeenCalledWith("vehicle-1", expect.objectContaining({
      status, transmission: null, fuelType: null, seatCount: null, luggageCapacity: null,
      model: "Egea", year: 2024
    })));
    expect(api.updateVehiclePhotos).toHaveBeenCalledWith("vehicle-1", ["/uploads/vehicles/legacy.png"]);
    expect(api.createVehicle).not.toHaveBeenCalled();
  });
});
