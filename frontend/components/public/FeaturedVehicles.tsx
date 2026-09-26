"use client";
import { Suspense } from "react";
import { useParams, useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import VehicleCard from "./VehicleCard";
import { usePublicVehicles } from "@/hooks/useVehicles";

function FeaturedCatalogue() {
  const params = useParams(),
    search = useSearchParams();
  const locale = typeof params.locale === "string" ? params.locale : "tr";
  const t = useTranslations("vehicles");
  const { vehicles, isLoading, isError } = usePublicVehicles();
  if (isLoading) return <p className="py-12 text-center text-slate-600">{t("loading")}</p>;
  if (isError)
    return (
      <p role="alert" className="py-12 text-center text-slate-600">
        {t("failed")}
      </p>
    );
  const featured = vehicles.filter((vehicle) => vehicle.status === "Available").slice(0, 4);
  if (!featured.length) return <p className="py-12 text-center text-slate-600">{t("empty")}</p>;
  return (
    <div className="grid grid-cols-1 gap-[var(--space-fluid-lg)] sm:grid-cols-2 lg:grid-cols-4">
      {featured.map((vehicle) => (
        <VehicleCard key={vehicle.id} vehicle={vehicle} locale={locale} search={search} />
      ))}
    </div>
  );
}
export default function FeaturedVehicles() {
  return (
    <Suspense>
      <FeaturedCatalogue />
    </Suspense>
  );
}
