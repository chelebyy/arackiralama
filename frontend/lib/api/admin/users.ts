import { adminGet, adminPatch, adminPost } from '../client';
import { mockAdminUsers, mockCustomers } from './mock';
import type {
  AdminCustomer,
  AdminPaginatedResponse,
  AdminResponse,
  AdminUser,
  AdminUserListParams,
  CreateAdminUserData,
  CustomerListParams,
  PaginatedResponse,
  UpdateAdminUserRoleData,
  UpdateAdminUserStatusData,
} from './types';

const USE_MOCK = false;

const USERS_ENDPOINT = '/v1/users';

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


export async function getCustomers(params?: CustomerListParams | Record<string, unknown>) {
  if (USE_MOCK) {
    return createMockPaginated(mockCustomers);
  }
  const response = await adminGet<AdminPaginatedResponse<AdminCustomer>>(`${USERS_ENDPOINT}/customers${buildQueryString(params)}`);
  return unwrapPaginated(response);
}

export async function getCustomerById(id: string) {
  if (USE_MOCK) {
    return mockCustomers.find((customer) => customer.id === id) || mockCustomers[0];
  }
  const response = await adminGet<AdminResponse<AdminCustomer>>(`${USERS_ENDPOINT}/customers/${id}`);
  return unwrapResponse(response);
}

export async function getAdminUsers(params?: AdminUserListParams | Record<string, unknown>) {
  if (USE_MOCK) {
    return createMockPaginated(mockAdminUsers);
  }
  const response = await adminGet<AdminPaginatedResponse<AdminUser>>(`${USERS_ENDPOINT}/admins${buildQueryString(params)}`);
  return unwrapPaginated(response);
}

export async function createAdminUser(data: CreateAdminUserData) {
  if (USE_MOCK) {
    return mockAdminUsers[0];
  }
  const response = await adminPost<AdminResponse<AdminUser>>(`${USERS_ENDPOINT}/admins`, data);
  return unwrapResponse(response);
}

export async function updateAdminUserRole(
  id: string,
  role: UpdateAdminUserRoleData['role'] | UpdateAdminUserRoleData
) {
  if (USE_MOCK) {
    return mockAdminUsers[0];
  }
  const payload = typeof role === 'string' ? { role } : role;
  const response = await adminPatch<AdminResponse<AdminUser>>(`${USERS_ENDPOINT}/admins/${id}/role`, payload);
  return unwrapResponse(response);
}

export async function updateAdminUserStatus(
  id: string,
  isActive: UpdateAdminUserStatusData['isActive'] | UpdateAdminUserStatusData
) {
  if (USE_MOCK) {
    return mockAdminUsers[0];
  }
  const payload = typeof isActive === 'boolean' ? { isActive } : isActive;
  const response = await adminPatch<AdminResponse<AdminUser>>(`${USERS_ENDPOINT}/admins/${id}/status`, payload);
  return unwrapResponse(response);
}
