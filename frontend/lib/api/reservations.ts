import { get, post, patch } from './client';
import { API_ENDPOINTS } from './config';
import type {
  CreateReservationData,
  ExtendHoldData,
  HoldReservationData,
  Reservation,
} from './types';

export async function createReservation(data: CreateReservationData): Promise<Reservation> {
  return post<Reservation>(API_ENDPOINTS.reservations.create, data);
}

export async function getReservationByPublicCode(code: string): Promise<Reservation> {
  return get<Reservation>(API_ENDPOINTS.reservations.detail(code));
}

export async function placeHold(
  reservationId: string,
  data?: HoldReservationData
): Promise<Reservation> {
  return post<Reservation>(API_ENDPOINTS.reservations.hold(reservationId), data);
}

export async function extendHold(
  reservationId: string,
  data: ExtendHoldData
): Promise<Reservation> {
  return patch<Reservation>(API_ENDPOINTS.reservations.extendHold(reservationId), data);
}
