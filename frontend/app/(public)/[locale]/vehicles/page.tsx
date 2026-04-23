"use client";

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/routing";
import {
  Car,
  Users,
  Briefcase,
  Fuel,
  Calendar,
  MapPin,
  ChevronRight,
  Grid3X3,
  List,
  SlidersHorizontal,
  Gauge,
  X,
  Check,
} from "lucide-react";
import { cn } from "@/lib/utils";

interface Vehicle {
  id: string;
  name: string;
  group: string;
  image: string;
  passengers: number;
  luggage: number;
  transmission: "manual" | "automatic";
  fuelType: "gasoline" | "diesel" | "hybrid";
  dailyRate: number;
  features: string[];
  available: boolean;
}

const mockVehicles: Vehicle[] = [
  {
    id: "1",
    name: "Fiat Egea",
    group: "economy",
    image: "/images/vehicles/fiat-egea.png",
    passengers: 5,
    luggage: 2,
    transmission: "automatic",
    fuelType: "gasoline",
    dailyRate: 45,
    features: ["A/C", "Bluetooth", "GPS"],
    available: true,
  },
  {
    id: "2",
    name: "Renault Megane",
    group: "compact",
    image: "/images/vehicles/renault-megane.png",
    passengers: 5,
    luggage: 3,
    transmission: "automatic",
    fuelType: "diesel",
    dailyRate: 55,
    features: ["A/C", "Cruise Control", "Parking Sensors"],
    available: true,
  },
  {
    id: "3",
    name: "VW Passat",
    group: "midsize",
    image: "/images/vehicles/vw-passat.png",
    passengers: 5,
    luggage: 3,
    transmission: "automatic",
    fuelType: "diesel",
    dailyRate: 75,
    features: ["Leather Seats", "Sunroof", "Navigation"],
    available: true,
  },
  {
    id: "4",
    name: "BMW 3 Series",
    group: "luxury",
    image: "/images/vehicles/bmw-3.png",
    passengers: 5,
    luggage: 2,
    transmission: "automatic",
    fuelType: "gasoline",
    dailyRate: 95,
    features: ["Leather Seats", "Premium Sound", "Parking Assistant"],
    available: true,
  },
  {
    id: "5",
    name: "Mercedes Vito",
    group: "minivan",
    image: "/images/vehicles/mercedes-vito.png",
    passengers: 9,
    luggage: 5,
    transmission: "automatic",
    fuelType: "diesel",
    dailyRate: 120,
    features: ["Extra Space", "Dual A/C", "Rear Camera"],
    available: true,
  },
  {
    id: "6",
    name: "Audi Q5",
    group: "suv",
    image: "/images/vehicles/audi-q5.png",
    passengers: 5,
    luggage: 4,
    transmission: "automatic",
    fuelType: "diesel",
    dailyRate: 110,
    features: ["4WD", "Panoramic Roof", "Virtual Cockpit"],
    available: true,
  },
];

const vehicleGroups = ["all", "economy", "compact", "suv", "luxury", "minivan"];

const offices = [
  { id: "ala", name: "Alanya Şehir Merkezi" },
  { id: "gzp", name: "Gazipaşa Havalimanı" },
  { id: "ayt", name: "Antalya Havalimanı" },
  { id: "mahmutlar", name: "Mahmutlar" },
  { id: "kargicak", name: "Kargıcak" },
  { id: "konakli", name: "Konaklı" },
  { id: "avsallar", name: "Avsallar" },
];

