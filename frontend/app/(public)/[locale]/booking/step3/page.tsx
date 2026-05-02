"use client";

import { useParams, useSearchParams, useRouter } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import {
  User,
  Mail,
  Phone,
  CreditCard,
  Baby,
  Users,
  Shield,
  ArrowRight,
  ArrowLeft,
  Info,
  Check,
} from "lucide-react";
import Link from "next/link";
import { cn } from "@/lib/utils";

const step3Schema = z.object({
  firstName: z.string().min(2, "First name is required"),
  lastName: z.string().min(2, "Last name is required"),
  email: z.string().email("Invalid email address"),
  phone: z.string().min(10, "Valid phone number is required"),
  driverLicense: z.string().min(5, "Driver license number is required"),
  driverLicenseCountry: z.string().min(1, "Country is required"),
  birthDate: z.string().min(1, "Birth date is required"),
  specialRequests: z.string().optional(),
});

type Step3FormData = z.infer<typeof step3Schema>;

interface ExtraOption {
  id: string;
  name: string;
  description: string;
  price: number;
  priceType: "per_day" | "per_rental";
  icon: React.ReactNode;
}

const extraOptions: ExtraOption[] = [
  {
    id: "child_seat",
    name: "Child Safety Seat",
    description: "Suitable for children 9-36kg",
    price: 10,
    priceType: "per_day",
    icon: <Baby className="h-5 w-5" />,
  },
  {
    id: "additional_driver",
    name: "Additional Driver",
    description: "Add a second authorized driver",
    price: 15,
    priceType: "per_rental",
    icon: <Users className="h-5 w-5" />,
  },
  {
    id: "gps",
    name: "GPS Navigation",
    description: "Latest maps of Turkey included",
    price: 8,
    priceType: "per_day",
    icon: <Shield className="h-5 w-5" />,
  },
  {
    id: "wifi",
    name: "Mobile WiFi",
    description: "Unlimited 4G internet access",
    price: 12,
    priceType: "per_day",
    icon: <Shield className="h-5 w-5" />,
  },
];

