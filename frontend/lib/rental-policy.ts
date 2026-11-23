export interface OperatingWindow { day: number; startMinute: number; endMinute: number }
export interface GuestManagementPolicy {
  allowCancellation: boolean;
  cancellationNoticeMinutes: number | null;
  cancellationFee: number | null;
  allowDateChange: boolean;
  changeNoticeMinutes: number | null;
  changeFee: number | null;
}
export interface OperatingPolicy {
  guestManagement?: GuestManagementPolicy | null;
  minimumNoticeMinutes: number | null;
  preparationMinutes: number | null;
  pickupWindows: OperatingWindow[];
  returnWindows: OperatingWindow[];
  closedDates: string[];
}
export interface RentalRate {
  id: string;
  startDate: string;
  endDate: string;
  dailyPrice: number;
  multiplier: number;
  weekdayMultiplier: number;
  weekendMultiplier: number;
  calculationType: "fixed" | "multiplier";
  priority: number;
  createdAt: string;
}
export interface RentalTerms {
  depositAmount: number | null;
  minAge: number | null;
  minLicenseYears: number | null;
  rates: RentalRate[];
  extraOptionIds: string[];
}
