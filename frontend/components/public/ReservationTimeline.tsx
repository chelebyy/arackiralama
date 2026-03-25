"use client";

import { cn } from "@/lib/utils";
import {
  Clock,
  CheckCircle,
  XCircle,
  Car,
  Calendar,
  RotateCcw,
  AlertCircle
} from "lucide-react";

interface ReservationTimelineProps {
  status: "pending" | "confirmed" | "active" | "completed" | "cancelled";
}

interface TimelineStep {
  key: string;
  label: string;
  description: string;
  icon: React.ComponentType<{ className?: string }>;
  date?: string;
}

const getTimelineSteps = (status: string): TimelineStep[] => {
  const baseSteps: TimelineStep[] = [
    {
      key: "pending",
      label: "Reservation Created",
      description: "Your reservation has been received and is being processed",
      icon: Clock,
      date: "Mar 20, 2025 at 10:30"
    },
    {
      key: "confirmed",
      label: "Reservation Confirmed",
      description: "Your booking is confirmed and vehicle is reserved",
      icon: CheckCircle,
      date: "Mar 20, 2025 at 11:15"
    },
    {
      key: "active",
      label: "Vehicle Picked Up",
      description: "You have collected your vehicle",
      icon: Car,
      date: undefined
    },
    {
      key: "completed",
      label: "Rental Completed",
      description: "Vehicle returned and rental finalized",
      icon: CheckCircle,
      date: undefined
    }
  ];

  if (status === "cancelled") {
    return [
      {
        key: "pending",
        label: "Reservation Created",
        description: "Your reservation was received",
        icon: Clock,
        date: "Mar 20, 2025 at 10:30"
      },
      {
        key: "cancelled",
        label: "Reservation Cancelled",
        description: "This reservation has been cancelled",
        icon: XCircle,
        date: "Mar 21, 2025 at 14:00"
      }
    ];
  }

  return baseSteps;
};

export default function ReservationTimeline({ status }: ReservationTimelineProps) {
  const steps = getTimelineSteps(status);
  const currentStepIndex = steps.findIndex(step => step.key === status);
  const effectiveIndex = currentStepIndex === -1 ? 0 : currentStepIndex;

  return (
    <div className="bg-white rounded-2xl border border-[#E2E8F0] shadow-sm overflow-hidden">
      <div className="p-6 border-b border-[#E2E8F0]">
        <h2 className="text-lg font-bold text-[#0F172A] flex items-center gap-2">
          <RotateCcw className="h-5 w-5 text-[#0369A1]" />
          Reservation Progress
        </h2>
      </div>

      <div className="p-6">
        <div className="relative">
          <div className="absolute left-6 top-0 bottom-0 w-0.5 bg-[#E2E8F0]">
            <div
              className="absolute left-0 top-0 w-full bg-[#0369A1] transition-all duration-500"
              style={{
                height: status === "cancelled"
                  ? "0%"
                  : `${(effectiveIndex / (steps.length - 1)) * 100}%`
              }}
            />
          </div>

          <div className="space-y-6">
            {steps.map((step, index) => {
              const Icon = step.icon;
              const isCompleted = index < effectiveIndex || step.key === status;
              const isCurrent = step.key === status;
              const isCancelled = status === "cancelled" && step.key === "cancelled";

              return (
                <div
                  key={step.key}
                  className={cn(
                    "relative flex items-start gap-4 transition-all duration-300",
                    isCurrent && "opacity-100",
                    !isCompleted && !isCurrent && "opacity-60"
                  )}
                >
                  <div
                    className={cn(
                      "relative z-10 flex h-12 w-12 items-center justify-center rounded-full border-2 flex-shrink-0",
                      isCancelled && "bg-red-50 border-red-200",
                      isCurrent && !isCancelled && "bg-[#0369A1] border-[#0369A1]",
                      isCompleted && !isCurrent && !isCancelled && "bg-[#0369A1] border-[#0369A1]",
                      !isCompleted && !isCancelled && "bg-white border-[#E2E8F0]"
                    )}
                  >
                    {isCancelled ? (
                      <XCircle className="h-5 w-5 text-red-500" />
                    ) : isCompleted ? (
                      <CheckCircle className="h-5 w-5 text-white" />
                    ) : (
                      <Icon className="h-5 w-5 text-[#94A3B8]" />
                    )}
                  </div>

                  <div className="flex-1 pt-1">
                    <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-1">
                      <h3
                        className={cn(
                          "font-semibold",
                          isCancelled && "text-red-700",
                          isCurrent && !isCancelled && "text-[#0369A1]",
                          !isCurrent && !isCancelled && "text-[#0F172A]"
                        )}
                      >
                        {step.label}
                      </h3>
                      {step.date && (
                        <span className="text-sm text-[#64748B] flex items-center gap-1">
                          <Calendar className="h-3.5 w-3.5" />
                          {step.date}
                        </span>
                      )}
                    </div>
                    <p className="text-sm text-[#64748B] mt-1">{step.description}</p>

                    {isCurrent && !isCancelled && (
                      <div className="mt-3 p-3 rounded-lg bg-[#F0F9FF] border border-[#BAE6FD]">
                        <p className="text-sm text-[#0369A1] flex items-center gap-2">
                          <AlertCircle className="h-4 w-4 flex-shrink-0" />
                          {status === "pending" && "Your reservation is being processed. You will receive a confirmation email shortly."}
                          {status === "confirmed" && "Your reservation is confirmed. We will contact you before pickup."}
                          {status === "active" && "Your rental is currently active. Please return the vehicle by the agreed time."}
                          {status === "completed" && "Thank you for choosing us! We hope you enjoyed your rental experience."}
                        </p>
                      </div>
                    )}

                    {isCancelled && (
                      <div className="mt-3 p-3 rounded-lg bg-red-50 border border-red-200">
                        <p className="text-sm text-red-700 flex items-center gap-2">
                          <AlertCircle className="h-4 w-4 flex-shrink-0" />
                          This reservation has been cancelled. Contact support for more information.
                        </p>
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}
