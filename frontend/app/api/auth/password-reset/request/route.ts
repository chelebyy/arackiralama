import { NextRequest, NextResponse } from "next/server";

import { callPasswordResetRequest } from "@/lib/auth/backend";
import { normalizePrincipalScope } from "@/lib/auth/jwt";

interface ResetRequestBody {
  email?: string;
  principalScope?: string;
}

export async function POST(request: NextRequest) {
  let body: ResetRequestBody;

  try {
    body = (await request.json()) as ResetRequestBody;
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
  const email = body.email?.trim();

  if (!principalScope || !email) {
    return NextResponse.json(
      {
        success: false,
        message: "Email ve principalScope zorunludur."
      },
      { status: 400 }
    );
  }

  const { backendResponse, envelope } = await callPasswordResetRequest({
    email,
    principalScope
  });

  return NextResponse.json(
    {
      success: envelope?.success ?? backendResponse.ok,
      message:
        envelope?.message ??
        "Eğer hesap mevcutsa parola sıfırlama talimatları e-posta adresinize gönderilecektir."
    },
    { status: backendResponse.status }
  );
}
