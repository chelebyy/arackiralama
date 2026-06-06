'use client';

import useSWR from 'swr';
import {
  getFeatureFlags,
  updateFeatureFlag,
  getAuditLogs,
  getPublicSiteSettings,
  updatePublicSiteSettings,
} from '@/lib/api/admin/settings';
import type {
  FeatureFlag,
  AuditLog,
  PaginatedResponse,
  PublicSiteSettings,
  UpdatePublicSiteSettingsData,
} from '@/lib/api/admin';

const SETTINGS_KEYS = {
  featureFlags: () => ['admin', 'featureFlags'] as const,
  publicSiteSettings: () => ['admin', 'publicSiteSettings'] as const,
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

export function usePublicSiteSettings() {
  const { data, error, isLoading, mutate } = useSWR<PublicSiteSettings, Error>(
    SETTINGS_KEYS.publicSiteSettings(),
    getPublicSiteSettings,
    { revalidateOnFocus: false, dedupingInterval: 30000 }
  );

  return { settings: data ?? null, isLoading, isError: error, mutate };
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

export async function mutateUpdatePublicSiteSettings(data: UpdatePublicSiteSettingsData) {
  return updatePublicSiteSettings(data);
}
