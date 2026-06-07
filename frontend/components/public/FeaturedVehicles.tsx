"use client";

import { useParams } from "next/navigation";
import { useTranslations } from "next-intl";
import VehicleCard from "@/components/public/VehicleCard";
import { usePublicVehicles } from "@/hooks/useVehicles";
import type { PublicVehicle } from "@/lib/api/types";

const DEFAULT_LOCATION = "ala";

function getDateOffset(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() + days);
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function resolveGroupLabel(vehicle: PublicVehicle, locale: string): string {
  if (locale === "tr") return vehicle.groupName || vehicle.groupNameEn || "fleet";
  return vehicle.groupNameEn || vehicle.groupName || "fleet";
}

function buildBookingHref(vehicle: PublicVehicle, locale: string) {
  const query = new URLSearchParams({
    pickup: DEFAULT_LOCATION,
    return: DEFAULT_LOCATION,
    pickupDate: getDateOffset(0),
    pickupTime: "10:00",
    returnDate: getDateOffset(7),
    returnTime: "10:00",
    vehicle: vehicle.groupId,
    dailyPrice: String(vehicle.dailyPrice),
    vehicleName: `${vehicle.brand} ${vehicle.model}`,
  });

  return `/${locale}/booking/step3?${query.toString()}`;
}

export default function FeaturedVehicles() {
  const params = useParams();
  const locale = typeof params.locale === "string" ? params.locale : "tr";
  const t = useTranslations("vehicles");
  const { vehicles, isLoading, isError } = usePublicVehicles();

  const featuredVehicles = vehicles
    .filter((vehicle) => vehicle.status === "Available")
    .slice(0, 4);

  if (isLoading) {
    return (
      <div className="text-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-[#0369A1] mx-auto" />
        <p className="mt-4 text-[#64748B]">Araçlar yükleniyor...</p>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="text-center py-12">
        <p className="text-[#64748B]">Araçlar yüklenemedi. Lütfen tekrar deneyin.</p>
      </div>
    );
  }

  if (featuredVehicles.length === 0) {
    return (
      <div className="text-center py-12">
        <p className="text-[#64748B]">Şu anda gösterilecek araç bulunamadı.</p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-[var(--space-fluid-lg)]">
      {featuredVehicles.map((vehicle) => {
        const vehicleName = `${vehicle.brand} ${vehicle.model}`;

        return (
          <VehicleCard
            key={vehicle.id}
            id={vehicle.id}
            name={vehicleName}
            category={vehicle.groupNameEn?.toLowerCase() || vehicle.groupName?.toLowerCase() || "fleet"}
            categoryLabel={resolveGroupLabel(vehicle, locale)}
            image={vehicle.photoUrl ?? undefined}
            seats={5}
            transmission="automatic"
            fuelType="gasoline"
            pricePerDay={vehicle.dailyPrice}
            freeKm={200}
            bookingHref={buildBookingHref(vehicle, locale)}
          />
        );
      })}
    </div>
  );
}
