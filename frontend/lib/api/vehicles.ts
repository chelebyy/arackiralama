import { get } from './client';
import { API_ENDPOINTS } from './config';
import type {
  AvailableVehicleGroup,
  AvailableVehiclesParams,
  Office,
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
): Promise<AvailableVehicleGroup[]> {
  const queryString = buildQueryString({
    office_id: params.office_id,
    pickup_datetime: params.pickup_datetime,
    return_datetime: params.return_datetime,
    vehicle_group_id: params.vehicle_group_id,
  });

  return get<AvailableVehicleGroup[]>(`${API_ENDPOINTS.vehicles.list}${queryString}`);
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
