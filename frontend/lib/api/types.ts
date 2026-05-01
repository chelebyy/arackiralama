export interface Vehicle {
  id: string;
  name: string;
  description: string;
  imageUrl: string;
  images: string[];
  groupId: string;
  groupName: string;
  transmission: TransmissionType;
  fuelType: FuelType;
  seatCount: number;
  luggageCapacity: number;
  hasAirConditioning: boolean;
  minDriverAge: number;
  minLicenseYears: number;
  dailyPrice: number;
  weeklyPrice: number;
  monthlyPrice: number;
  features: VehicleFeature[];
  insuranceIncluded: boolean;
  mileageLimit: number | null;
  extraMileagePrice: number | null;
  availableExtras: VehicleExtra[];
}

export interface VehicleGroup {
  id: string;
  name: string;
  description: string;
  imageUrl: string;
  vehicles: Vehicle[];
  priceRange: {
    min: number;
    max: number;
  };
}

export interface AvailableVehicleGroup {
  groupId: string;
  groupName: string;
  groupNameEn: string;
  availableCount: number;
  dailyPrice: number;
  currency: string;
  depositAmount: number;
  minAge: number;
  minLicenseYears: number;
  features: string[];
  imageUrl: string | null;
}

export enum TransmissionType {
  MANUAL = 'MANUAL',
  AUTOMATIC = 'AUTOMATIC',
  SEMI_AUTOMATIC = 'SEMI_AUTOMATIC',
}

export enum FuelType {
  PETROL = 'PETROL',
  DIESEL = 'DIESEL',
  HYBRID = 'HYBRID',
  ELECTRIC = 'ELECTRIC',
  LPG = 'LPG',
}

export interface VehicleFeature {
  id: string;
  name: string;
  icon: string;
  description?: string;
}

export interface VehicleExtra {
  id: string;
  name: string;
  description: string;
  dailyPrice: number;
  maxQuantity: number;
  icon?: string;
}

export interface Office {
  id: string;
  name: string;
  code: string;
  address: string;
  city: string;
  district: string;
  postalCode?: string;
  phone: string;
  email: string;
  coordinates: {
    latitude: number;
    longitude: number;
  };
  openingHours: OpeningHours;
  isAirport: boolean;
  airportCode?: string;
  isHotel: boolean;
  hotelName?: string;
  services: string[];
  isActive: boolean;
}

export interface OpeningHours {
  monday: DayHours;
  tuesday: DayHours;
  wednesday: DayHours;
  thursday: DayHours;
  friday: DayHours;
  saturday: DayHours;
  sunday: DayHours;
}

export interface DayHours {
  open: string;
  close: string;
  isClosed: boolean;
}

export interface Location {
  id: string;
  name: string;
  type: 'AIRPORT' | 'CITY_CENTER' | 'HOTEL' | 'OTHER';
  officeId?: string;
  coordinates?: {
    latitude: number;
    longitude: number;
  };
}

export interface Reservation {
  id: string;
  publicCode: string;
  status: ReservationStatus;
  vehicleId: string;
  vehicleName: string;
  vehicleImage: string;
  pickupOfficeId: string;
  pickupOfficeName: string;
  pickupDate: string;
  pickupTime: string;
  returnOfficeId: string;
  returnOfficeName: string;
  returnDate: string;
  returnTime: string;
  customer: Customer;
  driver: Driver;
  extras: ReservationExtra[];
  priceBreakdown: PriceBreakdown;
  campaignCode?: string;
  campaignDiscount?: number;
  createdAt: string;
  updatedAt: string;
  expiresAt?: string;
  paymentStatus: PaymentStatus;
  paymentIntentId?: string;
  notes?: string;
}

export enum ReservationStatus {
  PENDING = 'PENDING',
  HOLD = 'HOLD',
  CONFIRMED = 'CONFIRMED',
  ACTIVE = 'ACTIVE',
  COMPLETED = 'COMPLETED',
  CANCELLED = 'CANCELLED',
  EXPIRED = 'EXPIRED',
}

