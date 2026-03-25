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

const vehicleGroups: VehicleGroup[] = [
  {
    id: "economy",
    name: "Fiat Egea or similar",
    category: "Economy",
    dailyRate: 45,
    passengers: 5,
    luggage: 2,
    transmission: "Automatic",
    fuelType: "Gasoline",
    features: ["A/C", "Bluetooth", "GPS"],
    image: "/images/vehicles/economy.png",
    rating: 4.6,
    reviews: 234,
  },
  {
    id: "compact",
    name: "Renault Megane or similar",
    category: "Compact",
    dailyRate: 55,
    passengers: 5,
    luggage: 3,
    transmission: "Automatic",
    fuelType: "Diesel",
    features: ["A/C", "Cruise Control", "Parking Sensors"],
    image: "/images/vehicles/compact.png",
    rating: 4.7,
    reviews: 189,
  },
  {
    id: "midsize",
    name: "VW Passat or similar",
    category: "Midsize",
    dailyRate: 75,
    passengers: 5,
    luggage: 3,
    transmission: "Automatic",
    fuelType: "Diesel",
    features: ["Leather Seats", "Sunroof", "Navigation"],
    image: "/images/vehicles/midsize.png",
    rating: 4.8,
    reviews: 156,
  },
  {
    id: "premium",
    name: "BMW 3 Series or similar",
    category: "Premium",
    dailyRate: 95,
    passengers: 5,
    luggage: 2,
    transmission: "Automatic",
    fuelType: "Gasoline",
    features: ["Leather Seats", "Premium Sound", "Parking Assistant"],
    image: "/images/vehicles/premium.png",
    rating: 4.9,
    reviews: 98,
  },
  {
    id: "suv",
    name: "Audi Q5 or similar",
    category: "SUV",
    dailyRate: 110,
    passengers: 5,
    luggage: 4,
    transmission: "Automatic",
    fuelType: "Diesel",
    features: ["4WD", "Panoramic Roof", "Virtual Cockpit"],
    image: "/images/vehicles/suv.png",
    rating: 4.8,
    reviews: 112,
  },
  {
    id: "minivan",
    name: "Mercedes Vito or similar",
    category: "Minivan",
    dailyRate: 120,
    passengers: 9,
    luggage: 5,
    transmission: "Automatic",
    fuelType: "Diesel",
    features: ["Extra Space", "Dual A/C", "Rear Camera"],
    image: "/images/vehicles/minivan.png",
    rating: 4.7,
    reviews: 76,
  },
];

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

  const handleContinue = () => {
    if (!selectedVehicle) return;
    const queryParams = new URLSearchParams(searchParams.toString());
    queryParams.set("vehicle", selectedVehicle);
    router.push(`/${locale}/booking/step3?${queryParams.toString()}`);
  };

  const days = 7;

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
                      <p className="text-2xl font-bold text-sky-700">€{vehicle.dailyRate}</p>
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
                        €{vehicle.dailyRate * days}
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
