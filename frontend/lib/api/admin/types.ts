import type {
  ApiSuccessResponse,
  Campaign as PublicCampaign,
  Customer,
  Office,
  PaginatedResponse,
  Reservation,
  Vehicle,
  VehicleGroup,
} from '../types';

export type AdminVehicleStatus = 'Available' | 'Maintenance' | 'Retired';
export type AdminUserRole = 'Admin' | 'SuperAdmin';
export type PricingCalculationType = 'multiplier' | 'fixed';
export type ReportChangeType = 'increase' | 'decrease' | 'neutral';
export type ReportPeriod = 'daily' | 'weekly' | 'monthly' | 'quarterly' | 'yearly';

export interface AdminVehicle extends Vehicle {
  plate: string;
  officeId: string;
  office?: Pick<AdminOffice, 'id' | 'name'>;
  group?: Pick<AdminVehicleGroup, 'id' | 'name'>;
  status: AdminVehicleStatus;
  officeName: string;
  adminNotes?: string | null;
  lastMaintenanceDate: string;
  nextMaintenanceDate: string;
  mileage: number;
}

export interface AdminVehicleGroup extends VehicleGroup {
  depositAmount: number;
  minAge: number;
  minLicenseYears: number;
  features: string[];
  createdAt: string;
  updatedAt: string;
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
  startDate?: string;
  endDate?: string;
}

export interface FeatureFlag {
  id: string;
  name: string;
  enabled: boolean;
  description: string;
}

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

export interface CreateVehicleData extends Omit<AdminVehicle, 'id'> {}
export interface UpdateVehicleData extends Partial<CreateVehicleData> {}

export interface TransferVehicleData {
  officeId: string;
}

export interface VehicleMaintenanceData {
  lastMaintenanceDate?: string;
  nextMaintenanceDate: string;
  adminNotes?: string;
}

export interface CreateVehicleGroupData extends Omit<AdminVehicleGroup, 'id'> {}
export interface UpdateVehicleGroupData extends Partial<CreateVehicleGroupData> {}

export interface CreateOfficeData extends Omit<AdminOffice, 'id'> {}
export interface UpdateOfficeData extends Partial<CreateOfficeData> {}

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

export interface ReservationCheckOutData {
  checkedOutBy: string;
  mileage?: number;
  fuelLevel?: number;
  adminNotes?: string;
}

export interface CreatePricingRuleData extends Omit<PricingRule, 'id'> {}
export interface UpdatePricingRuleData extends Partial<CreatePricingRuleData> {}

export interface CreateCampaignData extends Omit<Campaign, 'id'> {}
export interface UpdateCampaignData extends Partial<CreateCampaignData> {}

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
  fullName: string;
  role: AdminUserRole;
}

export interface UpdateAdminUserRoleData {
  role: AdminUserRole;
}

export interface UpdateAdminUserStatusData {
  isActive: boolean;
}

export type AdminResponse<T> = T | ApiSuccessResponse<T>;
export type AdminPaginatedResponse<T> = PaginatedResponse<T> | ApiSuccessResponse<PaginatedResponse<T>>;

export type { ApiSuccessResponse, PaginatedResponse } from '../types';
