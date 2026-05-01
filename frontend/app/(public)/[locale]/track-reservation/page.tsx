"use client";

import { useTranslations } from "next-intl";
import { useState } from "react";
import {
  Search,
  Calendar,
  Clock,
  MapPin,
  User,
  Car,
  CreditCard,
  CheckCircle,
  AlertCircle,
  ChevronRight,
  Phone,
  Mail,
  MessageCircle,
  Copy,
  Shield
} from "lucide-react";
import { cn } from "@/lib/utils";
import ReservationTimeline from "@/components/public/ReservationTimeline";

interface ReservationDetails {
  id: string;
  code: string;
  status: "pending" | "confirmed" | "active" | "completed" | "cancelled";
  vehicleName: string;
  vehicleCategory: string;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  pickupLocation: string;
  dropoffLocation: string;
  pickupDate: string;
  dropoffDate: string;
  pickupTime: string;
  dropoffTime: string;
  totalAmount: number;
  depositAmount: number;
  paymentStatus: "pending" | "paid" | "refunded";
  createdAt: string;
  confirmedAt?: string;
  updatedAt: string;
}

const mockReservation: ReservationDetails = {
  id: "res_123456789",
  code: "ALN-2025-8842",
  status: "confirmed",
  vehicleName: "Volkswagen Golf",
  vehicleCategory: "Compact",
  customerName: "John Smith",
  customerEmail: "john.smith@email.com",
  customerPhone: "+44 7700 900001",
  pickupLocation: "Gazipasa Airport (GZP)",
  dropoffLocation: "Alanya City Center",
  pickupDate: "2025-04-15",
  dropoffDate: "2025-04-22",
  pickupTime: "14:00",
  dropoffTime: "10:00",
  totalAmount: 455,
  depositAmount: 300,
  paymentStatus: "paid",
  createdAt: "2025-03-20T10:30:00Z",
  confirmedAt: "2025-03-20T11:15:00Z",
  updatedAt: "2025-03-20T11:15:00Z"
};

