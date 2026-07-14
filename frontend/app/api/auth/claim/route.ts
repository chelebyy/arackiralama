import { NextRequest, NextResponse } from "next/server";

import { callAccountClaimBackendEndpoint } from "@/lib/auth/backend";

interface AccountClaimBody {
  token?: string;
  newPassword?: string;
}

export async function POST(request: NextRequest) {
  let body: unknown;

  try {
    body = await request.json();
  } catch {
    return NextResponse.json(
      {
        success: false,
        message: "Geçersiz istek gövdesi."
      },
      { status: 400 }
    );
  }

  if (body === null || typeof body !== "object" || Array.isArray(body)) {
    return NextResponse.json(
      {
        success: false,
        message: "Geçersiz istek gövdesi."
      },
      { status: 400 }
    );
  }

  const claimBody = body as AccountClaimBody;
  const token =
    typeof claimBody.token === "string" ? claimBody.token.trim() : "";
  const newPassword =
    typeof claimBody.newPassword === "string" ? claimBody.newPassword : "";

  if (!token || !newPassword) {
    return NextResponse.json(
      {
        success: false,
        message: "Hesap talep anahtarı ve yeni parola zorunludur."
      },
      { status: 400 }
    );
  }

  const { backendResponse, envelope } = await callAccountClaimBackendEndpoint({
    token,
    newPassword
  });

  return NextResponse.json(
    {
      success: envelope?.success ?? backendResponse.ok,
      message:
        envelope?.message ??
        (backendResponse.ok
          ? "Hesap parolası oluşturuldu."
          : "Hesap parolası oluşturulamadı.")
    },
    { status: backendResponse.status }
  );
}
