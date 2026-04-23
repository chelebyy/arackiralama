'use client';

import useSWR from 'swr';
import {
  getRevenueReport,
  getOccupancyReport,
  getPopularVehicles,
} from '@/lib/api/admin/reports';
import type {
  RevenueReport,
  OccupancyReport,
  PopularVehicleReport,
} from '@/lib/api/admin';

const REPORT_KEYS = {
  revenue: (period: string) => ['admin', 'reports', 'revenue', period] as const,
  occupancy: (period: string) => ['admin', 'reports', 'occupancy', period] as const,
  popular: (period: string) => ['admin', 'reports', 'popular', period] as const,
};

export function useRevenueReport(period: string) {
  const { data, error, isLoading } = useSWR<RevenueReport, Error>(
    REPORT_KEYS.revenue(period),
    () => getRevenueReport(period),
    { revalidateOnFocus: false }
  );

  return { report: data, isLoading, isError: error };
}

export function useOccupancyReport(period: string) {
  const { data, error, isLoading } = useSWR<OccupancyReport, Error>(
    REPORT_KEYS.occupancy(period),
    () => getOccupancyReport(period),
    { revalidateOnFocus: false }
  );

  return { report: data, isLoading, isError: error };
}

export function usePopularVehicles(period: string) {
  const { data, error, isLoading } = useSWR<PopularVehicleReport[], Error>(
    REPORT_KEYS.popular(period),
    () => getPopularVehicles(period),
    { revalidateOnFocus: false }
  );

  return { vehicles: data ?? [], isLoading, isError: error };
}
