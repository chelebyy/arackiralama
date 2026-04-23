import { del, get, patch, post, put } from '../client';
import { mockOffices, mockVehicleGroups, mockVehicles } from './mock';
import type {
  AdminOffice,
  AdminPaginatedResponse,
  AdminResponse,
  AdminVehicle,
  AdminVehicleGroup,
  CreateOfficeData,
  CreateVehicleData,
  CreateVehicleGroupData,
  PaginatedResponse,
  TransferVehicleData,
  UpdateOfficeData,
  UpdateVehicleData,
  UpdateVehicleGroupData,
  VehicleListParams,
  VehicleMaintenanceData,
  AdminVehicleStatus,
} from './types';

const USE_MOCK = process.env.NEXT_PUBLIC_USE_MOCK === 'true';

const ADMIN_BASE = '/admin/v1';
const VEHICLES_ENDPOINT = `${ADMIN_BASE}/vehicles`;
const VEHICLE_GROUPS_ENDPOINT = `${ADMIN_BASE}/vehicle-groups`;
const OFFICES_ENDPOINT = `${ADMIN_BASE}/offices`;

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


export async function getVehicles(params?: VehicleListParams | Record<string, unknown>) {
  if (USE_MOCK) {
    return createMockPaginated(mockVehicles);
  }
  const response = await get<AdminPaginatedResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}${buildQueryString(params)}`);
  return unwrapPaginated(response);
}

export async function getVehicleById(id: string) {
  if (USE_MOCK) {
    return mockVehicles.find((vehicle) => vehicle.id === id) || mockVehicles[0];
  }
  const response = await get<AdminResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}/${id}`);
  return unwrapResponse(response);
}

export async function createVehicle(data: CreateVehicleData) {
  if (USE_MOCK) {
    return mockVehicles[0];
  }
  const response = await post<AdminResponse<AdminVehicle>>(VEHICLES_ENDPOINT, data);
  return unwrapResponse(response);
}

export async function updateVehicle(id: string, data: UpdateVehicleData) {
  if (USE_MOCK) {
    return mockVehicles[0];
  }
  const response = await put<AdminResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}/${id}`, data);
  return unwrapResponse(response);
}

export async function deleteVehicle(id: string): Promise<void> {
  if (USE_MOCK) {
    return Promise.resolve();
  }
  await del<void>(`${VEHICLES_ENDPOINT}/${id}`);
}

export async function updateVehicleStatus(id: string, status: AdminVehicleStatus) {
  if (USE_MOCK) {
    return mockVehicles[0];
  }
  const response = await patch<AdminResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}/${id}/status`, { status });
  return unwrapResponse(response);
}

export async function transferVehicle(id: string, officeId: TransferVehicleData['officeId']) {
  if (USE_MOCK) {
    return mockVehicles[0];
  }
  const response = await patch<AdminResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}/${id}/transfer`, { officeId });
  return unwrapResponse(response);
}

export async function scheduleMaintenance(id: string, data: VehicleMaintenanceData) {
  if (USE_MOCK) {
    return mockVehicles[0];
  }
  const response = await patch<AdminResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}/${id}/maintenance`, data);
  return unwrapResponse(response);
}

export async function getVehicleGroups() {
  if (USE_MOCK) {
    return createMockPaginated(mockVehicleGroups);
  }
  const response = await get<AdminPaginatedResponse<AdminVehicleGroup>>(VEHICLE_GROUPS_ENDPOINT);
  return unwrapPaginated(response);
}

export async function createVehicleGroup(data: CreateVehicleGroupData) {
  if (USE_MOCK) {
    return mockVehicleGroups[0];
  }
  const response = await post<AdminResponse<AdminVehicleGroup>>(VEHICLE_GROUPS_ENDPOINT, data);
  return unwrapResponse(response);
}

export async function updateVehicleGroup(id: string, data: UpdateVehicleGroupData) {
  if (USE_MOCK) {
    return mockVehicleGroups[0];
  }
  const response = await put<AdminResponse<AdminVehicleGroup>>(`${VEHICLE_GROUPS_ENDPOINT}/${id}`, data);
  return unwrapResponse(response);
}

export async function getOffices() {
  if (USE_MOCK) {
    return createMockPaginated(mockOffices);
  }
  const response = await get<AdminPaginatedResponse<AdminOffice>>(OFFICES_ENDPOINT);
  return unwrapPaginated(response);
}

export async function createOffice(data: CreateOfficeData) {
  if (USE_MOCK) {
    return mockOffices[0];
  }
  const response = await post<AdminResponse<AdminOffice>>(OFFICES_ENDPOINT, data);
  return unwrapResponse(response);
}

export async function updateOffice(id: string, data: UpdateOfficeData) {
  if (USE_MOCK) {
    return mockOffices[0];
  }
  const response = await put<AdminResponse<AdminOffice>>(`${OFFICES_ENDPOINT}/${id}`, data);
  return unwrapResponse(response);
}