export default function BookingStep3Page() {
  const params = useParams();
  const searchParams = useSearchParams();
  const router = useRouter();
  const locale = params.locale as string;
  const [selectedExtras, setSelectedExtras] = useState<Set<string>>(new Set());

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<Step3FormData>({
    resolver: zodResolver(step3Schema),
  });

  const toggleExtra = (id: string) => {
    setSelectedExtras((prev) => {
      const newSet = new Set(prev);
      if (newSet.has(id)) {
        newSet.delete(id);
      } else {
        newSet.add(id);
      }
      return newSet;
    });
  };

  const onSubmit = (data: Step3FormData) => {
    const queryParams = new URLSearchParams(searchParams.toString());
    queryParams.set("extras", Array.from(selectedExtras).join(","));
    router.push(`/${locale}/booking/step4?${queryParams.toString()}`);
  };

  return (
    <div className="max-w-4xl mx-auto">
      <div className="mb-8">
        <h1
          className="text-3xl font-bold text-slate-900 mb-2"
          style={{ fontFamily: "Lexend, sans-serif" }}
        >
          Your Information
        </h1>
        <p className="text-slate-600">Please provide your details to complete the reservation.</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        <div className="bg-white rounded-xl border border-slate-200 p-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-10 h-10 bg-sky-100 rounded-lg flex items-center justify-center">
              <User className="h-5 w-5 text-sky-600" />
            </div>
            <h2 className="text-xl font-semibold text-slate-900" style={{ fontFamily: "Lexend, sans-serif" }}>
              Primary Driver
            </h2>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label htmlFor="firstName" className="block text-sm font-medium text-slate-700 mb-2">
                First Name
              </label>
              <input
                type="text"
                id="firstName"
                {...register("firstName")}
                className="w-full px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                placeholder="John"
              />
              {errors.firstName && (
                <p className="mt-1 text-sm text-red-600">{errors.firstName.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="lastName" className="block text-sm font-medium text-slate-700 mb-2">
                Last Name
              </label>
              <input
                type="text"
                id="lastName"
                {...register("lastName")}
                className="w-full px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                placeholder="Doe"
              />
              {errors.lastName && (
                <p className="mt-1 text-sm text-red-600">{errors.lastName.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="email" className="block text-sm font-medium text-slate-700 mb-2">
                Email Address
              </label>
              <div className="relative">
                <Mail className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                <input
                  type="email"
                  id="email"
                  {...register("email")}
                  className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                  placeholder="john.doe@example.com"
                />
              </div>
              {errors.email && (
                <p className="mt-1 text-sm text-red-600">{errors.email.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="phone" className="block text-sm font-medium text-slate-700 mb-2">
                Phone Number
              </label>
              <div className="relative">
                <Phone className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                <input
                  type="tel"
                  id="phone"
                  {...register("phone")}
                  className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                  placeholder="+90 555 123 4567"
                />
              </div>
              {errors.phone && (
                <p className="mt-1 text-sm text-red-600">{errors.phone.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="birthDate" className="block text-sm font-medium text-slate-700 mb-2">
                Date of Birth
              </label>
              <input
                type="date"
                id="birthDate"
                {...register("birthDate")}
                className="w-full px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
              />
              {errors.birthDate && (
                <p className="mt-1 text-sm text-red-600">{errors.birthDate.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="driverLicense" className="block text-sm font-medium text-slate-700 mb-2">
                Driver License Number
              </label>
              <div className="relative">
                <CreditCard className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                <input
                  type="text"
                  id="driverLicense"
                  {...register("driverLicense")}
                  className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                  placeholder="License number"
                />
              </div>
              {errors.driverLicense && (
                <p className="mt-1 text-sm text-red-600">{errors.driverLicense.message}</p>
              )}
            </div>

            <div>
              <label htmlFor="driverLicenseCountry" className="block text-sm font-medium text-slate-700 mb-2">
                License Issuing Country
              </label>
              <div className="relative">
                <CreditCard className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                <input
                  type="text"
                  id="driverLicenseCountry"
                  {...register("driverLicenseCountry")}
                  className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                  placeholder="e.g. Turkey, Germany, United Kingdom"
                />
              </div>
              {errors.driverLicenseCountry && (
                <p className="mt-1 text-sm text-red-600">{errors.driverLicenseCountry.message}</p>
              )}
            </div>
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 p-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-10 h-10 bg-sky-100 rounded-lg flex items-center justify-center">
              <Shield className="h-5 w-5 text-sky-600" />
            </div>
            <h2 className="text-xl font-semibold text-slate-900" style={{ fontFamily: "Lexend, sans-serif" }}>
              Additional Options
            </h2>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {extraOptions.map((option) => (
              <button
                key={option.id}
                type="button"
                onClick={() => toggleExtra(option.id)}
                className={cn(
                  "relative p-4 border-2 rounded-lg text-left transition-all duration-200",
                  selectedExtras.has(option.id)
                    ? "border-sky-600 bg-sky-50"
                    : "border-slate-200 hover:border-sky-300"
                )}
              >
                <div className="flex items-start gap-3">
                  <div
                    className={cn(
                      "w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0",
                      selectedExtras.has(option.id) ? "bg-sky-600 text-white" : "bg-slate-100 text-slate-600"
                    )}
                  >
                    {selectedExtras.has(option.id) ? <Check className="h-5 w-5" /> : option.icon}
                  </div>
                  <div className="flex-1">
                    <div className="flex items-center justify-between">
                      <h3 className="font-medium text-slate-900">{option.name}</h3>
                      <span className="font-semibold text-sky-700">
                        ₺{option.price}
                        <span className="text-xs text-slate-500 font-normal">/{option.priceType === "per_day" ? "day" : "rental"}</span>
                      </span>
                    </div>
                    <p className="text-sm text-slate-500 mt-1">{option.description}</p>
                  </div>
                </div>
              </button>
            ))}
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 p-6">
          <label htmlFor="specialRequests" className="block text-sm font-medium text-slate-700 mb-2">
            Special Requests (Optional)
          </label>
          <textarea
            id="specialRequests"
            {...register("specialRequests")}
            rows={3}
            className="w-full px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
            placeholder="Any special requirements or requests..."
          />
        </div>

        <div className="flex items-center justify-between">
          <Link
            href={`/${locale}/booking/step2?${searchParams.toString()}`}
            className="inline-flex items-center gap-2 px-6 py-3 text-slate-600 hover:text-slate-900 transition-colors"
          >
            <ArrowLeft className="h-5 w-5" />
            Back
          </Link>

          <button
            type="submit"
            className="inline-flex items-center gap-2 px-8 py-4 bg-sky-700 text-white font-semibold rounded-lg hover:bg-sky-800 transition-colors"
          >
            Continue to Payment
            <ArrowRight className="h-5 w-5" />
          </button>
        </div>
      </form>
    </div>
  );
}
