import { NextRequest, NextResponse } from "next/server";

import { callPasswordResetConfirm } from "@/lib/auth/backend";
import { normalizePrincipalScope } from "@/lib/auth/jwt";

interface ResetConfirmBody {
  token?: string;
  newPassword?: string;
  principalScope?: string;
}

export async function POST(request: NextRequest) {
  let body: ResetConfirmBody;

  try {
    body = (await request.json()) as ResetConfirmBody;
  } catch {
    return NextResponse.json(
      {
        success: false,
        message: "Geçersiz istek gövdesi."
      },
      { status: 400 }
    );
  }

  const principalScope = normalizePrincipalScope(body.principalScope);
  const token = body.token?.trim();
  const newPassword = body.newPassword;

  if (!principalScope || !token || !newPassword) {
    return NextResponse.json(
      {
        success: false,
        message: "Token, yeni parola ve principalScope zorunludur."
      },
      { status: 400 }
    );
  }

  const { backendResponse, envelope } = await callPasswordResetConfirm({
    token,
    newPassword,
    principalScope
  });

  return NextResponse.json(
    {
      success: envelope?.success ?? backendResponse.ok,
      message: envelope?.message ?? "Parola sıfırlama işlemi tamamlanamadı."
    },
    { status: backendResponse.status }
  );
}
