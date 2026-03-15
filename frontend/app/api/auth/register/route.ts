import { NextRequest, NextResponse } from "next/server";

import { callRegisterEndpoint } from "@/lib/auth/backend";

interface RegisterBody {
  email?: string;
  password?: string;
  fullName?: string;
  phone?: string;
}

export async function POST(request: NextRequest) {
  let body: RegisterBody;

  try {
    body = (await request.json()) as RegisterBody;
  } catch {
    return NextResponse.json(
      {
        success: false,
        message: "Geçersiz istek gövdesi."
      },
      { status: 400 }
    );
  }

  const email = body.email?.trim();
  const password = body.password;

  if (!email || !password) {
    return NextResponse.json(
      {
        success: false,
        message: "Email ve parola zorunludur."
      },
      { status: 400 }
    );
  }

  const { backendResponse, envelope } = await callRegisterEndpoint({
    email,
    password,
    fullName: body.fullName?.trim(),
    phone: body.phone?.trim()
  });

  return NextResponse.json(
    {
      success: envelope?.success ?? backendResponse.ok,
      message: envelope?.message ?? (backendResponse.ok ? "Kayıt başarılı." : "Kayıt başarısız.")
    },
    {
      status: backendResponse.status
    }
  );
}
