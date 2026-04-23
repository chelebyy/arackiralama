'use client';

import useSWR from 'swr';
import {
  getVehicles,
  getVehicleById,
  getVehicleGroups,
  getOffices,
  createVehicle,
  updateVehicle,
  deleteVehicle,
  updateVehicleStatus,
  transferVehicle,
  scheduleMaintenance,
  createVehicleGroup,
  updateVehicleGroup,
  createOffice,
  updateOffice,
} from '@/lib/api/admin/vehicles';
import type {
  AdminVehicle,
  AdminVehicleGroup,
  AdminOffice,
  PaginatedResponse,
} from '@/lib/api/admin';

const VEHICLE_KEYS = {
  all: ['admin', 'vehicles'] as const,
  lists: () => [...VEHICLE_KEYS.all, 'list'] as const,
  list: (params?: Record<string, unknown>) =>
    [...VEHICLE_KEYS.lists(), params ?? 'all'] as const,
  details: () => [...VEHICLE_KEYS.all, 'detail'] as const,
  detail: (id: string) => [...VEHICLE_KEYS.details(), id] as const,
  groups: () => ['admin', 'vehicleGroups'] as const,
  offices: () => ['admin', 'offices'] as const,
};

export function useAdminVehicles(params?: Record<string, unknown>) {
  const { data, error, isLoading, mutate } = useSWR<
    PaginatedResponse<AdminVehicle>,
    Error
  >(VEHICLE_KEYS.list(params), () => getVehicles(params), {
    revalidateOnFocus: false,
    dedupingInterval: 10000,
  });

  return {
    vehicles: data?.items ?? [],
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

export function useAdminVehicle(id: string | null) {
  const { data, error, isLoading, mutate } = useSWR<AdminVehicle, Error>(
    id ? VEHICLE_KEYS.detail(id) : null,
    () => getVehicleById(id!),
    { revalidateOnFocus: false }
  );

  return { vehicle: data, isLoading, isError: error, mutate };
}

export function useAdminVehicleGroups() {
  const { data, error, isLoading, mutate } = useSWR<
    PaginatedResponse<AdminVehicleGroup>,
    Error
  >(VEHICLE_KEYS.groups(), getVehicleGroups, {
    revalidateOnFocus: false,
    dedupingInterval: 60000,
  });

  return { groups: data?.items ?? [], isLoading, isError: error, mutate };
}

export function useAdminOffices() {
  const { data, error, isLoading, mutate } = useSWR<
    PaginatedResponse<AdminOffice>,
    Error
  >(VEHICLE_KEYS.offices(), getOffices, {
    revalidateOnFocus: false,
    dedupingInterval: 60000,
  });

  return { offices: data?.items ?? [], isLoading, isError: error, mutate };
}

export async function mutateCreateVehicle(data: Parameters<typeof createVehicle>[0]) {
  return createVehicle(data);
}

export async function mutateUpdateVehicle(
  id: string,
  data: Parameters<typeof updateVehicle>[1]
) {
  return updateVehicle(id, data);
}

export async function mutateDeleteVehicle(id: string) {
  await deleteVehicle(id);
}

export async function mutateUpdateVehicleStatus(
  id: string,
  data: Parameters<typeof updateVehicleStatus>[1]
) {
  return updateVehicleStatus(id, data);
}

export async function mutateTransferVehicle(
  id: string,
  data: Parameters<typeof transferVehicle>[1]
) {
  return transferVehicle(id, data);
}

export async function mutateScheduleMaintenance(
  id: string,
  data: Parameters<typeof scheduleMaintenance>[1]
) {
  return scheduleMaintenance(id, data);
}

export async function mutateCreateVehicleGroup(data: Parameters<typeof createVehicleGroup>[0]) {
  return createVehicleGroup(data);
}

export async function mutateUpdateVehicleGroup(
  id: string,
  data: Parameters<typeof updateVehicleGroup>[1]
) {
  return updateVehicleGroup(id, data);
}

export async function mutateCreateOffice(data: Parameters<typeof createOffice>[0]) {
  return createOffice(data);
}

export async function mutateUpdateOffice(id: string, data: Parameters<typeof updateOffice>[1]) {
  return updateOffice(id, data);
}
