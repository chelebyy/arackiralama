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

const mockVehicles = [
  {
    id: "1",
    name: "Fiat Egea",
    group: "Economy",
    images: ["/images/vehicles/fiat-egea-1.png", "/images/vehicles/fiat-egea-2.png", "/images/vehicles/fiat-egea-3.png"],
    passengers: 5,
    luggage: 2,
    transmission: "Automatic",
    fuelType: "Gasoline",
    dailyRate: 45,
    features: ["Air Conditioning", "Bluetooth", "GPS Navigation", "USB Port", "Rear Camera"],
    specifications: { engine: "1.6L", power: "110 HP", doors: 4, minAge: 21, license: "B" },
    description:
      "The Fiat Egea is a perfect choice for exploring Alanya and its surroundings. Compact yet spacious, fuel-efficient, and easy to drive in city traffic. Ideal for couples or small families.",
    rating: 4.8,
    reviews: 124,
  },
  {
    id: "2",
    name: "Renault Megane",
    group: "Compact",
    images: ["/images/vehicles/renault-megane-1.png", "/images/vehicles/renault-megane-2.png", "/images/vehicles/renault-megane-3.png"],
    passengers: 5,
    luggage: 3,
    transmission: "Automatic",
    fuelType: "Diesel",
    dailyRate: 55,
    features: ["Air Conditioning", "Cruise Control", "Parking Sensors", "Bluetooth", "GPS Navigation"],
    specifications: { engine: "1.5L dCi", power: "115 HP", doors: 4, minAge: 21, license: "B" },
    description:
      "The Renault Megane offers a comfortable ride with ample space for luggage. Great for longer trips along the Mediterranean coast.",
    rating: 4.7,
    reviews: 98,
  },
  {
    id: "3",
    name: "VW Passat",
    group: "Midsize",
    images: ["/images/vehicles/vw-passat-1.png", "/images/vehicles/vw-passat-2.png", "/images/vehicles/vw-passat-3.png"],
    passengers: 5,
    luggage: 3,
    transmission: "Automatic",
    fuelType: "Diesel",
    dailyRate: 75,
    features: ["Leather Seats", "Sunroof", "Navigation", "Adaptive Cruise Control", "Parking Assistant"],
    specifications: { engine: "2.0L TDI", power: "150 HP", doors: 4, minAge: 23, license: "B" },
    description:
      "The VW Passat is a premium midsize sedan offering superior comfort and advanced technology. Perfect for business trips or family holidays.",
    rating: 4.9,
    reviews: 156,
  },
  {
    id: "4",
    name: "BMW 3 Series",
    group: "Premium",
    images: ["/images/vehicles/bmw-3-1.png", "/images/vehicles/bmw-3-2.png", "/images/vehicles/bmw-3-3.png"],
    passengers: 5,
    luggage: 2,
    transmission: "Automatic",
    fuelType: "Gasoline",
    dailyRate: 95,
    features: ["Leather Seats", "Premium Sound", "Parking Assistant", "Head-Up Display", "Wireless Charging"],
    specifications: { engine: "2.0L Turbo", power: "184 HP", doors: 4, minAge: 25, license: "B" },
    description:
      "Experience driving pleasure with the BMW 3 Series. Sporty handling, luxurious interior, and cutting-edge technology.",
    rating: 4.9,
    reviews: 203,
  },
  {
    id: "5",
    name: "Mercedes Vito",
    group: "Minivan",
    images: ["/images/vehicles/mercedes-vito-1.png", "/images/vehicles/mercedes-vito-2.png", "/images/vehicles/mercedes-vito-3.png"],
    passengers: 9,
    luggage: 5,
    transmission: "Automatic",
    fuelType: "Diesel",
    dailyRate: 120,
    features: ["Extra Space", "Dual A/C", "Rear Camera", "Leather Seats", "Sliding Doors"],
    specifications: { engine: "2.1L CDI", power: "163 HP", doors: 4, minAge: 25, license: "B" },
    description:
      "The Mercedes Vito is the ultimate group travel solution. Spacious, comfortable, and equipped with everything you need for large families or tour groups.",
    rating: 4.8,
    reviews: 87,
  },
  {
    id: "6",
    name: "Audi Q5",
    group: "SUV",
    images: ["/images/vehicles/audi-q5-1.png", "/images/vehicles/audi-q5-2.png", "/images/vehicles/audi-q5-3.png"],
    passengers: 5,
    luggage: 4,
    transmission: "Automatic",
    fuelType: "Diesel",
    dailyRate: 110,
    features: ["4WD", "Panoramic Roof", "Virtual Cockpit", "Adaptive Air Suspension", "Matrix LED"],
    specifications: { engine: "2.0L TDI", power: "190 HP", doors: 5, minAge: 25, license: "B" },
    description:
      "The Audi Q5 combines SUV practicality with premium refinement. All-wheel drive makes it ideal for exploring mountain roads around Alanya.",
    rating: 4.9,
    reviews: 142,
  },
];

const offices = [
  { id: "ala", name: "Alanya City Center" },
  { id: "gzp", name: "Gazipasa Airport" },
  { id: "ayt", name: "Antalya Airport" },
];

export default function VehicleDetailPage() {
  const params = useParams();
  const searchParams = useSearchParams();
  const locale = params.locale as string;
  const [currentImage, setCurrentImage] = useState(0);

  const pickupOffice = searchParams.get("pickup") || "ala";
  const returnOffice = searchParams.get("return") || "ala";
  const pickupDate = searchParams.get("pickupDate") || "2025-04-01";
  const returnDate = searchParams.get("returnDate") || "2025-04-08";

  const vehicle = mockVehicles.find((v) => v.id === params.id) || mockVehicles[0];

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
                <p className="text-4xl font-bold text-sky-700">€{totalPrice}</p>
                <p className="text-sm text-slate-500">€{vehicle.dailyRate} per day</p>
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
      </main>
    </div>
  );
}
