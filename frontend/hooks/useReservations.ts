import useSWR, { useSWRConfig } from 'swr';
import { useCallback, useState } from 'react';
import {
  createReservation,
  extendHold,
  getReservationByPublicCode,
  placeHold,
} from '@/lib/api/reservations';
import type {
  CreateReservationData,
  ExtendHoldData,
  HoldReservationData,
  Reservation,
} from '@/lib/api/types';

const RESERVATION_KEYS = {
  all: ['reservations'] as const,
  details: () => [...RESERVATION_KEYS.all, 'detail'] as const,
  detail: (code: string) => [...RESERVATION_KEYS.details(), code] as const,
};

export function useReservation(code: string | null) {
  const { data, error, isLoading, mutate } = useSWR<Reservation, Error>(
    code ? RESERVATION_KEYS.detail(code) : null,
    () => getReservationByPublicCode(code!),
    {
      revalidateOnFocus: true,
      refreshInterval: 30000,
    }
  );

  return {
    reservation: data,
    isLoading,
    isError: error,
    mutate,
  };
}

export function useCreateReservation() {
  const [isCreating, setIsCreating] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const { mutate } = useSWRConfig();

  const create = useCallback(
    async (data: CreateReservationData): Promise<Reservation | null> => {
      setIsCreating(true);
      setError(null);
      try {
        const reservation = await createReservation(data);
        mutate(RESERVATION_KEYS.detail(reservation.publicCode), reservation, false);
        return reservation;
      } catch (err) {
        setError(err instanceof Error ? err : new Error('Failed to create reservation'));
        return null;
      } finally {
        setIsCreating(false);
      }
    },
    [mutate]
  );

  return {
    create,
    isCreating,
    error,
  };
}

export function usePlaceHold() {
  const [isPlacingHold, setIsPlacingHold] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const { mutate } = useSWRConfig();

  const placeHoldMutation = useCallback(
    async (
      reservationId: string,
      data?: HoldReservationData
    ): Promise<Reservation | null> => {
      setIsPlacingHold(true);
      setError(null);
      try {
        const reservation = await placeHold(reservationId, data);
        mutate(RESERVATION_KEYS.detail(reservation.publicCode), reservation, false);
        return reservation;
      } catch (err) {
        setError(err instanceof Error ? err : new Error('Failed to place hold'));
        return null;
      } finally {
        setIsPlacingHold(false);
      }
    },
    [mutate]
  );

  return {
    placeHold: placeHoldMutation,
    isPlacingHold,
    error,
  };
}

export function useExtendHold() {
  const [isExtending, setIsExtending] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const { mutate } = useSWRConfig();

  const extend = useCallback(
    async (reservationId: string, data: ExtendHoldData): Promise<Reservation | null> => {
      setIsExtending(true);
      setError(null);
      try {
        const reservation = await extendHold(reservationId, data);
        mutate(RESERVATION_KEYS.detail(reservation.publicCode), reservation, false);
        return reservation;
      } catch (err) {
        setError(err instanceof Error ? err : new Error('Failed to extend hold'));
        return null;
      } finally {
        setIsExtending(false);
      }
    },
    [mutate]
  );

  return {
    extend,
    isExtending,
    error,
  };
}
