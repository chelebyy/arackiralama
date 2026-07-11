import { get, post } from './client';
import { API_ENDPOINTS } from './config';
import type {
  PublicReservationExtraOption,
  ReservationQuote,
  SelectedBookingExtra,
} from './types';

export interface CreateReservationQuoteData {
  vehicleGroupId: string;
  pickupOfficeId: string;
  returnOfficeId: string;
  pickupDateTimeUtc: string;
  returnDateTimeUtc: string;
  campaignCode?: string;
  driverAge?: number;
  fullCoverageWaiver: boolean;
  locale: string;
  selectedExtras: Array<Pick<SelectedBookingExtra, 'optionId' | 'quantity' | 'optionVersion'>>;
}

export async function getPublicReservationExtraOptions(
  vehicleGroupId: string,
  locale: string
): Promise<PublicReservationExtraOption[]> {
  const query = new URLSearchParams({ vehicleGroupId, locale });
  const response = await get<{ items: PublicReservationExtraOption[] }>(
    `${API_ENDPOINTS.reservationExtras.catalog}?${query.toString()}`,
    { cache: 'no-store' }
  );

  return response.items;
}

export async function createReservationQuote(
  data: CreateReservationQuoteData,
  sessionId: string
): Promise<ReservationQuote> {
  return post<ReservationQuote>(API_ENDPOINTS.pricing.quote, data, {
    cache: 'no-store',
    headers: { 'X-Session-Id': sessionId },
  });
}
