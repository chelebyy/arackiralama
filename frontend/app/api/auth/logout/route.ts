import { NextRequest, NextResponse } from "next/server";

import { callLogoutEndpoint } from "@/lib/auth/backend";
import {
  appendBackendSetCookie,
  clearAccessCookie,
  clearRefreshCookies,
  toBackendCookieHeader
} from "@/lib/auth/http";
import { ACCESS_COOKIE_NAME } from "@/lib/auth/constants";
import { normalizePrincipalScope, parseAccessTokenClaims } from "@/lib/auth/jwt";

export async function POST(request: NextRequest) {
  const accessToken = request.cookies.get(ACCESS_COOKIE_NAME)?.value;
  const claims = parseAccessTokenClaims(accessToken);
  const principalScope = normalizePrincipalScope(claims?.principal_type as string | undefined);

  let logoutResponse: Response | null = null;

  if (accessToken && principalScope) {
    try {
      const result = await callLogoutEndpoint({
        principalScope,
        accessToken,
        cookieHeader: toBackendCookieHeader(request)
      });
      logoutResponse = result.backendResponse;
    } catch {
      logoutResponse = null;
    }
  }

  const response = NextResponse.json({
    success: true,
    message: "Çıkış yapıldı."
  });

  clearAccessCookie(response, request);
  clearRefreshCookies(response, request);

  if (logoutResponse) {
    appendBackendSetCookie(response, logoutResponse);
  }

  return response;
}
