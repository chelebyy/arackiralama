"use client";

import { cn } from "@/lib/utils";
import { Calendar, Car, CreditCard, User } from "lucide-react";

interface BookingStepperProps {
  currentStep: 1 | 2 | 3 | 4;
}

const steps = [
  { id: 1, label: "Dates", icon: Calendar },
  { id: 2, label: "Vehicle", icon: Car },
  { id: 3, label: "Details", icon: User },
  { id: 4, label: "Payment", icon: CreditCard },
] as const;

export function BookingStepper({ currentStep }: BookingStepperProps) {
  return (
    <div className="w-full bg-white border-b border-slate-200">
      <div className="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8 py-6">
        <div className="flex items-center justify-between">
          {steps.map((step, index) => {
            const isActive = step.id === currentStep;
            const isCompleted = step.id < currentStep;
            const isLast = index === steps.length - 1;
            const Icon = step.icon;

            return (
              <div key={step.id} className="flex items-center flex-1">
                <div className="flex flex-col items-center">
                  <div
                    className={cn(
                      "flex h-12 w-12 items-center justify-center rounded-full border-2 transition-all duration-300",
                      isActive &&
                        "border-sky-600 bg-sky-600 text-white shadow-md shadow-sky-200",
                      isCompleted &&
                        "border-sky-600 bg-sky-600 text-white",
                      !isActive &&
                        !isCompleted &&
                        "border-slate-300 bg-white text-slate-400"
                    )}
                  >
                    <Icon className="h-5 w-5" />
                  </div>
                  <span
                    className={cn(
                      "mt-2 text-sm font-medium transition-colors",
                      isActive && "text-sky-700",
                      isCompleted && "text-sky-700",
                      !isActive && !isCompleted && "text-slate-400"
                    )}
                  >
                    {step.label}
                  </span>
                </div>
                {!isLast && (
                  <div
                    className={cn(
                      "h-1 flex-1 mx-4 rounded-full transition-colors duration-300",
                      isCompleted ? "bg-sky-600" : "bg-slate-200"
                    )}
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
