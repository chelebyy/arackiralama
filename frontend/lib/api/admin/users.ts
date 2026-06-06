import { adminDel, adminGet, adminPost, adminPut } from '../client';
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
  UpdateAdminUserData,
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
  if (Array.isArray(response)) {
    return createMockPaginated(response);
  }

  if (response && typeof response === 'object' && 'data' in response) {
    const data = (response as { data: PaginatedResponse<T> | T[] }).data;
    return Array.isArray(data) ? createMockPaginated(data) : data;
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
  const response = await adminGet<AdminPaginatedResponse<AdminUser>>(`${USERS_ENDPOINT}${buildQueryString(params)}`);
  return unwrapPaginated(response);
}

export async function createAdminUser(data: CreateAdminUserData) {
  if (USE_MOCK) {
    return mockAdminUsers[0];
  }
  const response = await adminPost<AdminResponse<AdminUser>>(USERS_ENDPOINT, data);
  return unwrapResponse(response);
}

export async function updateAdminUser(id: string, data: UpdateAdminUserData) {
  if (USE_MOCK) {
    return { ...mockAdminUsers[0], ...data };
  }
  const response = await adminPut<AdminResponse<AdminUser>>(`${USERS_ENDPOINT}/${id}`, data);
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
  const response = await adminPut<AdminResponse<AdminUser>>(`${USERS_ENDPOINT}/${id}/role`, payload);
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
  const action = payload.isActive ? 'activate' : 'deactivate';
  const response = await adminPost<AdminResponse<AdminUser>>(`${USERS_ENDPOINT}/${id}/${action}`);
  return unwrapResponse(response);
}

export async function deleteAdminUser(id: string): Promise<void> {
  if (USE_MOCK) {
    return;
  }
  await adminDel(`${USERS_ENDPOINT}/${id}`);
}
