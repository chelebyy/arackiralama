import { adminDel, adminGet, adminPatch, adminPost, adminPut } from "../client";
import type { ApiSuccessResponse } from "../types";

export const RESERVATION_EXTRA_LOCALES = ["tr", "en", "de", "ru", "ar"] as const;
export const RESERVATION_EXTRA_ICON_KEYS = ["baby", "users", "navigation", "wifi"] as const;

export type ReservationExtraLocale = (typeof RESERVATION_EXTRA_LOCALES)[number];
export type ReservationExtraIconKey = (typeof RESERVATION_EXTRA_ICON_KEYS)[number];
export type ReservationExtraPricingMode = "PER_DAY" | "PER_RENTAL";
export type ReservationExtraStatusFilter = "all" | "active" | "inactive" | "archived";

export interface ReservationExtraTranslation {
  locale: ReservationExtraLocale;
  name: string;
  description: string;
}

export interface AdminReservationExtraOption {
  id: string;
  code: string;
  unitPrice: number;
  pricingMode: ReservationExtraPricingMode;
  maxQuantity: number;
  iconKey: ReservationExtraIconKey;
  sortOrder: number;
  isActive: boolean;
  isArchived: boolean;
  version: number;
  updatedAt: string;
  vehicleGroupIds: string[];
  translations: ReservationExtraTranslation[];
}

export interface ReservationExtraOptionListResponse {
  totalCount: number;
  page: number;
  pageSize: number;
  items: AdminReservationExtraOption[];
}

export interface ReservationExtraOptionListParams {
  search?: string;
  status?: ReservationExtraStatusFilter;
  vehicleGroupId?: string;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface CreateReservationExtraOptionData {
  unitPrice: number;
  pricingMode: ReservationExtraPricingMode;
  maxQuantity: number;
  iconKey: ReservationExtraIconKey;
  sortOrder: number;
  vehicleGroupIds: string[];
  translations: ReservationExtraTranslation[];
}

export interface UpdateReservationExtraOptionData extends CreateReservationExtraOptionData {
  version: number;
}

export interface UpdateReservationExtraOptionStatusData {
  version: number;
  isActive: boolean;
}

export interface DeleteReservationExtraOptionResult {
  disposition: string;
}

type AdminResponse<T> = T | ApiSuccessResponse<T>;

const ENDPOINT = "/v1/reservation-extra-options";

function unwrap<T>(response: AdminResponse<T>): T {
  if (response && typeof response === "object" && "data" in response) {
    return (response as ApiSuccessResponse<T>).data;
  }
  return response as T;
}

function buildQueryString(params: ReservationExtraOptionListParams = {}): string {
  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      query.set(key, String(value));
    }
  });
  const value = query.toString();
  return value ? `?${value}` : "";
}

export async function getReservationExtraOptions(
  params: ReservationExtraOptionListParams = {}
): Promise<ReservationExtraOptionListResponse> {
  const response = await adminGet<AdminResponse<ReservationExtraOptionListResponse>>(
    `${ENDPOINT}${buildQueryString(params)}`
  );
  return unwrap(response);
}

export async function createReservationExtraOption(
  data: CreateReservationExtraOptionData
): Promise<AdminReservationExtraOption> {
  return unwrap(await adminPost<AdminResponse<AdminReservationExtraOption>>(ENDPOINT, data));
}

export async function updateReservationExtraOption(
  id: string,
  data: UpdateReservationExtraOptionData
): Promise<AdminReservationExtraOption> {
  return unwrap(
    await adminPut<AdminResponse<AdminReservationExtraOption>>(`${ENDPOINT}/${id}`, data)
  );
}

export async function updateReservationExtraOptionStatus(
  id: string,
  data: UpdateReservationExtraOptionStatusData
): Promise<AdminReservationExtraOption> {
  return unwrap(
    await adminPatch<AdminResponse<AdminReservationExtraOption>>(`${ENDPOINT}/${id}/status`, data)
  );
}

export async function deleteReservationExtraOption(
  id: string,
  version: number
): Promise<DeleteReservationExtraOptionResult> {
  return unwrap(
    await adminDel<AdminResponse<DeleteReservationExtraOptionResult>>(
      `${ENDPOINT}/${id}?version=${encodeURIComponent(String(version))}`
    )
  );
}

export async function restoreReservationExtraOption(
  id: string,
  version: number
): Promise<AdminReservationExtraOption> {
  const result = unwrap(
    await adminPost<AdminResponse<{ item: AdminReservationExtraOption }>>(
      `${ENDPOINT}/${id}/restore`,
      { version }
    )
  );
  return result.item;
}
