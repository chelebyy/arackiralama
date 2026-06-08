import { adminGet, adminPatch, adminPut } from '../client';
import type {
  AdminPaginatedResponse,
  AdminResponse,
  AuditLog,
  AuditLogListParams,
  FeatureFlag,
  PublicSiteSettings,
  UpdatePublicSiteSettingsData,
  PaginatedResponse,
} from './types';

const FEATURE_FLAGS_ENDPOINT = '/v1/feature-flags';
const AUDIT_LOGS_ENDPOINT = '/v1/audit-logs';
const PUBLIC_SITE_SETTINGS_ENDPOINT = '/v1/public-site-settings';

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

export async function getFeatureFlags() {
  const response = await adminGet<AdminResponse<FeatureFlag[]>>(FEATURE_FLAGS_ENDPOINT);
  return unwrapResponse(response);
}

export async function updateFeatureFlag(name: string, enabled: boolean) {
  const response = await adminPatch<AdminResponse<FeatureFlag>>(
    `${FEATURE_FLAGS_ENDPOINT}/${encodeURIComponent(name)}`,
    { enabled }
  );
  return unwrapResponse(response);
}

export async function getAuditLogs(params?: AuditLogListParams | Record<string, unknown>) {
  const response = await adminGet<AdminPaginatedResponse<AuditLog>>(`${AUDIT_LOGS_ENDPOINT}${buildQueryString(params)}`);
  return unwrapPaginated(response);
}

export async function getPublicSiteSettings() {
  const response = await adminGet<AdminResponse<PublicSiteSettings>>(PUBLIC_SITE_SETTINGS_ENDPOINT);
  return unwrapResponse(response);
}

export async function updatePublicSiteSettings(data: UpdatePublicSiteSettingsData) {
  const response = await adminPut<AdminResponse<PublicSiteSettings>>(PUBLIC_SITE_SETTINGS_ENDPOINT, data);
  return unwrapResponse(response);
}
