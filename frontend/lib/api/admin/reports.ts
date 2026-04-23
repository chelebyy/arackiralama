import { get } from '../client';
import { mockOccupancyReports, mockPopularVehicles, mockRevenueReports } from './mock';
import type {
  AdminResponse,
  OccupancyReport,
  PopularVehicleReportItem,
  RevenueReport,
} from './types';

const USE_MOCK = true;

function unwrapResponse<T>(response: AdminResponse<T>): T {
  if (response && typeof response === 'object' && 'data' in response) {
    return (response as { data: T }).data;
  }
  return response as T;
}

const REPORTS_ENDPOINT = '/admin/v1/reports';

export async function getRevenueReport(period: string) {
  if (USE_MOCK) {
    return mockRevenueReports[0];
  }
  const response = await get<AdminResponse<RevenueReport>>(`${REPORTS_ENDPOINT}/revenue?period=${period}`);
  return unwrapResponse(response);
}

export async function getOccupancyReport(period: string) {
  if (USE_MOCK) {
    return mockOccupancyReports[0];
  }
  const response = await get<AdminResponse<OccupancyReport>>(`${REPORTS_ENDPOINT}/occupancy?period=${period}`);
  return unwrapResponse(response);
}

export async function getPopularVehicles(period: string) {
  if (USE_MOCK) {
    return mockPopularVehicles;
  }
  const response = await get<AdminResponse<PopularVehicleReportItem[]>>(`${REPORTS_ENDPOINT}/popular-vehicles?period=${period}`);
  return unwrapResponse(response);
}
