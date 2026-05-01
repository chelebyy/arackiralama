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
  Gauge,
  Snowflake,
  Shield,
  Check,
  ChevronLeft,
  ChevronRight,
  Star,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useAvailableVehicles, useOffices } from "@/hooks/useVehicles";
import type { AvailableVehicleGroup } from "@/lib/api/types";

interface VehicleDetail {
  id: string;
  name: string;
  group: string;
  images: string[];
  passengers: number;
  luggage: number;
  transmission: string;
  fuelType: string;
  dailyRate: number;
  features: string[];
  specifications: { engine: string; power: string; doors: number; minAge: number; license: string };
  description: string;
  rating: number;
  reviews: number;
}

function mapAvailableToDetail(group: AvailableVehicleGroup): VehicleDetail {
  return {
    id: group.groupId,
    name: group.groupNameEn || group.groupName,
    group: group.groupNameEn || group.groupName,
    images: group.imageUrl ? [group.imageUrl] : [],
    passengers: 5,
    luggage: 2,
    transmission: "Automatic",
    fuelType: "Gasoline",
    dailyRate: group.dailyPrice,
    features: group.features,
    specifications: {
      engine: "2.0L",
      power: "150 HP",
      doors: 4,
      minAge: group.minAge,
      license: "B",
    },
    description: `The ${group.groupNameEn || group.groupName} is a perfect choice for exploring Alanya and its surroundings.`,
    rating: 4.5,
    reviews: 0,
  };
}

const fallbackVehicle: VehicleDetail = {
  id: "",
  name: "Vehicle",
  group: "",
  images: [],
  passengers: 5,
  luggage: 2,
  transmission: "Automatic",
  fuelType: "Gasoline",
  dailyRate: 0,
  features: [],
  specifications: { engine: "2.0L", power: "150 HP", doors: 4, minAge: 21, license: "B" },
  description: "",
  rating: 4.5,
  reviews: 0,
};

const offices = [
  { id: "ala", name: "Alanya City Center" },
  { id: "gzp", name: "Gazipasa Airport" },
  { id: "ayt", name: "Antalya Airport" },
  { id: "mahmutlar", name: "Mahmutlar" },
  { id: "kargicak", name: "Kargicak" },
  { id: "konakli", name: "Konakli" },
  { id: "avsallar", name: "Avsallar" },
];

const officeSlugPatterns: Record<string, string> = {
  ala: "alanya",
  gzp: "gazipasa",
  ayt: "antalya",
  mahmutlar: "mahmutlar",
  kargicak: "kargicak",
  konakli: "konakli",
  avsallar: "avsallar",
};

function isGuid(value: string): boolean {
  return /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(value);
}

function resolveOfficeGuid(offices: { id: string; name: string }[], slugOrGuid: string): string {
  if (isGuid(slugOrGuid)) return slugOrGuid;
  const pattern = officeSlugPatterns[slugOrGuid.toLowerCase()];
  if (!pattern) return slugOrGuid;
  const matched = offices.find((o) => o.name.toLowerCase().includes(pattern));
  return matched?.id ?? slugOrGuid;
}