export default function VehiclesPage() {
  const searchParams = useSearchParams();
  const t = useTranslations("vehicles");
  const tCommon = useTranslations("common");
  const tSearch = useTranslations("searchForm");

  const [viewMode, setViewMode] = useState<"grid" | "list">("grid");
  const [selectedGroup, setSelectedGroup] = useState("all");
  const [mobileFiltersOpen, setMobileFiltersOpen] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);

  const pickupOffice = searchParams.get("pickup") || "ala";
  const pickupDate = searchParams.get("pickupDate") || "2025-04-01";
  const returnDate = searchParams.get("returnDate") || "2025-04-08";

  const pickupOfficeObj = offices.find((o) => o.id === pickupOffice);
  const locationKeyMap: Record<string, string> = {
    ala: "cityCenter",
    gzp: "airport",
    ayt: "airportAntalya",
    mahmutlar: "mahmutlar",
    kargicak: "kargicak",
    konakli: "konakli",
    avsallar: "avsallar",
  };
  const locationKey = locationKeyMap[pickupOfficeObj?.id ?? ""] ?? "airport";

  const filteredVehicles =
    selectedGroup === "all"
      ? mockVehicles
      : mockVehicles.filter((v) => v.group === selectedGroup);

  const totalPages = Math.ceil(filteredVehicles.length / 6);

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-white border-b border-slate-200 sticky top-0 z-30">
        <div className="mx-auto max-w-7xl px-[var(--space-fluid-md)] lg:px-[var(--space-fluid-lg)] py-[var(--space-fluid-md)]">
          <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-[var(--space-fluid-md)]">
            <div className="flex items-center gap-4 bg-slate-50 px-4 py-3 rounded-lg border border-slate-200 overflow-x-auto">
              <div className="flex items-center gap-2 whitespace-nowrap">
                <MapPin className="h-4 w-4 text-[#0369A1]" />
                <span className="text-sm text-slate-700">
                  {tSearch(`locationOptions.${locationKey}` as any)}
                </span>
              </div>
              <ChevronRight className="h-4 w-4 text-slate-400 shrink-0" />
              <div className="flex items-center gap-2 whitespace-nowrap">
                <Calendar className="h-4 w-4 text-[#0369A1]" />
                <span className="text-sm text-slate-700">
                  {pickupDate} &rarr; {returnDate}
                </span>
              </div>
            </div>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-[var(--space-fluid-md)] lg:px-[var(--space-fluid-lg)] py-[var(--space-fluid-xl)]">
        <div className="flex flex-col lg:flex-row gap-[var(--space-fluid-xl)]">
          <button
            type="button"
            onClick={() => setMobileFiltersOpen(true)}
            className="lg:hidden flex items-center justify-center gap-2 px-4 py-2 bg-white border border-slate-200 rounded-lg text-slate-700 hover:bg-slate-50"
          >
            <SlidersHorizontal className="h-4 w-4" />
            {tCommon("buttons.filter")}
          </button>

          <aside
            className={cn(
              "fixed inset-0 z-40 lg:relative lg:inset-auto lg:block shrink-0",
              mobileFiltersOpen ? "block" : "hidden lg:block",
            )}
          >
            <button
              type="button"
              className="absolute inset-0 bg-black/50 lg:hidden"
              onClick={() => setMobileFiltersOpen(false)}
              aria-label="Close filters"
            />
            <div className="absolute right-0 top-0 h-full w-80 bg-white p-[var(--space-fluid-lg)] lg:relative lg:w-64 lg:p-0 lg:bg-transparent">
              <div className="flex items-center justify-between lg:hidden mb-[var(--space-fluid-md)]">
                <h2 className="text-[length:var(--text-fluid-lg)] font-semibold text-slate-900">{tCommon("buttons.filter")}</h2>
                <button
                  type="button"
                  onClick={() => setMobileFiltersOpen(false)}
                  className="p-2 hover:bg-slate-100 rounded-lg"
                  aria-label="Close filters"
                >
                  <X className="h-5 w-5 text-slate-500" />
                </button>
              </div>

              <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-sm">
                <h3 className="text-sm font-semibold text-slate-900 mb-4">{t("title")}</h3>
                <div className="space-y-2">
                  {vehicleGroups.map((group) => (
                    <button
                      key={group}
                      type="button"
                      onClick={() => setSelectedGroup(group)}
                      className={cn(
                        "w-full text-left px-3 py-2 rounded-lg text-sm transition-colors",
                        selectedGroup === group
                          ? "bg-[#0369A1]/10 text-[#0369A1] font-medium"
                          : "text-slate-600 hover:bg-slate-50"
                      )}
                    >
                      {t.has(`categories.${group}`) ? t(`categories.${group}`) : group}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          </aside>

          <div className="flex-1 min-w-0">
            <div className="flex items-center justify-between mb-[var(--space-fluid-lg)]">
              <p className="text-slate-600">
                <span className="font-semibold text-[#0369A1]">{filteredVehicles.length}</span>{" "}
                {t("title").toLowerCase()}
              </p>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => setViewMode("grid")}
                  className={cn(
                    "p-2 rounded-lg transition-colors",
                    viewMode === "grid"
                      ? "bg-[#0369A1]/10 text-[#0369A1]"
                      : "text-slate-400 hover:text-slate-600 focus:bg-slate-100"
                  )}
                  aria-label="Grid view"
                >
                  <Grid3X3 className="h-5 w-5" />
                </button>
                <button
                  type="button"
                  onClick={() => setViewMode("list")}
                  className={cn(
                    "p-2 rounded-lg transition-colors",
                    viewMode === "list"
                      ? "bg-[#0369A1]/10 text-[#0369A1]"
                      : "text-slate-400 hover:text-slate-600 focus:bg-slate-100"
                  )}
                  aria-label="List view"
                >
                  <List className="h-5 w-5" />
                </button>
              </div>
            </div>

            <div
              className={cn(
                "grid gap-[var(--space-fluid-lg)]",
                viewMode === "grid"
                  ? "grid-cols-1 md:grid-cols-2 xl:grid-cols-3"
                  : "grid-cols-1"
              )}
            >
              {filteredVehicles.map((vehicle) => (
                <div
                  key={vehicle.id}
                  className={cn(
                    "@container group relative rounded-2xl bg-white border border-[#E2E8F0]",
                    "overflow-hidden transition-all duration-300",
                    "hover:shadow-xl hover:border-[#0369A1]/30 flex",
                    viewMode === "list" ? "flex-col @sm:flex-row" : "flex-col h-full"
                  )}
                >
                  <div
                    className={cn(
                      "relative bg-gradient-to-br from-[#F1F5F9] to-[#E2E8F0] overflow-hidden shrink-0",
                      viewMode === "list" ? "@sm:w-64 aspect-[16/10] @sm:aspect-auto" : "aspect-[16/10]"
                    )}
                  >
                    {vehicle.image && (
                      <img
                        src={vehicle.image}
                        alt={vehicle.name}
                        className="absolute inset-0 w-full h-full object-cover transition-transform duration-500 group-hover:scale-105"
                        onError={(e) => {
                          e.currentTarget.style.display = 'none';
                          e.currentTarget.parentElement?.querySelector('.fallback')?.classList.remove('hidden');
                          e.currentTarget.parentElement?.querySelector('.fallback')?.classList.add('flex');
                        }}
                      />
                    )}
                    <div className={cn("fallback absolute inset-0 items-center justify-center bg-slate-100", vehicle.image ? "hidden" : "flex")}>
                      <Car className="w-16 h-16 md:w-24 md:h-24 text-[#CBD5E1]" aria-hidden="true" />
                    </div>
                    
                    {/* Top Badges */}
                    <div className="absolute top-[var(--space-fluid-sm)] left-0 right-0 px-[var(--space-fluid-sm)] flex justify-between items-start gap-[var(--space-fluid-xs)] overflow-hidden pointer-events-none">
                      <span className="px-[var(--space-fluid-xs)] py-1 rounded-lg text-[10px] @sm:text-xs font-semibold bg-white/90 backdrop-blur-sm text-[#0369A1] shadow-sm whitespace-nowrap truncate max-w-[50%]">
                        {t.has(`categories.${vehicle.group}`) ? t(`categories.${vehicle.group}`) : vehicle.group}
                      </span>
                      <span className="flex items-center gap-1 px-[var(--space-fluid-xs)] py-1 rounded-lg text-[10px] @sm:text-xs font-medium bg-[#10B981] text-white shadow-sm whitespace-nowrap truncate max-w-[50%]">
                        <Check className="h-3 w-3 shrink-0" />
                        <span className="truncate">{t("freeCancellation")}</span>
                      </span>
                    </div>
                  </div>
                  
                  <div className="p-[var(--space-fluid-sm)] flex flex-col flex-1 space-y-[var(--space-fluid-sm)]">
                    <h3 className="text-[length:var(--text-fluid-lg)] font-bold text-[#0F172A] truncate">
                      {vehicle.name}
                    </h3>

                    <div className="flex flex-wrap gap-[var(--space-fluid-xs)]">
                      <div className="flex items-center gap-1.5 px-2 py-1 rounded-lg bg-[#F8FAFC] text-[length:var(--text-fluid-sm)] text-[#475569]">
                        <Users className="h-3.5 w-3.5 text-[#0369A1]" />
                        {vehicle.passengers} {t("features.seats")}
                      </div>
                      <div className="flex items-center gap-1.5 px-2 py-1 rounded-lg bg-[#F8FAFC] text-[length:var(--text-fluid-sm)] text-[#475569]">
                        <Briefcase className="h-3.5 w-3.5 text-[#0369A1]" />
                        {vehicle.luggage}
                      </div>
                      <div className="flex items-center gap-1.5 px-2 py-1 rounded-lg bg-[#F8FAFC] text-[length:var(--text-fluid-sm)] text-[#475569]">
                        <Gauge className="h-3.5 w-3.5 text-[#0369A1]" />
                        {t(`features.${vehicle.transmission}`)}
                      </div>
                      <div className="flex items-center gap-1.5 px-2 py-1 rounded-lg bg-[#F8FAFC] text-[length:var(--text-fluid-sm)] text-[#475569]">
                        <Fuel className="h-3.5 w-3.5 text-[#0369A1]" />
                        {t(`features.${vehicle.fuelType}`)}
                      </div>
                    </div>

                    <div className="pt-[var(--space-fluid-sm)] mt-auto border-t border-[#E2E8F0] flex items-center justify-between gap-[var(--space-fluid-xs)] overflow-hidden">
                      <div className="space-y-0.5 whitespace-nowrap min-w-0">
                        <div className="flex items-baseline gap-1 truncate">
                          <span className="text-[length:var(--text-fluid-xl)] font-bold text-[#0F172A] tracking-tight">
                            ₺ {vehicle.dailyRate}
                          </span>
                          <span className="text-[length:var(--text-fluid-sm)] text-[#64748B]">
                            /{t("pricePerDay")}
                          </span>
                        </div>
                      </div>

                      {vehicle.available ? (
                        <Link
                          href={{ pathname: "/vehicles/[id]", params: { id: vehicle.id } }}
                          className={cn(
                            "px-[var(--space-fluid-sm)] py-[var(--space-fluid-xs)] rounded-xl text-[length:var(--text-fluid-sm)] font-bold whitespace-nowrap shrink-0",
                            "transition-all duration-200",
                            "focus:outline-none focus:ring-2 focus:ring-offset-2",
                            "text-white bg-[#0369A1]",
                            "hover:bg-[#0284C7] active:bg-[#075985]",
                            "cursor-pointer focus:ring-[#0369A1]",
                            "shadow-md hover:shadow-lg"
                          )}
                        >
                          {t("bookNow")}
                        </Link>
                      ) : (
                        <span
                          className={cn(
                            "px-[var(--space-fluid-sm)] py-[var(--space-fluid-xs)] rounded-xl text-[length:var(--text-fluid-sm)] font-bold whitespace-nowrap shrink-0",
                            "text-[#94A3B8] bg-[#F1F5F9]",
                            "cursor-not-allowed"
                          )}
                        >
                          {t("unavailable")}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>

            {totalPages > 1 && (
              <div className="flex items-center justify-center gap-2 mt-8">
                <button
                  type="button"
                  onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                  disabled={currentPage === 1}
                  className="px-4 py-2 bg-white border border-slate-200 rounded-lg text-sm font-medium text-slate-700 disabled:opacity-50 hover:bg-slate-50 transition-colors"
                >
                  {tCommon("buttons.back")}
                </button>
                <div className="flex items-center gap-1">
                  {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                    <button
                      key={page}
                      type="button"
                      onClick={() => setCurrentPage(page)}
                      className={cn(
                        "w-10 h-10 rounded-lg text-sm font-medium transition-colors",
                        currentPage === page
                          ? "bg-[#0369A1] text-white shadow-md border-transparent"
                          : "bg-white border border-slate-200 text-slate-700 hover:bg-slate-50"
                      )}
                    >
                      {page}
                    </button>
                  ))}
                </div>
                <button
                  type="button"
                  onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                  disabled={currentPage === totalPages}
                  className="px-4 py-2 bg-white border border-slate-200 rounded-lg text-sm font-medium text-slate-700 disabled:opacity-50 hover:bg-slate-50 transition-colors"
                >
                  {tCommon("buttons.next")}
                </button>
              </div>
            )}
          </div>
        </div>
      </main>
    </div>
  );
}
