'use client';

import useSWR from 'swr';
import {
  getReservations,
  getReservationById,
  createManualReservation,
  updateReservation,
  confirmUnpaidRequest,
  cancelReservation,
  assignVehicle,
  checkIn,
  checkOut,
  refundReservation,
} from '@/lib/api/admin/reservations';
import type {
  AdminRefundData,
  AdminManualReservationData,
  AdminReservation,
  PaginatedResponse,
} from '@/lib/api/admin';

const RESERVATION_KEYS = {
  all: ['admin', 'reservations'] as const,
  lists: () => [...RESERVATION_KEYS.all, 'list'] as const,
  list: (params?: Record<string, unknown>) =>
    [...RESERVATION_KEYS.lists(), params ?? 'all'] as const,
  details: () => [...RESERVATION_KEYS.all, 'detail'] as const,
  detail: (id: string) => [...RESERVATION_KEYS.details(), id] as const,
};

export function useAdminReservations(params?: Record<string, unknown>) {
  const { data, error, isLoading, mutate } = useSWR<
    PaginatedResponse<AdminReservation>,
    Error
  >(RESERVATION_KEYS.list(params), () => getReservations(params), {
    revalidateOnFocus: false,
    dedupingInterval: 10000,
  });

  return {
    reservations: data?.items ?? [],
    pagination: data
      ? {
          page: data.page,
          pageSize: data.pageSize,
          totalCount: data.totalCount,
          totalPages: data.totalPages,
        }
      : null,
    isLoading,
    isError: error,
    mutate,
  };
}

export function useAdminReservation(id: string | null) {
  const { data, error, isLoading, mutate } = useSWR<AdminReservation, Error>(
    id ? RESERVATION_KEYS.detail(id) : null,
    () => getReservationById(id!),
    { revalidateOnFocus: false }
  );

  return { reservation: data, isLoading, isError: error, mutate };
}

export async function mutateCancelReservation(
  id: string,
  data: Parameters<typeof cancelReservation>[1]
) {
  return cancelReservation(id, data);
}

export async function mutateAssignVehicle(id: string, vehicleId: string) {
  return assignVehicle(id, vehicleId);
}

export async function mutateCreateManualReservation(data: AdminManualReservationData) {
  return createManualReservation(data);
}

export async function mutateUpdateReservation(
  id: string,
  data: Parameters<typeof updateReservation>[1]
) {
  return updateReservation(id, data);
}

export async function mutateConfirmUnpaidRequest(id: string) {
  return confirmUnpaidRequest(id);
}

export async function mutateCheckIn(id: string, data: Parameters<typeof checkIn>[1]) {
  return checkIn(id, data);
}

export async function mutateCheckOut(id: string, data: Parameters<typeof checkOut>[1]) {
  return checkOut(id, data);
}

export async function mutateRefundReservation(id: string, data: AdminRefundData) {
  return refundReservation(id, data);
}
