import { NextRequest, NextResponse } from "next/server";

import { tryRefreshWithBackend } from "@/lib/auth/backend";
import {
  appendBackendSetCookie,
  clearAccessCookie,
  clearRefreshCookies,
  setAccessCookie,
  toBackendCookieHeader
} from "@/lib/auth/http";
import { normalizePrincipalScope, parseAccessTokenClaims } from "@/lib/auth/jwt";
import { ACCESS_COOKIE_NAME } from "@/lib/auth/constants";

export async function POST(request: NextRequest) {
  const accessToken = request.cookies.get(ACCESS_COOKIE_NAME)?.value;
  const claims = parseAccessTokenClaims(accessToken);
  const preferredScope = normalizePrincipalScope(claims?.principal_type as string | undefined);

  const refreshResult = await tryRefreshWithBackend({
    preferredScope,
    cookieHeader: toBackendCookieHeader(request)
  });

  if (!refreshResult) {
    const response = NextResponse.json(
      {
        success: false,
        message: "Oturum yenileme başarısız."
      },
      { status: 401 }
    );

    clearAccessCookie(response, request);
    clearRefreshCookies(response, request);
    return response;
  }

  const refreshData = refreshResult.envelope?.data;
  if (!refreshData?.accessToken) {
    const response = NextResponse.json(
      {
        success: false,
        message: "Oturum yenileme başarısız."
      },
      { status: 401 }
    );

    clearAccessCookie(response, request);
    clearRefreshCookies(response, request);
    return response;
  }

  const response = NextResponse.json({
    success: true,
    message: refreshResult.envelope.message,
    data: {
      principalScope: refreshResult.scope,
      expiresAtUtc: refreshData.expiresAtUtc
    }
  });

  setAccessCookie(response, request, refreshData.accessToken);
  appendBackendSetCookie(response, refreshResult.backendResponse);

  return response;
}
