import { adminGet, adminPatch, adminPost, adminPut } from "../client";
import type {
  AdminPaginatedResponse,
  AdminPaymentOperation,
  AdminReservation,
  AdminManualReservationData,
  AdminUpdateReservationData,
  AdminResponse,
  AssignVehicleData,
  CancelReservationData,
  AdminRefundData,
  PaginatedResponse,
  ReservationCheckInData,
  ReservationCheckOutData,
  ReservationListParams
} from "./types";

const RESERVATIONS_ENDPOINT = "/v1/reservations";

function buildQueryString(params?: object): string {
  if (!params) return "";
  const searchParams = new URLSearchParams();

  Object.entries(params).forEach(([key, value]) => {
    if (
      value !== undefined &&
      value !== null &&
      value !== "" &&
      (typeof value === "string" || typeof value === "number" || typeof value === "boolean")
    ) {
      searchParams.append(key, String(value));
    }
  });

  const queryString = searchParams.toString();
  return queryString ? `?${queryString}` : "";
}

function unwrapResponse<T>(response: AdminResponse<T>): T {
  if (response && typeof response === "object" && "data" in response) {
    return (response as { data: T }).data;
  }
  return response as T;
}

function createPaginated<T>(items: T[]): PaginatedResponse<T> {
  return {
    items,
    page: 1,
    pageSize: items.length,
    totalCount: items.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false
  };
}

function unwrapPaginated<T>(response: AdminPaginatedResponse<T>) {
  if (Array.isArray(response)) {
    return createPaginated(response);
  }

  if (response && typeof response === "object" && "data" in response) {
    const data = (response as { data: PaginatedResponse<T> | T[] }).data;
    return Array.isArray(data) ? createPaginated(data) : data;
  }

  return response as PaginatedResponse<T>;
}

function splitDateTime(value?: string) {
  if (!value) {
    return { date: "", time: "" };
  }

  const [date = "", timeWithZone = ""] = value.split("T");
  return {
    date,
    time: timeWithZone.slice(0, 5)
  };
}

function normalizeReservation(reservation: AdminReservation): AdminReservation {
  const raw = reservation as AdminReservation & {
    publicCode?: string;
    totalAmount?: number;
    pickupDateTime?: string;
    returnDateTime?: string;
    customerId?: string;
    customerEmail?: string;
    customerPhone?: string;
    vehicleBrand?: string;
    vehicleModel?: string;
    vehicleGroupName?: string;
    customerReservationCount?: number;
    customerTotalSpent?: number;
    rentalDays?: number;
    driver?: AdminReservation["driver"];
    priceBreakdown?: AdminReservation["priceBreakdown"] & {
      dailyRate?: number;
      baseTotal?: number;
      finalTotal?: number;
      campaignDiscount?: number;
      depositAmount?: number;
      airportFee?: number;
      oneWayFee?: number;
      extraDriverFee?: number;
      childSeatFee?: number;
      youngDriverFee?: number;
      fullCoverageWaiverFee?: number;
    };
  };
  const pickup = splitDateTime(raw.pickupDateTime);
  const dropoff = splitDateTime(raw.returnDateTime);
  const vehicleName =
    reservation.vehicleName ||
    [raw.vehicleBrand, raw.vehicleModel].filter(Boolean).join(" ") ||
    raw.vehicleGroupName ||
    reservation.vehicle?.name ||
    "";
  const rawBreakdown = raw.priceBreakdown;
  const normalizedPriceBreakdown = rawBreakdown
    ? {
        basePrice:
          rawBreakdown.basePrice ??
          rawBreakdown.baseTotal ??
          rawBreakdown.finalTotal ??
          raw.totalAmount ??
          0,
        dailyRate: rawBreakdown.dailyRate,
        rentalDays: rawBreakdown.rentalDays ?? raw.rentalDays ?? 1,
        baseTotal: rawBreakdown.baseTotal,
        extraFees: rawBreakdown.extraFees ?? [],
        extrasTotal: rawBreakdown.extrasTotal ?? 0,
        insuranceTotal: rawBreakdown.insuranceTotal ?? rawBreakdown.fullCoverageWaiverFee ?? 0,
        subtotal:
          rawBreakdown.subtotal ??
          (rawBreakdown.baseTotal ?? raw.totalAmount ?? 0) +
            (rawBreakdown.extrasTotal ?? 0) +
            (rawBreakdown.airportFee ?? 0) +
            (rawBreakdown.oneWayFee ?? 0) +
            (rawBreakdown.extraDriverFee ?? 0) +
            (rawBreakdown.childSeatFee ?? 0) +
            (rawBreakdown.youngDriverFee ?? 0) +
            (rawBreakdown.fullCoverageWaiverFee ?? 0),
        taxRate: rawBreakdown.taxRate ?? 0,
        taxAmount: rawBreakdown.taxAmount ?? 0,
        discountAmount: rawBreakdown.discountAmount ?? rawBreakdown.campaignDiscount ?? 0,
        campaignDiscount: rawBreakdown.campaignDiscount,
        totalAmount: rawBreakdown.totalAmount ?? rawBreakdown.finalTotal ?? raw.totalAmount ?? 0,
        finalTotal: rawBreakdown.finalTotal,
        currency: rawBreakdown.currency ?? "TRY",
        depositAmount: rawBreakdown.depositAmount ?? 0,
        appliedCampaignCode: rawBreakdown.appliedCampaignCode
      }
    : reservation.priceBreakdown;

  return {
    ...reservation,
    reservationCode: reservation.reservationCode || raw.publicCode || reservation.id,
    customer: reservation.customer || {
      id: raw.customerId || "",
      name: reservation.customerName,
      firstName:
        reservation.customerName?.split(" ").slice(0, -1).join(" ") ||
        reservation.customerName ||
        "",
      lastName: reservation.customerName?.split(" ").slice(-1).join(" ") || "",
      email: raw.customerEmail,
      phone: raw.customerPhone,
      reservationCount: raw.customerReservationCount ?? 0,
      totalSpent: raw.customerTotalSpent ?? 0,
      createdAt: reservation.createdAt
    },
    customerName: reservation.customerName || reservation.customer?.name || "",
    vehicleName,
    vehicle: reservation.vehicle || {
      id: reservation.vehicleId,
      name: vehicleName,
      plate: raw.vehiclePlate
    },
    pickupDate: reservation.pickupDate || pickup.date,
    pickupTime: reservation.pickupTime || pickup.time,
    returnDate: reservation.returnDate || dropoff.date,
    returnTime: reservation.returnTime || dropoff.time,
    totalPrice: reservation.totalPrice ?? raw.totalAmount ?? 0,
    adminNotes: reservation.adminNotes ?? reservation.notes,
    driver: reservation.driver || raw.driver,
    priceBreakdown: normalizedPriceBreakdown
  };
}

