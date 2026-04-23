import { get } from './client';
import { API_ENDPOINTS } from './config';
import type {
  AvailableVehiclesParams,
  Office,
  PaginatedResponse,
  Vehicle,
  VehicleGroup,
} from './types';

function buildQueryString(params: Record<string, string | number | boolean | undefined>): string {
  const searchParams = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      searchParams.append(key, String(value));
    }
  });
  const query = searchParams.toString();
  return query ? `?${query}` : '';
}

export async function getAvailableVehicles(
  params: AvailableVehiclesParams
): Promise<PaginatedResponse<Vehicle>> {
  const queryString = buildQueryString({
    pickupOfficeId: params.pickupOfficeId,
    pickupDate: params.pickupDate,
    pickupTime: params.pickupTime,
    returnOfficeId: params.returnOfficeId,
    returnDate: params.returnDate,
    returnTime: params.returnTime,
    groupId: params.groupId,
    transmission: params.transmission,
    fuelType: params.fuelType,
    minSeats: params.minSeats,
    campaignCode: params.campaignCode,
  });

  return get<PaginatedResponse<Vehicle>>(`${API_ENDPOINTS.vehicles.list}${queryString}`);
}

export async function getVehicleGroups(): Promise<VehicleGroup[]> {
  return get<VehicleGroup[]>(API_ENDPOINTS.vehicles.groups);
}

export async function getVehicleById(id: string): Promise<Vehicle> {
  return get<Vehicle>(API_ENDPOINTS.vehicles.detail(id));
}

export async function getOffices(): Promise<Office[]> {
  return get<Office[]>(API_ENDPOINTS.offices.list);
}
