import useSWR from 'swr';
import useSWRInfinite from 'swr/infinite';
import {
  getAvailableVehicles,
  getOffices,
  getVehicleById,
  getVehicleGroups,
} from '@/lib/api/vehicles';
import type {
  AvailableVehiclesParams,
  Office,
  PaginatedResponse,
  Vehicle,
  VehicleGroup,
} from '@/lib/api/types';

const VEHICLE_KEYS = {
  all: ['vehicles'] as const,
  lists: () => [...VEHICLE_KEYS.all, 'list'] as const,
  list: (params: AvailableVehiclesParams) =>
    [...VEHICLE_KEYS.lists(), params] as const,
  groups: () => [...VEHICLE_KEYS.all, 'groups'] as const,
  details: () => [...VEHICLE_KEYS.all, 'detail'] as const,
  detail: (id: string) => [...VEHICLE_KEYS.details(), id] as const,
};

const OFFICE_KEYS = {
  all: ['offices'] as const,
  list: () => [...OFFICE_KEYS.all, 'list'] as const,
};

export function useAvailableVehicles(params: AvailableVehiclesParams | null) {
  const { data, error, isLoading, mutate } = useSWR<
    PaginatedResponse<Vehicle>,
    Error
  >(
    params ? VEHICLE_KEYS.list(params) : null,
    () => getAvailableVehicles(params!),
    {
      revalidateOnFocus: false,
      revalidateIfStale: false,
      dedupingInterval: 5000,
    }
  );

  return {
    vehicles: data?.items ?? [],
    pagination: data
      ? {
          page: data.page,
          pageSize: data.pageSize,
          totalCount: data.totalCount,
          totalPages: data.totalPages,
          hasNextPage: data.hasNextPage,
          hasPreviousPage: data.hasPreviousPage,
        }
      : null,
    isLoading,
    isError: error,
    mutate,
  };
}

export function useVehicleGroups() {
  const { data, error, isLoading } = useSWR<VehicleGroup[], Error>(
    VEHICLE_KEYS.groups(),
    getVehicleGroups,
    {
      revalidateOnFocus: false,
      dedupingInterval: 60000,
    }
  );

  return {
    groups: data ?? [],
    isLoading,
    isError: error,
  };
}

export function useVehicle(id: string | null) {
  const { data, error, isLoading, mutate } = useSWR<Vehicle, Error>(
    id ? VEHICLE_KEYS.detail(id) : null,
    () => getVehicleById(id!),
    {
      revalidateOnFocus: false,
    }
  );

  return {
    vehicle: data,
    isLoading,
    isError: error,
    mutate,
  };
}

export function useOffices() {
  const { data, error, isLoading } = useSWR<Office[], Error>(
    OFFICE_KEYS.list(),
    getOffices,
    {
      revalidateOnFocus: false,
      dedupingInterval: 300000,
    }
  );

  return {
    offices: data ?? [],
    isLoading,
    isError: error,
  };
}

export function useInfiniteVehicles(params: Omit<AvailableVehiclesParams, 'page'>) {
  const getKey = (pageIndex: number, previousPageData: PaginatedResponse<Vehicle> | null) => {
    if (previousPageData && !previousPageData.hasNextPage) return null;
    const pageParams: AvailableVehiclesParams = { ...params, page: pageIndex + 1 };
    return VEHICLE_KEYS.list(pageParams);
  };

  const fetcher = async (key: ReturnType<typeof getKey>): Promise<PaginatedResponse<Vehicle>> => {
    if (!key) throw new Error('Invalid key');
    const pageParams = key[key.length - 1] as AvailableVehiclesParams;
    return getAvailableVehicles(pageParams);
  };

  const { data, error, isLoading, size, setSize } = useSWRInfinite<
    PaginatedResponse<Vehicle>,
    Error
  >(getKey, fetcher, {
    revalidateOnFocus: false,
  });

  const vehicles = data?.flatMap((page) => page.items) ?? [];
  const hasMore = data?.[data.length - 1]?.hasNextPage ?? false;
  const isLoadingMore = isLoading || (size > 0 && data && typeof data[size - 1] === 'undefined');

  return {
    vehicles,
    isLoading,
    isLoadingMore,
    isError: error,
    size,
    setSize,
    hasMore,
  };
}