export default function VehicleDetailPage() {
  const params = useParams();
  const searchParams = useSearchParams();
  const locale = params.locale as string;
  const [currentImage, setCurrentImage] = useState(0);

  const pickupOffice = searchParams.get("pickup") || "ala";
  const returnOffice = searchParams.get("return") || "ala";
  const pickupDate = searchParams.get("pickupDate") || "2025-04-01";
  const pickupTime = searchParams.get("pickupTime") || "10:00";
  const returnDate = searchParams.get("returnDate") || "2025-04-08";
  const returnTime = searchParams.get("returnTime") || "09:00";

  const { offices: apiOffices } = useOffices();
  const pickupOfficeGuid = resolveOfficeGuid(apiOffices, pickupOffice);

  const { vehicles: availableGroups, isLoading, isError } = useAvailableVehicles(
    pickupDate && pickupTime && returnDate && returnTime && pickupOfficeGuid
      ? {
          office_id: pickupOfficeGuid,
          pickup_datetime: `${pickupDate}T${pickupTime}`,
          return_datetime: `${returnDate}T${returnTime}`,
        }
      : null
  );

  const matchedGroup = availableGroups.find((g) => g.groupId === params.id);
  const vehicle: VehicleDetail = matchedGroup
    ? mapAvailableToDetail(matchedGroup)
    : fallbackVehicle;

  const getDays = (start: string, end: string) => {
    const s = new Date(start);
    const e = new Date(end);
    const diff = Math.ceil((e.getTime() - s.getTime()) / (1000 * 60 * 60 * 24));
    return Math.max(1, diff);
  };

  const days = getDays(pickupDate, returnDate);
  const totalPrice = vehicle.dailyRate * days;

  return (
    <div className="min-h-screen bg-slate-50">
      <main className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-8">
        <Link
          href={`/${locale}/vehicles?pickup=${pickupOffice}&return=${returnOffice}&pickupDate=${pickupDate}&returnDate=${returnDate}`}
          className="inline-flex items-center gap-2 text-sm text-slate-600 hover:text-sky-700 transition-colors mb-6"
        >
          <ChevronLeft className="h-4 w-4" />
          Back to search
        </Link>

        {isLoading && (
          <div className="text-center py-12">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-sky-600 mx-auto" />
            <p className="mt-4 text-slate-600">Loading vehicle details...</p>
          </div>
        )}

        {isError && (
          <div className="text-center py-12">
            <Car className="h-12 w-12 text-red-400 mx-auto" />
            <p className="mt-4 text-slate-600">Failed to load vehicle details. Please try again.</p>
          </div>
        )}

        {!isLoading && !isError && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          <div className="lg:col-span-2 space-y-6">
            <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
              <div className="relative bg-slate-100 h-80 lg:h-96 flex items-center justify-center">
                <Car className="h-32 w-32 text-slate-300" />
                <button
                  type="button"
                  onClick={() => setCurrentImage((p) => (p > 0 ? p - 1 : 2))}
                  className="absolute left-4 p-2 bg-white/90 rounded-full shadow-lg hover:bg-white"
                  aria-label="Previous image"
                >
                  <ChevronLeft className="h-5 w-5 text-slate-700" />
                </button>
                <button
                  type="button"
                  onClick={() => setCurrentImage((p) => (p < 2 ? p + 1 : 0))}
                  className="absolute right-4 p-2 bg-white/90 rounded-full shadow-lg hover:bg-white"
                  aria-label="Next image"
                >
                  <ChevronRight className="h-5 w-5 text-slate-700" />
                </button>
                <div className="absolute bottom-4 left-1/2 -translate-x-1/2 flex gap-2">
                  {[0, 1, 2].map((i) => (
                    <button
                      key={i}
                      type="button"
                      onClick={() => setCurrentImage(i)}
                      className={cn(
                        "w-2.5 h-2.5 rounded-full transition-colors",
                        currentImage === i ? "bg-sky-600" : "bg-white/70"
                      )}
                      aria-label={`View image ${i + 1}`}
                    />
                  ))}
                </div>
              </div>
            </div>

            <div className="bg-white rounded-xl border border-slate-200 p-6">
              <div className="flex items-start justify-between mb-4">
                <div>
                  <span className="text-sm font-medium text-sky-700 bg-sky-50 px-3 py-1 rounded-full">
                    {vehicle.group}
                  </span>
                  <h1
                    className="text-3xl font-bold text-slate-900 mt-3"
                    style={{ fontFamily: "Lexend, sans-serif" }}
                  >
                    {vehicle.name}
                  </h1>
                  <div className="flex items-center gap-2 mt-2">
                    <Star className="h-5 w-5 text-yellow-400 fill-yellow-400" />
                    <span className="font-semibold text-slate-900">{vehicle.rating}</span>
                    <span className="text-slate-500">({vehicle.reviews} reviews)</span>
                  </div>
                </div>
              </div>

              <p className="text-slate-600 leading-relaxed">{vehicle.description}</p>
            </div>

            <div className="bg-white rounded-xl border border-slate-200 p-6">
              <h2
                className="text-xl font-semibold text-slate-900 mb-4"
                style={{ fontFamily: "Lexend, sans-serif" }}
              >
                Features
              </h2>
              <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                {vehicle.features.map((feature) => (
                  <div
                    key={feature}
                    className="flex items-center gap-2 p-3 bg-slate-50 rounded-lg"
                  >
                    <Check className="h-4 w-4 text-sky-600 flex-shrink-0" />
                    <span className="text-sm text-slate-700">{feature}</span>
                  </div>
                ))}
              </div>
            </div>

            <div className="bg-white rounded-xl border border-slate-200 p-6">
              <h2
                className="text-xl font-semibold text-slate-900 mb-4"
                style={{ fontFamily: "Lexend, sans-serif" }}
              >
                Technical Specifications
              </h2>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                {[
                  { label: "Engine", value: vehicle.specifications.engine, icon: Gauge },
                  { label: "Power", value: vehicle.specifications.power, icon: Fuel },
                  { label: "Doors", value: vehicle.specifications.doors, icon: Car },
                  { label: "Min. Age", value: vehicle.specifications.minAge, icon: Users },
                ].map(({ label, value, icon: Icon }) => (
                  <div key={label} className="text-center p-4 bg-slate-50 rounded-lg">
                    <Icon className="h-6 w-6 text-slate-400 mx-auto mb-2" />
                    <p className="text-xs text-slate-500 uppercase tracking-wide">{label}</p>
                    <p className="text-lg font-semibold text-slate-900">{value}</p>
                  </div>
                ))}
              </div>
            </div>
          </div>

          <div className="space-y-6">
            <div className="bg-white rounded-xl border border-slate-200 p-6 sticky top-24">
              <div className="text-center pb-6 border-b border-slate-200">
                <p className="text-sm text-slate-500">Total for {days} days</p>
                <p className="text-4xl font-bold text-sky-700">₺{totalPrice}</p>
                <p className="text-sm text-slate-500">₺{vehicle.dailyRate} per day</p>
              </div>

              <div className="py-6 space-y-4">
                <div className="flex items-center gap-3 text-sm">
                  <Calendar className="h-4 w-4 text-sky-600" />
                  <div>
                    <p className="font-medium text-slate-900">{pickupDate}</p>
                    <p className="text-slate-500">{offices.find((o) => o.id === pickupOffice)?.name}</p>
                  </div>
                </div>
                <div className="flex items-center gap-3 text-sm">
                  <Calendar className="h-4 w-4 text-sky-600" />
                  <div>
                    <p className="font-medium text-slate-900">{returnDate}</p>
                    <p className="text-slate-500">{offices.find((o) => o.id === returnOffice)?.name}</p>
                  </div>
                </div>
              </div>

              <Link
                href={`/${locale}/booking/step2?vehicle=${vehicle.id}&pickup=${pickupOffice}&return=${returnOffice}&pickupDate=${pickupDate}&returnDate=${returnDate}`}
                className="block w-full py-4 bg-sky-700 text-white text-center font-semibold rounded-lg hover:bg-sky-800 transition-colors"
              >
                Book Now
              </Link>

              <div className="mt-6 p-4 bg-sky-50 rounded-lg">
                <div className="flex items-start gap-3">
                  <Shield className="h-5 w-5 text-sky-600 mt-0.5 flex-shrink-0" />
                  <div>
                    <p className="text-sm font-medium text-sky-900">Full Protection</p>
                    <ul className="mt-2 space-y-1 text-xs text-sky-800">
                      {["Zero excess", "Theft protection", "24/7 assistance"].map((item) => (
                        <li key={item} className="flex items-center gap-1">
                          <Check className="h-3 w-3" /> {item}
                        </li>
                      ))}
                    </ul>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
        )}
      </main>
    </div>
  );
}
