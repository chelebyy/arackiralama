import { NextRequest, NextResponse } from "next/server";

import { tryRefreshWithBackend } from "@/lib/auth/backend";
import { ACCESS_COOKIE_NAME } from "@/lib/auth/constants";
import {
  appendBackendSetCookie,
  setAccessCookie,
  toBackendCookieHeader
} from "@/lib/auth/http";
import { isExpired, normalizePrincipalScope, parseAccessTokenClaims } from "@/lib/auth/jwt";

function unauthorizedResponse() {
  const response = NextResponse.json(
    {
      success: false,
      message: "Yetkisiz erişim"
    },
    { status: 401 }
  );

  return response;
}

export async function GET(request: NextRequest) {
  let accessToken = request.cookies.get(ACCESS_COOKIE_NAME)?.value;
  let claims = parseAccessTokenClaims(accessToken);

  if (!accessToken || !claims) {
    return unauthorizedResponse();
  }

  if (isExpired(claims)) {
    const refreshResult = await tryRefreshWithBackend({
      preferredScope: normalizePrincipalScope(claims.principal_type as string | undefined),
      cookieHeader: toBackendCookieHeader(request)
    });

    if (!refreshResult?.envelope.data?.accessToken) {
      return unauthorizedResponse();
    }

    accessToken = refreshResult.envelope.data.accessToken;
    claims = parseAccessTokenClaims(accessToken);

    if (!claims) {
      return unauthorizedResponse();
    }

    const response = NextResponse.json({
      success: true,
      data: {
        principalScope: claims.principal_type,
        role: claims.role,
        email: claims.email,
        principalId: claims.sub
      }
    });

    setAccessCookie(response, request, accessToken);
    appendBackendSetCookie(response, refreshResult.backendResponse);
    return response;
  }

  return NextResponse.json({
    success: true,
    data: {
      principalScope: claims.principal_type,
      role: claims.role,
      email: claims.email,
      principalId: claims.sub
    }
  });
}
