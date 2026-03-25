"use client";

import { useState } from "react";
import { useParams, useSearchParams } from "next/navigation";
import Link from "next/link";
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
  Snowflake,
  X,
} from "lucide-react";
import { cn } from "@/lib/utils";

interface Vehicle {
  id: string;
  name: string;
  group: string;
  image: string;
  passengers: number;
  luggage: number;
  transmission: string;
  fuelType: string;
  dailyRate: number;
  features: string[];
  available: boolean;
}

const mockVehicles: Vehicle[] = [
  {
    id: "1",
    name: "Fiat Egea",
    group: "Economy",
    image: "/images/vehicles/fiat-egea.png",
    passengers: 5,
    luggage: 2,
    transmission: "Automatic",
    fuelType: "Gasoline",
    dailyRate: 45,
    features: ["A/C", "Bluetooth", "GPS"],
    available: true,
  },
  {
    id: "2",
    name: "Renault Megane",
    group: "Compact",
    image: "/images/vehicles/renault-megane.png",
    passengers: 5,
    luggage: 3,
    transmission: "Automatic",
    fuelType: "Diesel",
    dailyRate: 55,
    features: ["A/C", "Cruise Control", "Parking Sensors"],
    available: true,
  },
  {
    id: "3",
    name: "VW Passat",
    group: "Midsize",
    image: "/images/vehicles/vw-passat.png",
    passengers: 5,
    luggage: 3,
    transmission: "Automatic",
    fuelType: "Diesel",
    dailyRate: 75,
    features: ["Leather Seats", "Sunroof", "Navigation"],
    available: true,
  },
  {
    id: "4",
    name: "BMW 3 Series",
    group: "Premium",
    image: "/images/vehicles/bmw-3.png",
    passengers: 5,
    luggage: 2,
    transmission: "Automatic",
    fuelType: "Gasoline",
    dailyRate: 95,
    features: ["Leather Seats", "Premium Sound", "Parking Assistant"],
    available: true,
  },
  {
    id: "5",
    name: "Mercedes Vito",
    group: "Minivan",
    image: "/images/vehicles/mercedes-vito.png",
    passengers: 9,
    luggage: 5,
    transmission: "Automatic",
    fuelType: "Diesel",
    dailyRate: 120,
    features: ["Extra Space", "Dual A/C", "Rear Camera"],
    available: true,
  },
  {
    id: "6",
    name: "Audi Q5",
    group: "SUV",
    image: "/images/vehicles/audi-q5.png",
    passengers: 5,
    luggage: 4,
    transmission: "Automatic",
    fuelType: "Diesel",
    dailyRate: 110,
    features: ["4WD", "Panoramic Roof", "Virtual Cockpit"],
    available: true,
  },
];

const vehicleGroups = ["All", "Economy", "Compact", "Midsize", "Premium", "SUV", "Minivan"];

const offices = [
  { id: "ala", name: "Alanya City Center" },
  { id: "gzp", name: "Gazipasa Airport" },
  { id: "ayt", name: "Antalya Airport" },
];

