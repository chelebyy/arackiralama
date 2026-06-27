import type {
  ApiSuccessResponse,
  Campaign as PublicCampaign,
  Customer,
  OpeningHours,
  Office,
  PaginatedResponse,
  Reservation,
  Vehicle,
  VehicleGroup,
} from '../types';

export type AdminVehicleStatus = 'Available' | 'Reserved' | 'Rented' | 'Maintenance' | 'OutOfService' | 'Retired';
export type AdminUserRole = 'Admin' | 'SuperAdmin';
export type PricingCalculationType = 'multiplier' | 'fixed';
export type ReportChangeType = 'increase' | 'decrease' | 'neutral';
export type ReportPeriod = 'daily' | 'weekly' | 'monthly' | 'quarterly' | 'yearly';

export interface AdminVehicle extends Partial<Vehicle> {
  id: string;
  plate: string;
  brand?: string;
  model?: string;
  year?: number;
  color?: string;
  name?: string;
  photoUrl?: string | null;
  officeId: string;
  groupId: string;
  office?: Pick<AdminOffice, 'id' | 'name'>;
  group?: Pick<AdminVehicleGroup, 'id' | 'name'>;
  status: AdminVehicleStatus | number;
  officeName?: string;
  groupName?: string;
  adminNotes?: string | null;
  lastMaintenanceDate?: string;
  nextMaintenanceDate?: string;
  mileage?: number;
}

export interface AdminVehicleGroup {
  id: string;
  name?: string;
  nameTr?: string;
  nameEn?: string;
  nameRu?: string;
  nameAr?: string;
  nameDe?: string;
  description?: string;
  imageUrl?: string;
  vehicles?: AdminVehicle[];
  priceRange?: VehicleGroup['priceRange'];
  depositAmount: number;
  minAge: number;
  minLicenseYears: number;
  isActive: boolean;
  features: string[];
  createdAt?: string;
  updatedAt?: string;
}

export interface AdminOffice extends Office {
  type: 'airport' | 'hotel' | 'office';
}

export interface AdminReservation extends Reservation {
  reservationCode: string;
  customer: AdminCustomer;
  customerName: string;
  totalPrice: number;
  vehicle?: {
    id?: string;
    name: string;
    plate?: string;
  };
  vehiclePlate?: string;
  assignedVehicleId?: string;
  adminNotes?: string;
  cancellationReason?: string;
  refundAmount?: number;
  checkedInAt?: string;
  checkedOutAt?: string;
  checkedInBy?: string;
  checkedOutBy?: string;
}

export interface AdminCustomer extends Customer {
  id: string;
  name?: string;
  reservationCount: number;
  totalSpent: number;
  createdAt: string;
}

export interface AdminUpdateReservationData {
  pickupDateTimeUtc?: string;
  returnDateTimeUtc?: string;
  pickupOfficeId?: string;
  returnOfficeId?: string;
  notes?: string;
}

export interface AdminUser {
  id: string;
  email: string;
  fullName: string;
  role: AdminUserRole;
  isActive: boolean;
  lastLoginAt: string | null;
  createdAt: string;
}

export interface PricingRule {
  id: string;
  vehicleGroupId: string;
  startDate: string;
  endDate: string;
  dailyPrice: number;
  multiplier: number;
  priority: number;
  calculationType: PricingCalculationType;
  weekdayMultiplier?: number;
  weekendMultiplier?: number;
}

export interface Campaign extends Omit<PublicCampaign, 'discountType'> {
  discountType: PublicCampaign['discountType'] | 'percentage' | 'fixed';
  minDays?: number;
  allowedVehicleGroupIds?: string[];
  startDate?: string;
  endDate?: string;
}

export interface FeatureFlag {
  id: string;
  name: string;
  enabled: boolean;
  description: string;
}

export type PublicSettingsLocale = 'tr' | 'en' | 'ru' | 'ar' | 'de';

export interface PublicLocalizedText {
  label?: string | null;
  value?: string | null;
  description?: string | null;
  name?: string | null;
  address?: string | null;
  hours?: string | null;
  day?: string | null;
}

export type PublicLocalizedTextMap = Partial<Record<PublicSettingsLocale | string, PublicLocalizedText>>;

export interface PublicSiteLink {
  id: string;
  label: string;
  href: string;
  isVisible: boolean;
  sortOrder: number;
  translations?: PublicLocalizedTextMap | null;
}

