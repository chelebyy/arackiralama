"use client";

import useSWR from "swr";
import {
  createReservationExtraOption,
  deleteReservationExtraOption,
  getReservationExtraOptions,
  restoreReservationExtraOption,
  updateReservationExtraOption,
  updateReservationExtraOptionStatus
} from "@/lib/api/admin/reservationExtras";
import type {
  ReservationExtraOptionListParams,
  ReservationExtraOptionListResponse
} from "@/lib/api/admin/reservationExtras";

export const RESERVATION_EXTRA_KEYS = {
  all: ["admin", "reservationExtras"] as const,
  lists: () => [...RESERVATION_EXTRA_KEYS.all, "list"] as const,
  list: (params: ReservationExtraOptionListParams = {}) =>
    [
      ...RESERVATION_EXTRA_KEYS.lists(),
      params.search ?? "",
      params.status ?? "all",
      params.vehicleGroupId ?? "",
      params.includeArchived ?? false,
      params.page ?? 1,
      params.pageSize ?? 20
    ] as const
};

export function useAdminReservationExtras(params: ReservationExtraOptionListParams = {}) {
  const { data, error, isLoading, mutate } = useSWR<ReservationExtraOptionListResponse, Error>(
    RESERVATION_EXTRA_KEYS.list(params),
    () => getReservationExtraOptions(params),
    { revalidateOnFocus: false, keepPreviousData: true }
  );

  return {
    options: data?.items ?? [],
    pagination: data
      ? {
          page: data.page,
          pageSize: data.pageSize,
          totalCount: data.totalCount,
          totalPages: Math.max(1, Math.ceil(data.totalCount / data.pageSize))
        }
      : null,
    isLoading,
    isError: error,
    mutate
  };
}

export async function mutateCreateReservationExtraOption(
  data: Parameters<typeof createReservationExtraOption>[0]
) {
  return createReservationExtraOption(data);
}

export async function mutateUpdateReservationExtraOption(
  id: string,
  data: Parameters<typeof updateReservationExtraOption>[1]
) {
  return updateReservationExtraOption(id, data);
}

export async function mutateUpdateReservationExtraOptionStatus(
  id: string,
  data: Parameters<typeof updateReservationExtraOptionStatus>[1]
) {
  return updateReservationExtraOptionStatus(id, data);
}

export async function mutateDeleteReservationExtraOption(id: string, version: number) {
  return deleteReservationExtraOption(id, version);
}

export async function mutateRestoreReservationExtraOption(id: string, version: number) {
  return restoreReservationExtraOption(id, version);
}