export enum PaymentStatus {
  PENDING = 'PENDING',
  AUTHORIZED = 'AUTHORIZED',
  CAPTURED = 'CAPTURED',
  FAILED = 'FAILED',
  REFUNDED = 'REFUNDED',
  PARTIALLY_REFUNDED = 'PARTIALLY_REFUNDED',
}

export interface Customer {
  id?: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  dateOfBirth?: string;
  nationality?: string;
  passportNumber?: string;
  passportCountry?: string;
  passportExpiry?: string;
  address?: Address;
}

export interface Driver {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  licenseNumber: string;
  licenseCountry: string;
  licenseIssueDate: string;
  licenseExpiryDate: string;
  isPrimaryDriver: boolean;
}

export interface Address {
  street: string;
  city: string;
  postalCode: string;
  country: string;
}

export interface ReservationExtra {
  extraId: string;
  name: string;
  quantity: number;
  dailyPrice: number;
  totalPrice: number;
}

export interface PriceBreakdown {
  basePrice: number;
  rentalDays: number;
  extraFees: ExtraFee[];
  extrasTotal: number;
  insuranceTotal: number;
  subtotal: number;
  taxRate: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  currency: string;
  depositAmount: number;
}

export interface ExtraFee {
  name: string;
  description: string;
  amount: number;
  isOptional: boolean;
}

export interface Campaign {
  id: string;
  code: string;
  name: string;
  description: string;
  discountType: 'PERCENTAGE' | 'FIXED_AMOUNT';
  discountValue: number;
  minRentalDays?: number;
  maxDiscount?: number;
  validFrom: string;
  validUntil: string;
  isActive: boolean;
}

export interface PaymentIntent {
  id: string;
  clientSecret: string;
  amount: number;
  currency: string;
  status: PaymentIntentStatus;
  reservationId: string;
}

export enum PaymentIntentStatus {
  REQUIRES_PAYMENT_METHOD = 'REQUIRES_PAYMENT_METHOD',
  REQUIRES_CONFIRMATION = 'REQUIRES_CONFIRMATION',
  REQUIRES_ACTION = 'REQUIRES_ACTION',
  PROCESSING = 'PROCESSING',
  SUCCEEDED = 'SUCCEEDED',
  CANCELLED = 'CANCELLED',
  FAILED = 'FAILED',
}

export interface ApiErrorResponse {
  statusCode: number;
  message: string;
  code: string;
  details?: Record<string, string[]>;
  timestamp: string;
  path: string;
}

export interface ApiSuccessResponse<T> {
  data: T;
  meta?: {
    page?: number;
    pageSize?: number;
    totalCount?: number;
    totalPages?: number;
  };
}

export interface PaginatedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface AvailableVehiclesParams {
  office_id: string;
  pickup_datetime: string;
  return_datetime: string;
  vehicle_group_id?: string;
  page?: number;
}

export interface PriceBreakdownParams {
  vehicle_group_id: string;
  pickup_office_id: string;
  return_office_id: string;
  pickup_datetime: string;
  return_datetime: string;
  campaign_code?: string;
  extra_driver_count?: number;
  child_seat_count?: number;
  driver_age?: number;
  full_coverage_waiver?: boolean;
}

export interface ValidateCampaignParams {
  code: string;
  vehicleGroupId: string;
  rentalDays: number;
  pickupDate: string;
}

export interface ValidateCampaignResponse {
  valid: boolean;
}

export interface CreateReservationData {
  vehicleGroupId: string;
  pickupOfficeId: string;
  returnOfficeId: string;
  pickupDateTimeUtc: string;
  returnDateTimeUtc: string;
  customer: Customer;
  campaignCode?: string;
  extraDriverCount?: number;
  childSeatCount?: number;
  driverAge?: number;
  fullCoverageWaiver?: boolean;
  notes?: string;
}

export interface HoldReservationData {
  durationMinutes?: number;
}

export interface ExtendHoldData {
  additionalMinutes: number;
}