export interface PublicSocialLink {
  id: string;
  platform: string;
  url: string;
  isVisible: boolean;
  sortOrder: number;
}

export interface PublicContactChannel {
  id: string;
  type: 'phone' | 'whatsapp' | 'email' | 'emergency' | string;
  label: string;
  value: string;
  href: string;
  description: string;
  isVisible: boolean;
  sortOrder: number;
  translations?: PublicLocalizedTextMap | null;
}

export interface PublicContactOffice {
  id: string;
  name: string;
  address: string;
  phone: string;
  hours: string;
  type: 'main' | 'airport' | 'branch' | string;
  isVisible: boolean;
  sortOrder: number;
  translations?: PublicLocalizedTextMap | null;
}

export interface PublicContactWorkingHour {
  id: string;
  day: string;
  hours: string;
  isVisible: boolean;
  sortOrder: number;
  translations?: PublicLocalizedTextMap | null;
}

export interface PublicPageBlock {
  id: string;
  heading: string;
  body: string;
  isVisible: boolean;
  sortOrder: number;
}

export interface PublicManagedPage {
  id: string;
  slug: string;
  locale: string;
  title: string;
  subtitle: string;
  seoTitle: string;
  seoDescription: string;
  isPublished: boolean;
  sortOrder: number;
  blocks: PublicPageBlock[];
}

export interface PublicPaymentMethods {
  creditCardEnabled: boolean;
  debitCardEnabled: boolean;
  unpaidRequestEnabled: boolean;
  paypalEnabled: boolean;
  anyEnabled: boolean;
}

export interface PublicSiteSettings {
  companyName: string;
  companyAddress: string;
  companyPhone: string;
  companyEmail: string;
  workingHours: string;
  headerLinks: PublicSiteLink[];
  heroLinks: PublicSiteLink[];
  quickLinks: PublicSiteLink[];
  socialLinks: PublicSocialLink[];
  footerBottomLinks: PublicSiteLink[];
  contactPageChannels: PublicContactChannel[];
  contactPageOffices: PublicContactOffice[];
  contactPageWorkingHours: PublicContactWorkingHour[];
  contactPageMapTitle: string;
  contactPageMapEmbedUrl: string;
  contactPageMapIsVisible: boolean;
  pages: PublicManagedPage[];
  paymentMethods: PublicPaymentMethods;
  onlinePaymentEnabled: boolean;
  updatedAt: string;
}

export type UpdatePublicSiteSettingsData = Omit<
  PublicSiteSettings,
  'updatedAt' | 'paymentMethods' | 'onlinePaymentEnabled'
>;

export interface AuditLog {
  id: string;
  action: string;
  entityType: string;
  entityId: string;
  userId: string;
  userEmail: string;
  timestamp: string;
  details: string;
}

export interface ReportStat {
  label: string;
  value: number | string;
  change?: number;
  changeType?: ReportChangeType;
}

export interface RevenueReportBreakdown {
  date: string;
  revenue: number;
  reservations: number;
}

export interface RevenueReport {
  period: string;
  totalRevenue: number;
  totalReservations: number;
  averageOrderValue: number;
  dailyBreakdown: RevenueReportBreakdown[];
}

export interface OccupancyReportBreakdown {
  date: string;
  occupiedVehicles: number;
  totalVehicles: number;
  occupancyRate: number;
}

export interface OccupancyReport {
  period: string;
  totalVehicles: number;
  occupiedVehicles: number;
  occupancyRate: number;
  dailyBreakdown: OccupancyReportBreakdown[];
}

export interface PopularVehicleReportItem {
  vehicleName: string;
  rentalCount: number;
  revenue: number;
}

export type PopularVehicleReport = PopularVehicleReportItem;

