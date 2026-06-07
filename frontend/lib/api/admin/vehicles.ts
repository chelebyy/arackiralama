import { adminDel, adminGet, adminPatch, adminPost, adminPostFormData, adminPut } from '../client';
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

const ADMIN_BASE = '/v1';
const VEHICLES_ENDPOINT = `${ADMIN_BASE}/vehicles`;
const VEHICLE_GROUPS_ENDPOINT = `${ADMIN_BASE}/vehicle-groups`;
const OFFICES_ENDPOINT = `${ADMIN_BASE}/offices`;

function toBackendVehicleStatus(status: AdminVehicleStatus | number) {
  if (typeof status === 'number') return status;
  switch (status) {
    case 'Available':
      return 0;
    case 'Reserved':
      return 1;
    case 'Rented':
      return 2;
    case 'Maintenance':
      return 3;
    case 'OutOfService':
      return 4;
    case 'Retired':
      return 5;
    default:
      return 0;
  }
}

function withBackendVehicleStatus<T extends { status?: AdminVehicleStatus | number }>(data: T) {
  if (data.status === undefined) return data;
  return {
    ...data,
    status: toBackendVehicleStatus(data.status),
  };
}

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
  if (Array.isArray(response)) {
    return createPaginated(response);
  }

  if (response && typeof response === 'object' && 'data' in response) {
    const data = (response as { data: PaginatedResponse<T> | T[] }).data;
    return Array.isArray(data) ? createPaginated(data) : data;
  }
  return response as PaginatedResponse<T>;
}

function createPaginated<T>(items: T[]): PaginatedResponse<T> {
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
  const response = await adminGet<AdminPaginatedResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}${buildQueryString(params)}`);
  return unwrapPaginated(response);
}

export async function getVehicleById(id: string) {
  const response = await adminGet<AdminResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}/${id}`);
  return unwrapResponse(response);
}

export async function createVehicle(data: CreateVehicleData) {
  const response = await adminPost<AdminResponse<AdminVehicle>>(VEHICLES_ENDPOINT, withBackendVehicleStatus(data));
  return unwrapResponse(response);
}

export async function updateVehicle(id: string, data: UpdateVehicleData) {
  const response = await adminPut<AdminResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}/${id}`, withBackendVehicleStatus(data));
  return unwrapResponse(response);
}

export async function uploadVehiclePhoto(id: string, file: File) {
  const formData = new FormData();
  formData.append('file', file);
  const response = await adminPostFormData<AdminResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}/${id}/photo`, formData);
  return unwrapResponse(response);
}

export async function deleteVehicle(id: string) {
  const response = await adminDel<AdminResponse<{ id: string; outcome?: 'Deleted' | 'Archived' }>>(`${VEHICLES_ENDPOINT}/${id}`);
  return unwrapResponse(response);
}

export async function updateVehicleStatus(id: string, status: AdminVehicleStatus) {
  const response = await adminPatch<AdminResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}/${id}/status`, {
    status: toBackendVehicleStatus(status),
  });
  return unwrapResponse(response);
}

export async function transferVehicle(id: string, officeId: TransferVehicleData['officeId']) {
  const response = await adminPatch<AdminResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}/${id}/transfer`, { officeId });
  return unwrapResponse(response);
}

export async function scheduleMaintenance(id: string, data: VehicleMaintenanceData) {
  const response = await adminPatch<AdminResponse<AdminVehicle>>(`${VEHICLES_ENDPOINT}/${id}/maintenance`, data);
  return unwrapResponse(response);
}

export async function getVehicleGroups() {
  const response = await adminGet<AdminPaginatedResponse<AdminVehicleGroup>>(VEHICLE_GROUPS_ENDPOINT);
  return unwrapPaginated(response);
}

export async function createVehicleGroup(data: CreateVehicleGroupData) {
  const response = await adminPost<AdminResponse<AdminVehicleGroup>>(VEHICLE_GROUPS_ENDPOINT, data);
  return unwrapResponse(response);
}

export async function updateVehicleGroup(id: string, data: UpdateVehicleGroupData) {
  const response = await adminPut<AdminResponse<AdminVehicleGroup>>(`${VEHICLE_GROUPS_ENDPOINT}/${id}`, data);
  return unwrapResponse(response);
}

export async function deleteVehicleGroup(id: string): Promise<void> {
  await adminDel<void>(`${VEHICLE_GROUPS_ENDPOINT}/${id}`);
}

export async function getOffices() {
  const response = await adminGet<AdminPaginatedResponse<AdminOffice>>(OFFICES_ENDPOINT);
  return unwrapPaginated(response);
}

export async function createOffice(data: CreateOfficeData) {
  const response = await adminPost<AdminResponse<AdminOffice>>(OFFICES_ENDPOINT, data);
  return unwrapResponse(response);
}

export async function updateOffice(id: string, data: UpdateOfficeData) {
  const response = await adminPut<AdminResponse<AdminOffice>>(`${OFFICES_ENDPOINT}/${id}`, data);
  return unwrapResponse(response);
}

export async function deleteOffice(id: string): Promise<void> {
  await adminDel<void>(`${OFFICES_ENDPOINT}/${id}`);
}
