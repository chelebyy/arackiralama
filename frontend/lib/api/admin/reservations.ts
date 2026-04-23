import { get, patch } from '../client';
import { mockReservations } from './mock';
import type {
  AdminPaginatedResponse,
  AdminReservation,
  AdminResponse,
  AssignVehicleData,
  CancelReservationData,
  PaginatedResponse,
  ReservationCheckInData,
  ReservationCheckOutData,
  ReservationListParams,
} from './types';

const USE_MOCK = true;

const RESERVATIONS_ENDPOINT = '/admin/v1/reservations';

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

function createMockPaginated<T>(items: T[]): PaginatedResponse<T> {
  return {
    items,
    page: 1,
    pageSize: items.length,
    totalCount: items.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
  };
}


export async function getReservations(params?: ReservationListParams | Record<string, unknown>) {
  if (USE_MOCK) {
    return createMockPaginated(mockReservations);
  }
  const response = await get<AdminPaginatedResponse<AdminReservation>>(`${RESERVATIONS_ENDPOINT}${buildQueryString(params)}`);
  return unwrapPaginated(response);
}

export async function getReservationById(id: string) {
  if (USE_MOCK) {
    return mockReservations.find((reservation) => reservation.id === id) || mockReservations[0];
  }
  const response = await get<AdminResponse<AdminReservation>>(`${RESERVATIONS_ENDPOINT}/${id}`);
  return unwrapResponse(response);
}

export async function cancelReservation(
  id: string,
  reason: CancelReservationData['reason'] | CancelReservationData
) {
  if (USE_MOCK) {
    return mockReservations[0];
  }
  const payload = typeof reason === 'string' ? { reason } : reason;
  const response = await patch<AdminResponse<AdminReservation>>(`${RESERVATIONS_ENDPOINT}/${id}/cancel`, payload);
  return unwrapResponse(response);
}

export async function assignVehicle(id: string, vehicleId: AssignVehicleData['vehicleId']) {
  if (USE_MOCK) {
    return mockReservations[0];
  }
  const response = await patch<AdminResponse<AdminReservation>>(`${RESERVATIONS_ENDPOINT}/${id}/assign-vehicle`, { vehicleId });
  return unwrapResponse(response);
}

export async function checkIn(id: string, data: ReservationCheckInData) {
  if (USE_MOCK) {
    return mockReservations[0];
  }
  const response = await patch<AdminResponse<AdminReservation>>(`${RESERVATIONS_ENDPOINT}/${id}/check-in`, data);
  return unwrapResponse(response);
}

export async function checkOut(id: string, data: ReservationCheckOutData) {
  if (USE_MOCK) {
    return mockReservations[0];
  }
  const response = await patch<AdminResponse<AdminReservation>>(`${RESERVATIONS_ENDPOINT}/${id}/check-out`, data);
  return unwrapResponse(response);
}