export async function getReservations(params?: ReservationListParams | Record<string, unknown>) {
  const response = await adminGet<AdminPaginatedResponse<AdminReservation>>(
    `${RESERVATIONS_ENDPOINT}${buildQueryString(params)}`
  );
  const paginated = unwrapPaginated(response);
  return {
    ...paginated,
    items: paginated.items.map(normalizeReservation)
  };
}

export async function getReservationById(id: string) {
  const response = await adminGet<AdminResponse<AdminReservation>>(
    `${RESERVATIONS_ENDPOINT}/${id}`
  );
  return normalizeReservation(unwrapResponse(response));
}

export async function createManualReservation(data: AdminManualReservationData) {
  const response = await adminPost<AdminResponse<AdminReservation>>(
    `${RESERVATIONS_ENDPOINT}/manual`,
    data
  );
  return normalizeReservation(unwrapResponse(response));
}

export async function updateReservation(id: string, data: AdminUpdateReservationData) {
  const response = await adminPut<AdminResponse<AdminReservation>>(
    `${RESERVATIONS_ENDPOINT}/${id}`,
    data
  );
  return normalizeReservation(unwrapResponse(response));
}

export async function confirmUnpaidRequest(id: string) {
  const response = await adminPost<AdminResponse<AdminReservation>>(
    `${RESERVATIONS_ENDPOINT}/${id}/confirm-unpaid-request`,
    {}
  );
  return normalizeReservation(unwrapResponse(response));
}

export async function cancelReservation(
  id: string,
  reason: CancelReservationData["reason"] | CancelReservationData
) {
  const payload = typeof reason === "string" ? reason : reason.reason;
  const response = await adminPost<AdminResponse<AdminReservation>>(
    `${RESERVATIONS_ENDPOINT}/${id}/cancel`,
    payload
  );
  return normalizeReservation(unwrapResponse(response));
}

export async function assignVehicle(id: string, vehicleId: AssignVehicleData["vehicleId"]) {
  const response = await adminPatch<AdminResponse<AdminReservation>>(
    `${RESERVATIONS_ENDPOINT}/${id}/assign-vehicle`,
    { vehicleId }
  );
  return normalizeReservation(unwrapResponse(response));
}

export async function checkIn(id: string, data: ReservationCheckInData) {
  const response = await adminPatch<AdminResponse<AdminReservation>>(
    `${RESERVATIONS_ENDPOINT}/${id}/check-in`,
    data
  );
  return normalizeReservation(unwrapResponse(response));
}

export async function checkOut(id: string, data: ReservationCheckOutData) {
  const response = await adminPatch<AdminResponse<AdminReservation>>(
    `${RESERVATIONS_ENDPOINT}/${id}/check-out`,
    data
  );
  return normalizeReservation(unwrapResponse(response));
}

export async function refundReservation(id: string, data: AdminRefundData) {
  const response = await adminPost<AdminResponse<AdminPaymentOperation>>(
    `${RESERVATIONS_ENDPOINT}/${id}/refund`,
    data
  );
  return unwrapResponse(response);
}
