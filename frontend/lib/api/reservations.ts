import { get, post, patch } from './client';
import { API_ENDPOINTS } from './config';
import type {
  CreateReservationData,
  ExtendHoldData,
  HoldReservationData,
  ReservationRequestOptions,
  Reservation,
} from './types';

function requestHeaders(options?: ReservationRequestOptions) {
  if (!options) return undefined;

  return {
    'X-Session-Id': options.sessionId,
    'Idempotency-Key': options.idempotencyKey,
  };
}

export async function createReservation(
  data: CreateReservationData,
  options?: ReservationRequestOptions
): Promise<Reservation> {
  return options
    ? post<Reservation>(API_ENDPOINTS.reservations.create, data, { headers: requestHeaders(options) })
    : post<Reservation>(API_ENDPOINTS.reservations.create, data);
}

export async function createUnpaidReservationRequest(
  data: CreateReservationData,
  options?: ReservationRequestOptions
): Promise<Reservation> {
  return options
    ? post<Reservation>(API_ENDPOINTS.reservations.createUnpaidRequest, data, { headers: requestHeaders(options) })
    : post<Reservation>(API_ENDPOINTS.reservations.createUnpaidRequest, data);
}

export async function getReservationByPublicCode(code: string): Promise<Reservation> {
  return get<Reservation>(API_ENDPOINTS.reservations.detail(code));
}

export async function placeHold(
  reservationId: string,
  sessionId: string,
  data?: HoldReservationData
): Promise<Reservation> {
  return post<Reservation>(API_ENDPOINTS.reservations.hold(reservationId), data, {
    headers: { 'X-Session-Id': sessionId },
  });
}

export async function extendHold(
  reservationId: string,
  data: ExtendHoldData
): Promise<Reservation> {
  return patch<Reservation>(API_ENDPOINTS.reservations.extendHold(reservationId), data);
}