export interface AdminListParams {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface VehicleListParams extends AdminListParams {
  officeId?: string;
  groupId?: string;
  status?: AdminVehicleStatus;
}

export interface ReservationListParams extends AdminListParams {
  searchTerm?: string;
  status?: Reservation['status'];
  pickupOfficeId?: string;
  returnOfficeId?: string;
  startDate?: string;
  endDate?: string;
}

export interface PricingRuleListParams extends AdminListParams {
  vehicleGroupId?: string;
  startDate?: string;
  endDate?: string;
}

export interface AuditLogListParams extends AdminListParams {
  entityType?: string;
  userId?: string;
  action?: string;
  startDate?: string;
  endDate?: string;
}

export interface CreateVehicleData {
  plate: string;
  brand: string;
  model: string;
  year: number;
  color: string;
  groupId: string;
  officeId: string;
  status: AdminVehicleStatus;
}

export type UpdateVehicleData = CreateVehicleData;

export interface TransferVehicleData {
  officeId: string;
}

export interface VehicleMaintenanceData {
  lastMaintenanceDate?: string;
  nextMaintenanceDate: string;
  adminNotes?: string;
}

export interface CreateVehicleGroupData {
  nameTr: string;
  nameEn: string;
  nameRu: string;
  nameAr: string;
  nameDe: string;
  depositAmount: number;
  minAge: number;
  minLicenseYears: number;
  isActive: boolean;
  features?: string[];
}

export type UpdateVehicleGroupData = CreateVehicleGroupData;

export interface CreateOfficeData {
  name: string;
  code: string;
  type: AdminOffice['type'];
  city: string;
  district: string;
  address: string;
  phone: string;
  email: string;
  isActive: boolean;
  isAirport: boolean;
  isHotel: boolean;
  coordinates: AdminOffice['coordinates'];
  openingHours: OpeningHours;
  services: string[];
}

export type UpdateOfficeData = CreateOfficeData;

export interface CancelReservationData {
  reason: string;
}

export interface AssignVehicleData {
  vehicleId: string;
}

export interface ReservationCheckInData {
  checkedInBy: string;
  adminNotes?: string;
}

export interface AdminManualReservationData {
  vehicleId: string;
  pickupOfficeId: string;
  returnOfficeId: string;
  pickupDateTimeUtc: string;
  returnDateTimeUtc: string;
  customerFirstName: string;
  customerLastName: string;
  customerPhone: string;
  customerEmail?: string;
  notes?: string;
  totalAmount?: number;
}

export interface ReservationCheckOutData {
  checkedOutBy: string;
  mileage?: number;
  fuelLevel?: number;
  adminNotes?: string;
}

export interface AdminRefundData {
  amount?: number;
  reason?: string;
  idempotencyKey?: string;
}

export interface AdminPaymentOperation {
  reservationId: string;
  paymentIntentId?: string;
  paymentKind?: string;
  operation: string;
  status: string;
  amount?: number;
  currency?: string;
  referenceId?: string;
  reason?: string;
}

export interface CreatePricingRuleData {
  vehicleGroupId: string;
  startDate: string;
  endDate: string;
  dailyPrice: number;
  multiplier: number;
  weekdayMultiplier: number;
  weekendMultiplier: number;
  priority: number;
  calculationType: PricingCalculationType;
}
export type UpdatePricingRuleData = CreatePricingRuleData;

export interface CreateCampaignData {
  code: string;
  name?: string;
  description?: string;
  discountType: 'percentage' | 'fixed';
  discountValue: number;
  minRentalDays: number;
  minDays?: number;
  validFrom: string;
  validUntil: string;
  isActive: boolean;
  allowedVehicleGroupIds?: string[];
}

export type UpdateCampaignData = CreateCampaignData;

export interface CustomerListParams extends AdminListParams {
  createdAfter?: string;
  createdBefore?: string;
}

export interface AdminUserListParams extends AdminListParams {
  role?: AdminUserRole;
  isActive?: boolean;
}

export interface CreateAdminUserData {
  email: string;
  password: string;
  fullName: string;
  role: AdminUserRole;
}

export interface UpdateAdminUserRoleData {
  role: AdminUserRole;
}

export interface UpdateAdminUserData {
  email: string;
  fullName: string;
  role: AdminUserRole;
}

export interface UpdateAdminUserStatusData {
  isActive: boolean;
}

export interface AdminRefundData {
  amount?: number;
  reason?: string;
  idempotencyKey?: string;
}

export interface AdminPaymentOperation {
  reservationId: string;
  paymentIntentId?: string;
  paymentKind?: string;
  operation: string;
  status: string;
  amount?: number;
  currency?: string;
  referenceId?: string;
  reason?: string;
}

export type AdminResponse<T> = T | ApiSuccessResponse<T>;
export type AdminPaginatedResponse<T> =
  | PaginatedResponse<T>
  | T[]
  | ApiSuccessResponse<PaginatedResponse<T> | T[]>;

export type { ApiSuccessResponse, PaginatedResponse } from '../types';
