import { get, patch } from '../client';
import { mockAuditLogs, mockFeatureFlags } from './mock';
import type {
  AdminPaginatedResponse,
  AdminResponse,
  AuditLog,
  AuditLogListParams,
  FeatureFlag,
  PaginatedResponse,
} from './types';

const USE_MOCK = false;

const FEATURE_FLAGS_ENDPOINT = '/admin/v1/feature-flags';
const AUDIT_LOGS_ENDPOINT = '/admin/v1/audit-logs';

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


export async function getFeatureFlags() {
  if (USE_MOCK) {
    return mockFeatureFlags;
  }
  const response = await get<AdminResponse<FeatureFlag[]>>(FEATURE_FLAGS_ENDPOINT);
  return unwrapResponse(response);
}

export async function updateFeatureFlag(id: string, enabled: boolean) {
  if (USE_MOCK) {
    return mockFeatureFlags[0];
  }
  const response = await patch<AdminResponse<FeatureFlag>>(`${FEATURE_FLAGS_ENDPOINT}/${id}`, { enabled });
  return unwrapResponse(response);
}

export async function getAuditLogs(params?: AuditLogListParams | Record<string, unknown>) {
  if (USE_MOCK) {
    return createMockPaginated(mockAuditLogs);
  }
  const response = await get<AdminPaginatedResponse<AuditLog>>(`${AUDIT_LOGS_ENDPOINT}${buildQueryString(params)}`);
  return unwrapPaginated(response);
}
