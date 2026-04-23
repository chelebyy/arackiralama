'use client';

import useSWR from 'swr';
import {
  getCustomers,
  getCustomerById,
  getAdminUsers,
  createAdminUser,
  updateAdminUserRole,
  updateAdminUserStatus,
} from '@/lib/api/admin/users';
import type { AdminCustomer, AdminUser, PaginatedResponse } from '@/lib/api/admin';

const USER_KEYS = {
  customers: () => ['admin', 'customers'] as const,
  customer: (id: string) => ['admin', 'customers', id] as const,
  admins: () => ['admin', 'adminUsers'] as const,
};

export function useAdminCustomers(params?: Record<string, unknown>) {
  const { data, error, isLoading, mutate } = useSWR<
    PaginatedResponse<AdminCustomer>,
    Error
  >(USER_KEYS.customers(), () => getCustomers(params), {
    revalidateOnFocus: false,
    dedupingInterval: 30000,
  });

  return {
    customers: data?.items ?? [],
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

export function useAdminCustomer(id: string | null) {
  const { data, error, isLoading, mutate } = useSWR<AdminCustomer, Error>(
    id ? USER_KEYS.customer(id) : null,
    () => getCustomerById(id!),
    { revalidateOnFocus: false }
  );

  return { customer: data, isLoading, isError: error, mutate };
}

export function useAdminUsers() {
  const { data, error, isLoading, mutate } = useSWR<
    PaginatedResponse<AdminUser>,
    Error
  >(USER_KEYS.admins(), getAdminUsers, {
    revalidateOnFocus: false,
    dedupingInterval: 30000,
  });

  return {
    users: data?.items ?? [],
    isLoading,
    isError: error,
    mutate,
  };
}

export async function mutateCreateAdminUser(data: Parameters<typeof createAdminUser>[0]) {
  return createAdminUser(data);
}

export async function mutateUpdateAdminUserRole(
  id: string,
  role: Parameters<typeof updateAdminUserRole>[1]
) {
  return updateAdminUserRole(id, role);
}

export async function mutateUpdateAdminUserStatus(id: string, isActive: boolean) {
  return updateAdminUserStatus(id, isActive);
}
