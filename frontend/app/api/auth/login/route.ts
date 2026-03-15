import { NextRequest, NextResponse } from "next/server";

import { callLoginEndpoint } from "@/lib/auth/backend";
import { appendBackendSetCookie, clearAccessCookie, clearRefreshCookies, setAccessCookie } from "@/lib/auth/http";
import { parseAccessTokenClaims, normalizePrincipalScope } from "@/lib/auth/jwt";
import type { PrincipalScope } from "@/lib/auth/types";

interface LoginBody {
  principalScope?: string;
  email?: string;
  password?: string;
}

export async function POST(request: NextRequest) {
  let body: LoginBody;

  try {
    body = (await request.json()) as LoginBody;
  } catch {
    return NextResponse.json(
      {
        success: false,
        message: "Geçersiz istek gövdesi."
      },
      { status: 400 }
    );
  }

  const principalScope = normalizePrincipalScope(body.principalScope ?? "");
  const email = body.email?.trim();
  const password = body.password;

  if (!principalScope || !email || !password) {
    return NextResponse.json(
      {
        success: false,
        message: "Email, parola ve giriş kapsamı zorunludur."
      },
      { status: 400 }
    );
  }

  const { backendResponse, envelope } = await callLoginEndpoint({
    principalScope: principalScope as PrincipalScope,
    email,
    password
  });

  if (!backendResponse.ok || !envelope?.success || !envelope.data?.accessToken) {
    return NextResponse.json(
      {
        success: false,
        message: envelope?.message ?? "Giriş başarısız."
      },
      { status: backendResponse.status || 401 }
    );
  }

  const response = NextResponse.json(
    {
      success: true,
      message: envelope.message,
      data: {
        principalScope,
        role: envelope.data.role,
        expiresAtUtc: envelope.data.expiresAtUtc
      }
    },
    { status: 200 }
  );

  setAccessCookie(response, request, envelope.data.accessToken);
  appendBackendSetCookie(response, backendResponse);

  const claims = parseAccessTokenClaims(envelope.data.accessToken);
  if (!claims) {
    clearAccessCookie(response, request);
    clearRefreshCookies(response, request);

    return NextResponse.json(
      {
        success: false,
        message: "Oturum oluşturulamadı."
      },
      { status: 500 }
    );
  }

  return response;
}
