import { adminGet } from '../client';
import type {
  AdminResponse,
  OccupancyReport,
  PopularVehicleReportItem,
  RevenueReport,
} from './types';

function unwrapResponse<T>(response: AdminResponse<T>): T {
  if (response && typeof response === 'object' && 'data' in response) {
    return (response as { data: T }).data;
  }
  return response as T;
}

const REPORTS_ENDPOINT = '/v1/reports';

export async function getRevenueReport(period: string) {
  const response = await adminGet<AdminResponse<RevenueReport>>(`${REPORTS_ENDPOINT}/revenue?period=${period}`);
  return unwrapResponse(response);
}

export async function getOccupancyReport(period: string) {
  const response = await adminGet<AdminResponse<OccupancyReport>>(`${REPORTS_ENDPOINT}/occupancy?period=${period}`);
  return unwrapResponse(response);
}

export async function getPopularVehicles(period: string) {
  const response = await adminGet<AdminResponse<PopularVehicleReportItem[]>>(`${REPORTS_ENDPOINT}/popular-vehicles?period=${period}`);
  return unwrapResponse(response);
}
