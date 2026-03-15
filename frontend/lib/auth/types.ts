export type PrincipalScope = "Admin" | "Customer";

export type AuthRole = "Guest" | "Customer" | "Admin" | "SuperAdmin";

export interface ApiEnvelope<T> {
  success: boolean;
  message: string;
  data: T | null;
}

export interface AccessTokenClaims {
  sub?: string;
  email?: string;
  role?: AuthRole;
  principal_type?: PrincipalScope;
  exp?: number;
  sid?: string;
  ver?: string;
  [key: string]: unknown;
}

export interface LoginResponseData {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  role?: AuthRole;
  email?: string;
  fullName?: string;
  customerId?: string;
}

export interface RefreshResponseData {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
}
