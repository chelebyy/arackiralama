"use client";

import { useParams, useSearchParams, useRouter } from "next/navigation";
import { useState } from "react";
import {
  Car,
  Users,
  Briefcase,
  Fuel,
  Gauge,
  Check,
  ArrowRight,
  ArrowLeft,
  Info,
  Star,
} from "lucide-react";
import Link from "next/link";
import { cn } from "@/lib/utils";
import { differenceInCalendarDays } from "date-fns";
import { useAvailableVehicles } from "@/hooks/useVehicles";
import type { AvailableVehicleGroup } from "@/lib/api/types";

interface VehicleGroup {
  id: string;
  name: string;
  category: string;
  dailyRate: number;
  passengers: number;
  luggage: number;
  transmission: string;
  fuelType: string;
  features: string[];
  image: string;
  rating: number;
  reviews: number;
}

function mapAvailableGroup(group: AvailableVehicleGroup): VehicleGroup {
  return {
    id: group.groupId,
    name: group.groupName,
    category: group.groupName,
    dailyRate: group.dailyPrice,
    passengers: 5,
    luggage: 2,
    transmission: "Automatic",
    fuelType: "Gasoline",
    features: group.features,
    image: group.imageUrl ?? "",
    rating: 4.5,
    reviews: 0,
  };
}

export default function BookingStep2Page() {
  const params = useParams();
  const searchParams = useSearchParams();
  const router = useRouter();
  const locale = params.locale as string;
  const [selectedVehicle, setSelectedVehicle] = useState<string | null>(null);

  const pickupOffice = searchParams.get("pickup") || "ala";
  const returnOffice = searchParams.get("return") || "ala";
  const pickupDate = searchParams.get("pickupDate") || "";
  const pickupTime = searchParams.get("pickupTime") || "";
  const returnDate = searchParams.get("returnDate") || "";
  const returnTime = searchParams.get("returnTime") || "";

  const { vehicles: availableGroups, isLoading, isError } = useAvailableVehicles(
    pickupDate && pickupTime && returnDate && returnTime
      ? {
          office_id: pickupOffice,
          pickup_datetime: `${pickupDate}T${pickupTime}`,
          return_datetime: `${returnDate}T${returnTime}`,
        }
      : null
  );

  const vehicleGroups = availableGroups.map(mapAvailableGroup);

  const handleContinue = () => {
    if (!selectedVehicle) return;
    const queryParams = new URLSearchParams(searchParams.toString());
    queryParams.set("vehicle", selectedVehicle);
    router.push(`/${locale}/booking/step3?${queryParams.toString()}`);
  };

  const days = Math.max(
    1,
    pickupDate && returnDate
      ? differenceInCalendarDays(new Date(returnDate), new Date(pickupDate))
      : 7
  );

  return (
    <div className="max-w-6xl mx-auto">
      <div className="mb-8">
        <h1
          className="text-3xl font-bold text-slate-900 mb-2"
          style={{ fontFamily: "Lexend, sans-serif" }}
        >
          Select Your Vehicle
        </h1>
        <p className="text-slate-600">Choose the vehicle group that best fits your needs.</p>
      </div>

      {isLoading && (
        <div className="text-center py-12">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-sky-600 mx-auto" />
          <p className="mt-4 text-slate-600">Loading available vehicles...</p>
        </div>
      )}

      {isError && (
        <div className="text-center py-12">
          <Info className="h-12 w-12 text-red-400 mx-auto" />
          <p className="mt-4 text-slate-600">Failed to load vehicles. Please try again.</p>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {vehicleGroups.map((vehicle) => (
          <button
            key={vehicle.id}
            type="button"
            onClick={() => setSelectedVehicle(vehicle.id)}
            className={cn(
              "relative text-left bg-white rounded-xl border-2 overflow-hidden transition-all duration-200",
              selectedVehicle === vehicle.id
                ? "border-sky-600 shadow-lg shadow-sky-100"
                : "border-slate-200 hover:border-sky-300 hover:shadow-md"
            )}
          >
            {selectedVehicle === vehicle.id && (
              <div className="absolute top-4 right-4 w-8 h-8 bg-sky-600 rounded-full flex items-center justify-center">
                <Check className="h-5 w-5 text-white" />
              </div>
            )}

            <div className="p-6">
              <div className="flex gap-6">
                <div className="w-32 h-24 bg-slate-100 rounded-lg flex items-center justify-center flex-shrink-0">
                  <Car className="h-12 w-12 text-slate-300" />
                </div>

                <div className="flex-1">
                  <div className="flex items-start justify-between mb-2">
                    <div>
                      <span className="text-xs font-medium text-sky-700 bg-sky-50 px-2 py-1 rounded-full">
                        {vehicle.category}
                      </span>
                      <h3
                        className="text-lg font-semibold text-slate-900 mt-1"
                        style={{ fontFamily: "Lexend, sans-serif" }}
                      >
                        {vehicle.name}
                      </h3>
                      <div className="flex items-center gap-1 mt-1">
                        <Star className="h-4 w-4 text-yellow-400 fill-yellow-400" />
                        <span className="text-sm font-medium text-slate-700">{vehicle.rating}</span>
                        <span className="text-sm text-slate-400">({vehicle.reviews} reviews)</span>
                      </div>
                    </div>
                    <div className="text-right">
                      <p className="text-2xl font-bold text-sky-700">₺{vehicle.dailyRate}</p>
                      <p className="text-sm text-slate-500">per day</p>
                    </div>
                  </div>

                  <div className="flex items-center gap-4 text-sm text-slate-600 mb-3">
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
                        className="text-xs text-slate-500 bg-slate-50 px-2 py-1 rounded"
                      >
                        {feature}
                      </span>
                    ))}
                  </div>

                  <div className="mt-4 pt-4 border-t border-slate-100">
                    <p className="text-sm text-slate-600">
                      Total for {days} days:{" "}
                      <span className="font-semibold text-slate-900">
                        ₺{vehicle.dailyRate * days}
                      </span>
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </button>
        ))}
      </div>

      <div className="flex items-center justify-between mt-8">
        <Link
          href={`/${locale}/booking/step1?${searchParams.toString()}`}
          className="inline-flex items-center gap-2 px-6 py-3 text-slate-600 hover:text-slate-900 transition-colors"
        >
          <ArrowLeft className="h-5 w-5" />
          Back
        </Link>

        <button
          type="button"
          onClick={handleContinue}
          disabled={!selectedVehicle}
          className={cn(
            "inline-flex items-center gap-2 px-8 py-4 font-semibold rounded-lg transition-colors",
            selectedVehicle
              ? "bg-sky-700 text-white hover:bg-sky-800"
              : "bg-slate-200 text-slate-400 cursor-not-allowed"
          )}
        >
          Continue
          <ArrowRight className="h-5 w-5" />
        </button>
      </div>
    </div>
  );
}
