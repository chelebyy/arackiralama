import { adminGet, adminPatch, adminPost } from '../client';
import type {
  AdminPaginatedResponse,
  AdminPaymentOperation,
  AdminReservation,
  AdminResponse,
  AssignVehicleData,
  CancelReservationData,
  AdminRefundData,
  PaginatedResponse,
  ReservationCheckInData,
  ReservationCheckOutData,
  ReservationListParams,
} from './types';

const RESERVATIONS_ENDPOINT = '/v1/reservations';

function buildQueryString(params?: object): string {
  if (!params) return '';
  const searchParams = new URLSearchParams();

  Object.entries(params).forEach(([key, value]) => {
    if (
      value !== undefined &&
      value !== null &&
      value !== '' &&
      (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean')
    ) {
      searchParams.append(key, String(value));
    }
  });

  const queryString = searchParams.toString();
  return queryString ? `?${queryString}` : '';
}

function unwrapResponse<T>(response: AdminResponse<T>): T {
  if (response && typeof response === 'object' && 'data' in response) {
    return (response as { data: T }).data;
  }
  return response as T;
}

function unwrapPaginated<T>(response: AdminPaginatedResponse<T>) {
  if (response && typeof response === 'object' && 'data' in response) {
    return (response as { data: PaginatedResponse<T> }).data;
  }
  return response as PaginatedResponse<T>;
}

export async function getReservations(params?: ReservationListParams | Record<string, unknown>) {
  const response = await adminGet<AdminPaginatedResponse<AdminReservation>>(`${RESERVATIONS_ENDPOINT}${buildQueryString(params)}`);
  return unwrapPaginated(response);
}

export async function getReservationById(id: string) {
  const response = await adminGet<AdminResponse<AdminReservation>>(`${RESERVATIONS_ENDPOINT}/${id}`);
  return unwrapResponse(response);
}

export async function cancelReservation(
  id: string,
  reason: CancelReservationData['reason'] | CancelReservationData
) {
  const payload = typeof reason === 'string' ? { reason } : reason;
  const response = await adminPatch<AdminResponse<AdminReservation>>(`${RESERVATIONS_ENDPOINT}/${id}/cancel`, payload);
  return unwrapResponse(response);
}

export async function assignVehicle(id: string, vehicleId: AssignVehicleData['vehicleId']) {
  const response = await adminPatch<AdminResponse<AdminReservation>>(`${RESERVATIONS_ENDPOINT}/${id}/assign-vehicle`, { vehicleId });
  return unwrapResponse(response);
}

export async function checkIn(id: string, data: ReservationCheckInData) {
  const response = await adminPatch<AdminResponse<AdminReservation>>(`${RESERVATIONS_ENDPOINT}/${id}/check-in`, data);
  return unwrapResponse(response);
}

export async function checkOut(id: string, data: ReservationCheckOutData) {
  const response = await adminPatch<AdminResponse<AdminReservation>>(`${RESERVATIONS_ENDPOINT}/${id}/check-out`, data);
  return unwrapResponse(response);
}

export async function refundReservation(
  id: string,
  data: AdminRefundData
) {
  const response = await adminPost<AdminResponse<AdminPaymentOperation>>(`${RESERVATIONS_ENDPOINT}/${id}/refund`, data);
  return unwrapResponse(response);
}
