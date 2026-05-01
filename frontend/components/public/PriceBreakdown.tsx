"use client";

import { cn } from "@/lib/utils";
import { Check, Tag, Percent, Shield } from "lucide-react";

interface PriceBreakdownProps {
  dailyRate: number;
  days: number;
  vehicleGroup: string;
  extras?: Array<{
    name: string;
    price: number;
  }>;
  campaignDiscount?: number;
  currency?: string;
}

export function PriceBreakdown({
  dailyRate,
  days,
  vehicleGroup,
  extras = [],
  campaignDiscount = 0,
  currency = "TRY",
}: PriceBreakdownProps) {
  const subtotal = dailyRate * days;
  const extrasTotal = extras.reduce((sum, extra) => sum + extra.price, 0);
  const discountAmount = (subtotal + extrasTotal) * (campaignDiscount / 100);
  const total = subtotal + extrasTotal - discountAmount;

  const formatPrice = (amount: number) => {
    return new Intl.NumberFormat("en-US", {
      style: "currency",
      currency,
    }).format(amount);
  };

  return (
    <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
      <div className="px-6 py-4 border-b border-slate-200 bg-slate-50/50">
        <h3 className="text-lg font-semibold text-slate-900" style={{ fontFamily: "Lexend, sans-serif" }}>
          Price Breakdown
        </h3>
      </div>

      <div className="p-6 space-y-4">
        <div className="flex items-center justify-between">
          <span className="text-slate-600">{vehicleGroup}</span>
          <span className="text-slate-900 font-medium">{dailyRate} {currency}/day</span>
        </div>

        <div className="flex items-center justify-between">
          <span className="text-slate-600">Rental period</span>
          <span className="text-slate-900 font-medium">{days} days</span>
        </div>

        <div className="flex items-center justify-between pt-2 border-t border-slate-100">
          <span className="text-slate-700 font-medium">Subtotal</span>
          <span className="text-slate-900 font-semibold">{formatPrice(subtotal)}</span>
        </div>

        {extras.length > 0 && (
          <>
            <div className="pt-3 border-t border-slate-100">
              <p className="text-sm font-medium text-slate-700 mb-3 flex items-center gap-2">
                <Tag className="h-4 w-4 text-slate-400" />
                Additional Options
              </p>
              <div className="space-y-2">
                {extras.map((extra) => (
                  <div key={extra.name} className="flex items-center justify-between text-sm">
                    <span className="text-slate-600">{extra.name}</span>
                    <span className="text-slate-900">{formatPrice(extra.price)}</span>
                  </div>
                ))}
              </div>
            </div>
            <div className="flex items-center justify-between pt-2 border-t border-slate-100">
              <span className="text-slate-700 font-medium">Extras Total</span>
              <span className="text-slate-900 font-semibold">{formatPrice(extrasTotal)}</span>
            </div>
          </>
        )}

        {campaignDiscount > 0 && (
          <div className="flex items-center justify-between pt-3 border-t border-slate-100">
            <span className="text-green-700 font-medium flex items-center gap-2">
              <Percent className="h-4 w-4" />
              Campaign Discount ({campaignDiscount}%)
            </span>
            <span className="text-green-700 font-semibold">-{formatPrice(discountAmount)}</span>
          </div>
        )}

        <div className="flex items-center justify-between pt-4 border-t-2 border-slate-200">
          <span className="text-lg font-semibold text-slate-900">Total</span>
          <span className="text-2xl font-bold text-sky-700">{formatPrice(total)}</span>
        </div>

        <div className="pt-4 border-t border-slate-100">
          <div className="flex items-start gap-3 p-3 bg-sky-50 rounded-lg">
            <Shield className="h-5 w-5 text-sky-600 mt-0.5 flex-shrink-0" />
            <div>
              <p className="text-sm font-medium text-sky-900">What's included</p>
              <ul className="mt-2 space-y-1">
                {[
                  "Full insurance with zero excess",
                  "24/7 roadside assistance",
                  "Free cancellation up to 48h",
                  "Unlimited mileage",
                ].map((item) => (
                  <li key={item} className="text-xs text-sky-800 flex items-center gap-1.5">
                    <Check className="h-3 w-3 flex-shrink-0" />
                    {item}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
