'use client';

import useSWR from 'swr';
import {
  getFeatureFlags,
  updateFeatureFlag,
  getAuditLogs,
} from '@/lib/api/admin/settings';
import type { FeatureFlag, AuditLog, PaginatedResponse } from '@/lib/api/admin';

const SETTINGS_KEYS = {
  featureFlags: () => ['admin', 'featureFlags'] as const,
  auditLogs: (params?: Record<string, unknown>) =>
    ['admin', 'auditLogs', params ?? 'all'] as const,
};

export function useFeatureFlags() {
  const { data, error, isLoading, mutate } = useSWR<FeatureFlag[], Error>(
    SETTINGS_KEYS.featureFlags(),
    getFeatureFlags,
    { revalidateOnFocus: false, dedupingInterval: 30000 }
  );

  return { flags: data ?? [], isLoading, isError: error, mutate };
}

export function useAuditLogs(params?: Record<string, unknown>) {
  const { data, error, isLoading, mutate } = useSWR<
    PaginatedResponse<AuditLog>,
    Error
  >(SETTINGS_KEYS.auditLogs(params), () => getAuditLogs(params), {
    revalidateOnFocus: false,
    dedupingInterval: 30000,
  });

  return {
    logs: data?.items ?? [],
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

export async function mutateUpdateFeatureFlag(id: string, enabled: boolean) {
  return updateFeatureFlag(id, enabled);
}
