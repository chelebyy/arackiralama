import { NextRequest, NextResponse } from "next/server";
import { DEFAULT_BACKEND_BASE_URL } from "@/lib/auth/constants";

const sessionName = "guest_reservation_session";
const csrfName = "guest_reservation_csrf";
const allowed = new Set(["view", "request", "verify", "logout", "cancel", "amendment/quote", "amendment/confirm"]);
const headers = { "Cache-Control": "no-store", "Referrer-Policy": "no-referrer" };

async function forward(req: NextRequest, ctx: { params: Promise<{ path: string[] }> }) {
  const path = (await ctx.params).path.join("/");
  if (!allowed.has(path) || (req.method === "GET") !== (path === "view"))
    return NextResponse.json({ code: "not_found" }, { status: 404, headers });
  if (req.method !== "GET" && (req.headers.get("origin") !== req.nextUrl.origin ||
      !req.headers.get("content-type")?.startsWith("application/json")))
    return NextResponse.json({ code: "origin_invalid" }, { status: 403, headers });
  const csrf = req.cookies.get(csrfName)?.value;
  const authenticated = !["request", "verify"].includes(path);
  if (authenticated && req.method !== "GET" && (!csrf || req.headers.get("x-guest-csrf") !== csrf))
    return NextResponse.json({ code: "access_invalid" }, { status: 403, headers });
  const upstreamHeaders: Record<string, string> = { "Content-Type": "application/json" };
  if (authenticated) {
    upstreamHeaders["X-Guest-Session"] = req.cookies.get(sessionName)?.value ?? "";
    upstreamHeaders["X-Guest-CSRF"] = csrf ?? "";
  }
  try {
    const body = req.method === "GET" ? undefined : await req.text();
    if (body && new TextEncoder().encode(body).length > 8192)
      return NextResponse.json({ code: "invalid_request" }, { status: 413, headers });
    const response = await fetch(DEFAULT_BACKEND_BASE_URL.replace(/\/$/, "") +
      "/api/guest/v1/reservation" + (path === "view" ? "" : "/" + path), {
      method: req.method, body, headers: upstreamHeaders, cache: "no-store", signal: AbortSignal.timeout(15000),
    });
    const data = await response.json().catch(() => ({ code: "unavailable" }));
    if (!response.ok) return NextResponse.json({ code: response.status === 401 ? "access_invalid" : "unavailable" },
      { status: response.status, headers });
    const result = NextResponse.json(path === "verify" ? { success: true, csrfToken: data.csrfToken } :
      path === "view" ? { ...data, csrfToken: csrf } : data, { headers });
    const options = { httpOnly: true, secure: process.env.NODE_ENV === "production", sameSite: "strict" as const,
      path: "/api/guest", maxAge: 1200 };
    if (path === "verify") {
      result.cookies.set(sessionName, data.sessionToken, options);
      result.cookies.set(csrfName, data.csrfToken, options);
    }
    if (path === "logout") {
      result.cookies.set(sessionName, "", { ...options, maxAge: 0 });
      result.cookies.set(csrfName, "", { ...options, maxAge: 0 });
    }
    return result;
  } catch {
    return NextResponse.json({ code: "unavailable" }, { status: 503, headers });
  }
}
export const GET = forward;
export const POST = forward;

