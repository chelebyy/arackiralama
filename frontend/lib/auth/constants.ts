import type { PrincipalScope } from "@/lib/auth/types";

export const ACCESS_COOKIE_NAME = process.env.AUTH_ACCESS_COOKIE_NAME ?? "rac_access";

const configuredRefreshCookieName = process.env.AUTH_REFRESH_COOKIE_NAME;

export const REFRESH_COOKIE_CANDIDATES = Array.from(
  new Set([
    configuredRefreshCookieName,
    "rac_refresh",
    "__Host-rac_refresh"
  ].filter((value): value is string => Boolean(value && value.trim())))
);

export const DEFAULT_BACKEND_BASE_URL =
  process.env.AUTH_BACKEND_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5135";

export const COOKIE_MAX_AGE_SECONDS = 60 * 15;

export const ADMIN_DEFAULT_REDIRECT = "/dashboard/default";
export const CUSTOMER_DEFAULT_REDIRECT = "/dashboard/customer-portal";
export const SUPERADMIN_ONLY_PATH_PREFIXES = ["/dashboard/pages/users"];

export const PRINCIPAL_SCOPES: PrincipalScope[] = ["Admin", "Customer"];
