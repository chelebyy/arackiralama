import { NextRequest, NextResponse } from "next/server";

import {
  ACCESS_COOKIE_NAME,
  COOKIE_MAX_AGE_SECONDS,
  DEFAULT_BACKEND_BASE_URL,
  REFRESH_COOKIE_CANDIDATES
} from "@/lib/auth/constants";

function isSecureRequest(request: NextRequest) {
  return request.nextUrl.protocol === "https:" || process.env.NODE_ENV === "production";
}

export function buildBackendUrl(path: string) {
  const base = DEFAULT_BACKEND_BASE_URL.endsWith("/")
    ? DEFAULT_BACKEND_BASE_URL.slice(0, -1)
    : DEFAULT_BACKEND_BASE_URL;

  return `${base}${path.startsWith("/") ? path : `/${path}`}`;
}

export function setAccessCookie(response: NextResponse, request: NextRequest, token: string) {
  response.cookies.set({
    name: ACCESS_COOKIE_NAME,
    value: token,
    path: "/",
    httpOnly: true,
    sameSite: "strict",
    secure: isSecureRequest(request),
    maxAge: COOKIE_MAX_AGE_SECONDS
  });
}

export function clearAccessCookie(response: NextResponse, request: NextRequest) {
  response.cookies.set({
    name: ACCESS_COOKIE_NAME,
    value: "",
    path: "/",
    httpOnly: true,
    sameSite: "strict",
    secure: isSecureRequest(request),
    expires: new Date(0)
  });
}

export function readRefreshCookie(request: NextRequest) {
  for (const cookieName of REFRESH_COOKIE_CANDIDATES) {
    const cookieValue = request.cookies.get(cookieName)?.value;
    if (cookieValue) {
      return { name: cookieName, value: cookieValue };
    }
  }

  return null;
}

export function toBackendCookieHeader(request: NextRequest) {
  const refreshCookie = readRefreshCookie(request);
  if (!refreshCookie) {
    return null;
  }

  return `${refreshCookie.name}=${refreshCookie.value}`;
}

export function appendBackendSetCookie(response: NextResponse, backendResponse: Response) {
  const setCookieHeader = backendResponse.headers.get("set-cookie");
  if (setCookieHeader) {
    response.headers.append("set-cookie", setCookieHeader);
  }
}

export function clearRefreshCookies(response: NextResponse, request: NextRequest) {
  for (const cookieName of REFRESH_COOKIE_CANDIDATES) {
    response.cookies.set({
      name: cookieName,
      value: "",
      path: "/",
      httpOnly: true,
      sameSite: "strict",
      secure: isSecureRequest(request),
      expires: new Date(0)
    });
  }
}
