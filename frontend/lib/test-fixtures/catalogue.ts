import type { PublicVehicle } from "@/lib/api/types";
export const catalogueVehicle: PublicVehicle = {
  id: "vehicle-1",
  brand: "Fiat",
  model: "Egea",
  year: 2024,
  color: "White",
  groupId: "group-1",
  groupName: "Ekonomi",
  groupNameEn: "Economy",
  officeId: "office-1",
  status: "Available",
  photoUrl: null,
  photoUrls: [],
  dailyPrice: null,
  depositAmount: 2000,
  minAge: 21,
  minLicenseYears: 2,
  features: ["bluetooth"],
  transmission: "manual",
  fuelType: "diesel",
  seatCount: 4,
  luggageCapacity: 1
};