export default function TrackReservationPage() {
  const t = useTranslations();
  const [reservationCode, setReservationCode] = useState("");
  const [isSearching, setIsSearching] = useState(false);
  const [reservation, setReservation] = useState<ReservationDetails | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsSearching(true);

    await new Promise((resolve) => setTimeout(resolve, 1000));

    if (reservationCode.toUpperCase() === "ALN-2025-8842" || reservationCode.toUpperCase() === "DEMO") {
      setReservation(mockReservation);
    } else {
      setError("No reservation found with this code. Please check and try again.");
      setReservation(null);
    }

    setIsSearching(false);
  };

  const handleCopyCode = () => {
    if (reservation) {
      navigator.clipboard.writeText(reservation.code);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case "pending": return "text-amber-600 bg-amber-50 border-amber-200";
      case "confirmed": return "text-emerald-600 bg-emerald-50 border-emerald-200";
      case "active": return "text-blue-600 bg-blue-50 border-blue-200";
      case "completed": return "text-slate-600 bg-slate-50 border-slate-200";
      case "cancelled": return "text-red-600 bg-red-50 border-red-200";
      default: return "text-slate-600 bg-slate-50 border-slate-200";
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString("en-GB", {
      weekday: "short",
      day: "numeric",
      month: "long",
      year: "numeric"
    });
  };

  return (
    <div className="min-h-screen bg-[#F8FAFC]">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-12 lg:py-16">
        <div className="max-w-3xl mx-auto">
          <div className="text-center mb-10">
            <h1 className="text-3xl lg:text-4xl font-bold text-[#0F172A] mb-4">
              Track Your Reservation
            </h1>
            <p className="text-lg text-[#64748B]">
              Enter your reservation code to view the status and details of your booking
            </p>
          </div>

          <form onSubmit={handleSearch} className="mb-12">
            <div className="flex flex-col sm:flex-row gap-3">
              <div className="relative flex-1">
                <Search className="absolute left-4 top-1/2 -translate-y-1/2 h-5 w-5 text-[#64748B]" />
                <input
                  type="text"
                  value={reservationCode}
                  onChange={(e) => setReservationCode(e.target.value.toUpperCase())}
                  placeholder="Enter reservation code (e.g., ALN-2025-8842)"
                  className={cn(
                    "w-full pl-12 pr-4 py-4 rounded-xl",
                    "bg-white border border-[#E2E8F0]",
                    "text-base text-[#0F172A] placeholder-[#94A3B8]",
                    "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:border-transparent",
                    "transition-all duration-200",
                    "uppercase tracking-wide"
                  )}
                />
              </div>
              <button
                type="submit"
                disabled={isSearching || !reservationCode.trim()}
                className={cn(
                  "px-8 py-4 rounded-xl text-base font-semibold",
                  "bg-[#0369A1] text-white",
                  "hover:bg-[#0284C7] active:bg-[#075985]",
                  "transition-all duration-200",
                  "focus:outline-none focus:ring-2 focus:ring-[#0369A1] focus:ring-offset-2",
                  "disabled:opacity-50 disabled:cursor-not-allowed",
                  "flex items-center justify-center gap-2"
                )}
              >
                {isSearching ? (
                  <>
                    <div className="h-5 w-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                    Searching...
                  </>
                ) : (
                  <>
                    <Search className="h-5 w-5" />
                    Track Reservation
                  </>
                )}
              </button>
            </div>

            {error && (
              <div className="mt-4 p-4 rounded-xl bg-red-50 border border-red-200 flex items-start gap-3">
                <AlertCircle className="h-5 w-5 text-red-500 flex-shrink-0 mt-0.5" />
                <p className="text-sm text-red-700">{error}</p>
              </div>
            )}
          </form>

          {reservation && (
            <div className="space-y-6">
              <div className="bg-white rounded-2xl border border-[#E2E8F0] shadow-sm overflow-hidden">
                <div className="p-6 border-b border-[#E2E8F0] bg-gradient-to-r from-[#F0F9FF] to-white">
                  <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                    <div>
                      <p className="text-sm text-[#64748B] mb-1">Reservation Code</p>
                      <div className="flex items-center gap-2">
                        <span className="text-2xl font-bold text-[#0F172A] tracking-wider">
                          {reservation.code}
                        </span>
                        <button
                          type="button"
                          onClick={handleCopyCode}
                          className={cn(
                            "p-2 rounded-lg transition-colors",
                            "hover:bg-[#E2E8F0] focus:outline-none focus:ring-2 focus:ring-[#0369A1]"
                          )}
                          aria-label="Copy reservation code"
                        >
                          {copied ? (
                            <CheckCircle className="h-4 w-4 text-emerald-500" />
                          ) : (
                            <Copy className="h-4 w-4 text-[#64748B]" />
                          )}
                        </button>
                      </div>
                    </div>
                    <div
                      className={cn(
                        "inline-flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-semibold border",
                        getStatusColor(reservation.status)
                      )}
                    >
                      <Shield className="h-4 w-4" />
                      {reservation.status.charAt(0).toUpperCase() + reservation.status.slice(1)}
                    </div>
                  </div>
                </div>

                <div className="p-6">
                  <h2 className="text-lg font-bold text-[#0F172A] mb-4 flex items-center gap-2">
                    <Car className="h-5 w-5 text-[#0369A1]" />
                    Vehicle Information
                  </h2>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-6">
                    <div className="p-4 rounded-xl bg-[#F8FAFC]">
                      <p className="text-sm text-[#64748B] mb-1">Vehicle</p>
                      <p className="text-base font-semibold text-[#0F172A]">{reservation.vehicleName}</p>
                      <p className="text-sm text-[#0369A1]">{reservation.vehicleCategory}</p>
                    </div>
                    <div className="p-4 rounded-xl bg-[#F8FAFC]">
                      <p className="text-sm text-[#64748B] mb-1">Rental Period</p>
                      <p className="text-base font-semibold text-[#0F172A]">
                        {Math.ceil((new Date(reservation.dropoffDate).getTime() - new Date(reservation.pickupDate).getTime()) / (1000 * 60 * 60 * 24))} Days
                      </p>
                      <p className="text-sm text-[#64748B]">{formatDate(reservation.pickupDate)} - {formatDate(reservation.dropoffDate)}</p>
                    </div>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-6 mb-6">
                    <div className="space-y-4">
                      <h3 className="text-sm font-semibold text-[#0F172A] flex items-center gap-2">
                        <MapPin className="h-4 w-4 text-[#0369A1]" />
                        Pickup
                      </h3>
                      <div className="p-4 rounded-xl bg-[#F0F9FF] border border-[#BAE6FD]">
                        <p className="font-semibold text-[#0F172A] mb-1">{reservation.pickupLocation}</p>
                        <div className="flex items-center gap-4 text-sm text-[#64748B]">
                          <span className="flex items-center gap-1">
                            <Calendar className="h-4 w-4" />
                            {formatDate(reservation.pickupDate)}
                          </span>
                          <span className="flex items-center gap-1">
                            <Clock className="h-4 w-4" />
                            {reservation.pickupTime}
                          </span>
                        </div>
                      </div>
                    </div>

                    <div className="space-y-4">
                      <h3 className="text-sm font-semibold text-[#0F172A] flex items-center gap-2">
                        <MapPin className="h-4 w-4 text-[#0369A1]" />
                        Drop-off
                      </h3>
                      <div className="p-4 rounded-xl bg-[#F0F9FF] border border-[#BAE6FD]">
                        <p className="font-semibold text-[#0F172A] mb-1">{reservation.dropoffLocation}</p>
                        <div className="flex items-center gap-4 text-sm text-[#64748B]">
                          <span className="flex items-center gap-1">
                            <Calendar className="h-4 w-4" />
                            {formatDate(reservation.dropoffDate)}
                          </span>
                          <span className="flex items-center gap-1">
                            <Clock className="h-4 w-4" />
                            {reservation.dropoffTime}
                          </span>
                        </div>
                      </div>
                    </div>
                  </div>

                  <h2 className="text-lg font-bold text-[#0F172A] mb-4 flex items-center gap-2">
                    <User className="h-5 w-5 text-[#0369A1]" />
                    Customer Information
                  </h2>
                  <div className="p-4 rounded-xl bg-[#F8FAFC] mb-6">
                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                      <div>
                        <p className="text-sm text-[#64748B] mb-1">Name</p>
                        <p className="font-medium text-[#0F172A]">{reservation.customerName}</p>
                      </div>
                      <div>
                        <p className="text-sm text-[#64748B] mb-1">Email</p>
                        <p className="font-medium text-[#0F172A]">{reservation.customerEmail}</p>
                      </div>
                      <div>
                        <p className="text-sm text-[#64748B] mb-1">Phone</p>
                        <p className="font-medium text-[#0F172A]">{reservation.customerPhone}</p>
                      </div>
                    </div>
                  </div>

                  <h2 className="text-lg font-bold text-[#0F172A] mb-4 flex items-center gap-2">
                    <CreditCard className="h-5 w-5 text-[#0369A1]" />
                    Payment Summary
                  </h2>
                  <div className="p-4 rounded-xl bg-[#F8FAFC]">
                    <div className="space-y-2 mb-4">
                      <div className="flex justify-between text-sm">
                        <span className="text-[#64748B]">Rental Total</span>
                        <span className="font-medium text-[#0F172A]">₺{reservation.totalAmount}</span>
                      </div>
                      <div className="flex justify-between text-sm">
                        <span className="text-[#64748B]">Security Deposit</span>
                        <span className="font-medium text-[#0F172A]">₺{reservation.depositAmount}</span>
                      </div>
                    </div>
                    <div className="pt-4 border-t border-[#E2E8F0] flex justify-between items-center">
                      <span className="font-semibold text-[#0F172A]">Payment Status</span>
                      <span className={cn(
                        "px-3 py-1 rounded-lg text-sm font-medium",
                        reservation.paymentStatus === "paid" && "bg-emerald-100 text-emerald-700",
                        reservation.paymentStatus === "pending" && "bg-amber-100 text-amber-700",
                        reservation.paymentStatus === "refunded" && "bg-slate-100 text-slate-700"
                      )}>
                        {reservation.paymentStatus.charAt(0).toUpperCase() + reservation.paymentStatus.slice(1)}
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              <ReservationTimeline status={reservation.status} />
            </div>
          )}

          <div className="mt-12 p-6 rounded-2xl bg-gradient-to-br from-[#0369A1] to-[#0C4A6E] text-white">
            <div className="flex flex-col sm:flex-row items-start sm:items-center gap-4">
              <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-white/10">
                <MessageCircle className="h-6 w-6" />
              </div>
              <div className="flex-1">
                <h3 className="text-lg font-bold mb-1">Need Help?</h3>
                <p className="text-white/80 text-sm">
                  Our support team is available 24/7 to assist you with any questions about your reservation
                </p>
              </div>
              <div className="flex flex-wrap gap-3">
                <a
                  href="tel:+905551234567"
                  className={cn(
                    "inline-flex items-center gap-2 px-4 py-2.5 rounded-xl",
                    "text-sm font-semibold bg-white text-[#0369A1]",
                    "hover:bg-[#F8FAFC] transition-all duration-200"
                  )}
                >
                  <Phone className="h-4 w-4" />
                  Call Us
                </a>
                <a
                  href="mailto:support@alanyacarrental.com"
                  className={cn(
                    "inline-flex items-center gap-2 px-4 py-2.5 rounded-xl",
                    "text-sm font-semibold bg-white/10 text-white border border-white/20",
                    "hover:bg-white/20 transition-all duration-200"
                  )}
                >
                  <Mail className="h-4 w-4" />
                  Email Us
                </a>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