export default function VehiclesPage() {
  const params = useParams();
  const searchParams = useSearchParams();
  const locale = params.locale as string;

  const [viewMode, setViewMode] = useState<"grid" | "list">("grid");
  const [selectedGroup, setSelectedGroup] = useState("All");
  const [mobileFiltersOpen, setMobileFiltersOpen] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);

  const pickupOffice = searchParams.get("pickup") || "ala";
  const returnOffice = searchParams.get("return") || "ala";
  const pickupDate = searchParams.get("pickupDate") || "2025-04-01";
  const returnDate = searchParams.get("returnDate") || "2025-04-08";

  const filteredVehicles =
    selectedGroup === "All"
      ? mockVehicles
      : mockVehicles.filter((v) => v.group === selectedGroup);

  const totalPages = Math.ceil(filteredVehicles.length / 6);

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-white border-b border-slate-200 sticky top-0 z-30">
        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-4">
          <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
            <div className="flex items-center gap-4">
              <Link
                href={`/${locale}`}
                className="text-2xl font-bold text-slate-900 hover:text-sky-700 transition-colors"
                style={{ fontFamily: "Lexend, sans-serif" }}
              >
                RentACar
              </Link>
            </div>

            <div className="flex items-center gap-4 bg-slate-50 px-4 py-3 rounded-lg border border-slate-200">
              <div className="flex items-center gap-2">
                <MapPin className="h-4 w-4 text-sky-600" />
                <span className="text-sm text-slate-700">
                  {offices.find((o) => o.id === pickupOffice)?.name}
                </span>
              </div>
              <ChevronRight className="h-4 w-4 text-slate-400" />
              <div className="flex items-center gap-2">
                <Calendar className="h-4 w-4 text-sky-600" />
                <span className="text-sm text-slate-700">
                  {pickupDate} → {returnDate}
                </span>
              </div>
            </div>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-8">
        <div className="flex flex-col lg:flex-row gap-8">
          <button
            type="button"
            onClick={() => setMobileFiltersOpen(true)}
            className="lg:hidden flex items-center justify-center gap-2 px-4 py-2 bg-white border border-slate-200 rounded-lg text-slate-700 hover:bg-slate-50"
          >
            <SlidersHorizontal className="h-4 w-4" />
            Filters
          </button>

          <aside
            className={cn(
              "fixed inset-0 z-40 lg:relative lg:inset-auto lg:block",
              mobileFiltersOpen ? "block" : "hidden lg:block"
            )}
          >
            <button
              type="button"
              className="absolute inset-0 bg-black/50 lg:hidden"
              onClick={() => setMobileFiltersOpen(false)}
              aria-label="Close filters"
            />
            <div className="absolute right-0 top-0 h-full w-80 bg-white p-6 lg:relative lg:w-64 lg:p-0 lg:bg-transparent">
              <div className="flex items-center justify-between lg:hidden mb-6">
                <h2 className="text-lg font-semibold text-slate-900">Filters</h2>
                <button
                  type="button"
                  onClick={() => setMobileFiltersOpen(false)}
                  className="p-2 hover:bg-slate-100 rounded-lg"
                  aria-label="Close filters"
                >
                  <X className="h-5 w-5 text-slate-500" />
                </button>
              </div>

              <div className="bg-white rounded-xl border border-slate-200 p-5">
                <h3 className="text-sm font-semibold text-slate-900 mb-4">Vehicle Group</h3>
                <div className="space-y-2">
                  {vehicleGroups.map((group) => (
                    <button
                      key={group}
                      type="button"
                      onClick={() => setSelectedGroup(group)}
                      className={cn(
                        "w-full text-left px-3 py-2 rounded-lg text-sm transition-colors",
                        selectedGroup === group
                          ? "bg-sky-50 text-sky-700 font-medium"
                          : "text-slate-600 hover:bg-slate-50"
                      )}
                    >
                      {group}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          </aside>

          <div className="flex-1">
            <div className="flex items-center justify-between mb-6">
              <p className="text-slate-600">
                <span className="font-semibold text-slate-900">{filteredVehicles.length}</span>{" "}
                vehicles available
              </p>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => setViewMode("grid")}
                  className={cn(
                    "p-2 rounded-lg transition-colors",
                    viewMode === "grid"
                      ? "bg-sky-100 text-sky-700"
                      : "text-slate-400 hover:text-slate-600"
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
                      ? "bg-sky-100 text-sky-700"
                      : "text-slate-400 hover:text-slate-600"
                  )}
                  aria-label="List view"
                >
                  <List className="h-5 w-5" />
                </button>
              </div>
            </div>

            <div
              className={cn(
                "grid gap-6",
                viewMode === "grid"
                  ? "grid-cols-1 md:grid-cols-2 xl:grid-cols-3"
                  : "grid-cols-1"
              )}
            >
              {filteredVehicles.map((vehicle) => (
                <Link
                  key={vehicle.id}
                  href={`/${locale}/vehicles/${vehicle.id}?pickup=${pickupOffice}&return=${returnOffice}&pickupDate=${pickupDate}&returnDate=${returnDate}`}
                  className={cn(
                    "group bg-white rounded-xl border border-slate-200 overflow-hidden hover:shadow-lg hover:border-sky-200 transition-all duration-300",
                    viewMode === "list" && "flex flex-col md:flex-row"
                  )}
                >
                  <div
                    className={cn(
                      "bg-slate-100 flex items-center justify-center",
                      viewMode === "list" ? "md:w-64 h-48 md:h-auto" : "h-48"
                    )}
                  >
                    <Car className="h-20 w-20 text-slate-300" />
                  </div>
                  <div className="p-5 flex-1">
                    <div className="flex items-start justify-between mb-3">
                      <div>
                        <span className="text-xs font-medium text-sky-700 bg-sky-50 px-2 py-1 rounded-full">
                          {vehicle.group}
                        </span>
                        <h3
                          className="text-lg font-semibold text-slate-900 mt-2"
                          style={{ fontFamily: "Lexend, sans-serif" }}
                        >
                          {vehicle.name}
                        </h3>
                      </div>
                      <div className="text-right">
                        <p className="text-2xl font-bold text-sky-700">
                          €{vehicle.dailyRate}
                        </p>
                        <p className="text-sm text-slate-500">per day</p>
                      </div>
                    </div>

                    <div className="flex items-center gap-4 text-sm text-slate-600 mb-4">
                      <span className="flex items-center gap-1">
                        <Users className="h-4 w-4" /> {vehicle.passengers}
                      </span>
                      <span className="flex items-center gap-1">
                        <Briefcase className="h-4 w-4" /> {vehicle.luggage}
                      </span>
                      <span className="flex items-center gap-1">
                        <Gauge className="h-4 w-4" /> {vehicle.transmission}
                      </span>
                      <span className="flex items-center gap-1">
                        <Fuel className="h-4 w-4" /> {vehicle.fuelType}
                      </span>
                    </div>

                    <div className="flex flex-wrap gap-2">
                      {vehicle.features.map((feature) => (
                        <span
                          key={feature}
                          className="inline-flex items-center gap-1 text-xs text-slate-500 bg-slate-50 px-2 py-1 rounded"
                        >
                          {feature === "A/C" && <Snowflake className="h-3 w-3" />}
                          {feature}
                        </span>
                      ))}
                    </div>
                  </div>
                </Link>
              ))}
            </div>

            {totalPages > 1 && (
              <div className="flex items-center justify-center gap-2 mt-8">
                <button
                  type="button"
                  onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                  disabled={currentPage === 1}
                  className="px-4 py-2 bg-white border border-slate-200 rounded-lg text-slate-700 disabled:opacity-50 hover:bg-slate-50"
                >
                  Previous
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
                          ? "bg-sky-600 text-white"
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
                  className="px-4 py-2 bg-white border border-slate-200 rounded-lg text-slate-700 disabled:opacity-50 hover:bg-slate-50"
                >
                  Next
                </button>
              </div>
            )}
          </div>
        </div>
      </main>
    </div>
  );
}
