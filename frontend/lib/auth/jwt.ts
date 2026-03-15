import type { AccessTokenClaims, PrincipalScope } from "@/lib/auth/types";

function decodeBase64UrlSegment(segment: string): string | null {
  try {
    const base64 = segment.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=");

    if (typeof atob === "function") {
      return decodeURIComponent(
        Array.from(atob(padded))
          .map((character) => `%${character.charCodeAt(0).toString(16).padStart(2, "0")}`)
          .join("")
      );
    }

    if (typeof Buffer !== "undefined") {
      return Buffer.from(padded, "base64").toString("utf-8");
    }

    return null;
  } catch {
    return null;
  }
}

export function parseAccessTokenClaims(token: string | null | undefined): AccessTokenClaims | null {
  if (!token) {
    return null;
  }

  const parts = token.split(".");
  if (parts.length < 2) {
    return null;
  }

  const payload = decodeBase64UrlSegment(parts[1]);
  if (!payload) {
    return null;
  }

  try {
    return JSON.parse(payload) as AccessTokenClaims;
  } catch {
    return null;
  }
}

export function isExpired(claims: AccessTokenClaims | null, nowEpochSeconds = Math.floor(Date.now() / 1000)) {
  if (!claims?.exp) {
    return true;
  }

  return claims.exp <= nowEpochSeconds;
}

export function normalizePrincipalScope(scope: string | undefined | null): PrincipalScope | null {
  if (!scope) {
    return null;
  }

  if (scope.toLowerCase() === "admin") {
    return "Admin";
  }

  if (scope.toLowerCase() === "customer") {
    return "Customer";
  }

  return null;
}
