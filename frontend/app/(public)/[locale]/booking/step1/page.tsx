"use client";

import { useParams, useSearchParams, useRouter } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import * as z from "zod";
import { Calendar, MapPin, Clock, ChevronRight, ArrowRight } from "lucide-react";
import { useState } from "react";

const step1Schema = z.object({
  pickupOffice: z.string().min(1, "Pickup office is required"),
  returnOffice: z.string().min(1, "Return office is required"),
  pickupDate: z.string().min(1, "Pickup date is required"),
  pickupTime: z.string().min(1, "Pickup time is required"),
  returnDate: z.string().min(1, "Return date is required"),
  returnTime: z.string().min(1, "Return time is required"),
});

type Step1FormData = z.infer<typeof step1Schema>;

const offices = [
  { id: "ala", name: "Alanya City Center", address: "Ataturk Blvd. No:123, Alanya" },
  { id: "gzp", name: "Gazipasa Airport", address: "Gazipasa Airport Terminal" },
  { id: "ayt", name: "Antalya Airport", address: "Antalya Airport Terminal 2" },
  { id: "mahmutlar", name: "Mahmutlar", address: "Mahmutlar, Alanya" },
  { id: "kargicak", name: "Kargicak", address: "Kargicak, Alanya" },
  { id: "konakli", name: "Konakli", address: "Konakli, Alanya" },
  { id: "avsallar", name: "Avsallar", address: "Avsallar, Alanya" },
];

const timeSlots = [
  "08:00", "08:30", "09:00", "09:30", "10:00", "10:30",
  "11:00", "11:30", "12:00", "12:30", "13:00", "13:30",
  "14:00", "14:30", "15:00", "15:30", "16:00", "16:30",
  "17:00", "17:30", "18:00", "18:30", "19:00", "19:30",
  "20:00", "20:30", "21:00", "21:30", "22:00",
];

export default function BookingStep1Page() {
  const params = useParams();
  const searchParams = useSearchParams();
  const router = useRouter();
  const locale = params.locale as string;
  const [sameOffice, setSameOffice] = useState(true);

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<Step1FormData>({
    resolver: zodResolver(step1Schema),
    defaultValues: {
      pickupOffice: searchParams.get("pickup") || "ala",
      returnOffice: searchParams.get("return") || "ala",
      pickupDate: searchParams.get("pickupDate") || "",
      pickupTime: "10:00",
      returnDate: searchParams.get("returnDate") || "",
      returnTime: "10:00",
    },
  });

  const onSubmit = (data: Step1FormData) => {
    const queryParams = new URLSearchParams({
      pickup: data.pickupOffice,
      return: sameOffice ? data.pickupOffice : data.returnOffice,
      pickupDate: data.pickupDate,
      pickupTime: data.pickupTime,
      returnDate: data.returnDate,
      returnTime: data.returnTime,
    });
    router.push(`/${locale}/booking/step2?${queryParams.toString()}`);
  };

  return (
    <div className="max-w-4xl mx-auto">
      <div className="mb-8">
        <h1
          className="text-3xl font-bold text-slate-900 mb-2"
          style={{ fontFamily: "Lexend, sans-serif" }}
        >
          Select Dates & Location
        </h1>
        <p className="text-slate-600">Choose where and when you want to pick up and return your vehicle.</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        <div className="bg-white rounded-xl border border-slate-200 p-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="w-10 h-10 bg-sky-100 rounded-lg flex items-center justify-center">
              <MapPin className="h-5 w-5 text-sky-600" />
            </div>
            <h2 className="text-xl font-semibold text-slate-900" style={{ fontFamily: "Lexend, sans-serif" }}>
              Pickup Location
            </h2>
          </div>

          <div className="space-y-4">
            <div>
              <label htmlFor="pickupOffice" className="block text-sm font-medium text-slate-700 mb-2">
                Office Location
              </label>
              <select
                id="pickupOffice"
                {...register("pickupOffice")}
                className="w-full px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500 bg-white"
              >
                {offices.map((office) => (
                  <option key={office.id} value={office.id}>
                    {office.name}
                  </option>
                ))}
              </select>
              {errors.pickupOffice && (
                <p className="mt-1 text-sm text-red-600">{errors.pickupOffice.message}</p>
              )}
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label htmlFor="pickupDate" className="block text-sm font-medium text-slate-700 mb-2">
                  Pickup Date
                </label>
                <div className="relative">
                  <Calendar className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                  <input
                    type="date"
                    id="pickupDate"
                    {...register("pickupDate")}
                    className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                  />
                </div>
                {errors.pickupDate && (
                  <p className="mt-1 text-sm text-red-600">{errors.pickupDate.message}</p>
                )}
              </div>

              <div>
                <label htmlFor="pickupTime" className="block text-sm font-medium text-slate-700 mb-2">
                  Pickup Time
                </label>
                <div className="relative">
                  <Clock className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                  <select
                    id="pickupTime"
                    {...register("pickupTime")}
                    className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500 bg-white"
                  >
                    {timeSlots.map((time) => (
                      <option key={time} value={time}>{time}</option>
                    ))}
                  </select>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 p-6">
          <div className="flex items-center justify-between mb-6">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 bg-sky-100 rounded-lg flex items-center justify-center">
                <MapPin className="h-5 w-5 text-sky-600" />
              </div>
              <h2 className="text-xl font-semibold text-slate-900" style={{ fontFamily: "Lexend, sans-serif" }}>
                Return Location
              </h2>
            </div>
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={sameOffice}
                onChange={(e) => setSameOffice(e.target.checked)}
                className="w-4 h-4 text-sky-600 border-slate-300 rounded focus:ring-sky-500"
              />
              <span className="text-sm text-slate-600">Same as pickup</span>
            </label>
          </div>

          <div className="space-y-4">
            {!sameOffice && (
              <div>
                <label htmlFor="returnOffice" className="block text-sm font-medium text-slate-700 mb-2">
                  Return Office
                </label>
                <select
                  id="returnOffice"
                  {...register("returnOffice")}
                  className="w-full px-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500 bg-white"
                >
                  {offices.map((office) => (
                    <option key={office.id} value={office.id}>
                      {office.name}
                    </option>
                  ))}
                </select>
                {errors.returnOffice && (
                  <p className="mt-1 text-sm text-red-600">{errors.returnOffice.message}</p>
                )}
              </div>
            )}

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label htmlFor="returnDate" className="block text-sm font-medium text-slate-700 mb-2">
                  Return Date
                </label>
                <div className="relative">
                  <Calendar className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                  <input
                    type="date"
                    id="returnDate"
                    {...register("returnDate")}
                    className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500"
                  />
                </div>
                {errors.returnDate && (
                  <p className="mt-1 text-sm text-red-600">{errors.returnDate.message}</p>
                )}
              </div>

              <div>
                <label htmlFor="returnTime" className="block text-sm font-medium text-slate-700 mb-2">
                  Return Time
                </label>
                <div className="relative">
                  <Clock className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-slate-400" />
                  <select
                    id="returnTime"
                    {...register("returnTime")}
                    className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg focus:ring-2 focus:ring-sky-500 focus:border-sky-500 bg-white"
                  >
                    {timeSlots.map((time) => (
                      <option key={time} value={time}>{time}</option>
                    ))}
                  </select>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="flex justify-end">
          <button
            type="submit"
            className="inline-flex items-center gap-2 px-8 py-4 bg-sky-700 text-white font-semibold rounded-lg hover:bg-sky-800 transition-colors"
          >
            Continue to Vehicle Selection
            <ArrowRight className="h-5 w-5" />
          </button>
        </div>
      </form>
    </div>
  );
}
