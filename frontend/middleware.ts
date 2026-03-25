import { NextRequest, NextResponse } from "next/server";
import createIntlMiddleware from "next-intl/middleware";
import { routing } from "./i18n/routing";

import { tryRefreshWithBackend, validateAccessTokenWithBackend } from "@/lib/auth/backend";
import {
  ACCESS_COOKIE_NAME,
  ADMIN_DEFAULT_REDIRECT,
  CUSTOMER_DEFAULT_REDIRECT,
  SUPERADMIN_ONLY_PATH_PREFIXES
} from "@/lib/auth/constants";
import {
  appendBackendSetCookie,
  clearAccessCookie,
  clearRefreshCookies,
  setAccessCookie,
  toBackendCookieHeader
} from "@/lib/auth/http";
import { isExpired, normalizePrincipalScope, parseAccessTokenClaims } from "@/lib/auth/jwt";

const intlMiddleware = createIntlMiddleware(routing);

const PUBLIC_LOCALES = ["/tr", "/en", "/ru", "/ar", "/de"];

const GUEST_ROUTES = [
  "/dashboard/login/v1",
  "/dashboard/login/v2",
  "/dashboard/register/v1",
  "/dashboard/register/v2",
  "/dashboard/forgot-password",
  "/dashboard/reset-password"
];

const PUBLIC_ROUTES = ["/dashboard/forbidden"];
const CUSTOMER_ONLY_ROUTES = [CUSTOMER_DEFAULT_REDIRECT];

function matchesRoute(pathname: string, route: string) {
  return pathname === route || pathname.startsWith(`${route}/`);
}

function isGuestRoute(pathname: string) {
  return GUEST_ROUTES.some((route) => matchesRoute(pathname, route));
}

function isPublicRoute(pathname: string) {
  return PUBLIC_ROUTES.some((route) => matchesRoute(pathname, route));
}

function isCustomerOnlyRoute(pathname: string) {
  return CUSTOMER_ONLY_ROUTES.some((route) => matchesRoute(pathname, route));
}

function isSuperAdminOnlyRoute(pathname: string) {
  return SUPERADMIN_ONLY_PATH_PREFIXES.some((prefix) => matchesRoute(pathname, prefix));
}

function createLoginRedirect(request: NextRequest, pathname: string) {
  const loginUrl = new URL("/dashboard/login/v2", request.url);

  if (!isGuestRoute(pathname) && !isPublicRoute(pathname)) {
    loginUrl.searchParams.set("next", `${pathname}${request.nextUrl.search}`);
  }

  return NextResponse.redirect(loginUrl);
}

function applyRefreshedCookies(options: {
  request: NextRequest;
  response: NextResponse;
  refreshedAccessToken: string | null;
  backendRefreshResponse: Response | null;
}) {
  if (options.refreshedAccessToken) {
    setAccessCookie(options.response, options.request, options.refreshedAccessToken);
  }

  if (options.backendRefreshResponse) {
    appendBackendSetCookie(options.response, options.backendRefreshResponse);
  }

  return options.response;
}

function chooseDefaultRedirect(principalScope: string | undefined, role: string | undefined) {
  if (principalScope === "Customer") {
    return CUSTOMER_DEFAULT_REDIRECT;
  }

  if (role === "Admin" || role === "SuperAdmin") {
    return ADMIN_DEFAULT_REDIRECT;
  }

  return "/dashboard/login/v2";
}

function isPublicWebsiteRoute(pathname: string): boolean {
  if (pathname === "/") return true;
  return PUBLIC_LOCALES.some((locale) => pathname === locale || pathname.startsWith(`${locale}/`));
}

async function handleDashboardAuth(request: NextRequest) {
  const pathname = request.nextUrl.pathname;

  let accessToken = request.cookies.get(ACCESS_COOKIE_NAME)?.value;
  let claims = parseAccessTokenClaims(accessToken);
  let tokenScope = normalizePrincipalScope((claims?.principal_type as string | undefined) ?? undefined);
  let refreshedAccessToken: string | null = null;
  let backendRefreshResponse: Response | null = null;

  const shouldAttemptRefresh = !claims || isExpired(claims) || !accessToken;

  if (shouldAttemptRefresh) {
    const refreshResult = await tryRefreshWithBackend({
      preferredScope: tokenScope,
      cookieHeader: toBackendCookieHeader(request)
    });

    if (refreshResult?.envelope.data?.accessToken) {
      refreshedAccessToken = refreshResult.envelope.data.accessToken;
      backendRefreshResponse = refreshResult.backendResponse;
      accessToken = refreshedAccessToken;
      claims = parseAccessTokenClaims(accessToken);
      tokenScope = refreshResult.scope;
    }
  }

  let principalScope = normalizePrincipalScope((claims?.principal_type as string | undefined) ?? undefined);
  const role = typeof claims?.role === "string" ? claims.role : undefined;

  const shouldValidateTokenWithBackend = Boolean(accessToken && principalScope);
  if (shouldValidateTokenWithBackend) {
    const validationResult = await validateAccessTokenWithBackend({
      accessToken: accessToken as string,
      preferredScope: principalScope ?? tokenScope
    });

    if (!validationResult) {
      principalScope = null;
    } else {
      principalScope = validationResult.scope;
    }
  }

  const isRootRequest = pathname === "/" || pathname === "/dashboard";

  if (!principalScope) {
    if (isGuestRoute(pathname) || isPublicRoute(pathname)) {
      return applyRefreshedCookies({
        request,
        response: NextResponse.next(),
        refreshedAccessToken,
        backendRefreshResponse
      });
    }

    const response = createLoginRedirect(request, pathname);

    if (accessToken) {
      clearAccessCookie(response, request);
      clearRefreshCookies(response, request);
    }

    return response;
  }

  if (isRootRequest) {
    const destination = chooseDefaultRedirect(principalScope, role);
    return applyRefreshedCookies({
      request,
      response: NextResponse.redirect(new URL(destination, request.url)),
      refreshedAccessToken,
      backendRefreshResponse
    });
  }

  if (isGuestRoute(pathname)) {
    const destination = chooseDefaultRedirect(principalScope, role);
    return applyRefreshedCookies({
      request,
      response: NextResponse.redirect(new URL(destination, request.url)),
      refreshedAccessToken,
      backendRefreshResponse
    });
  }

  if (isPublicRoute(pathname)) {
    return applyRefreshedCookies({
      request,
      response: NextResponse.next(),
      refreshedAccessToken,
      backendRefreshResponse
    });
  }

  if (principalScope === "Customer") {
    if (!isCustomerOnlyRoute(pathname)) {
      return applyRefreshedCookies({
        request,
        response: NextResponse.redirect(new URL("/dashboard/forbidden", request.url)),
        refreshedAccessToken,
        backendRefreshResponse
      });
    }

    return applyRefreshedCookies({
      request,
      response: NextResponse.next(),
      refreshedAccessToken,
      backendRefreshResponse
    });
  }

  if (isCustomerOnlyRoute(pathname)) {
    return applyRefreshedCookies({
      request,
      response: NextResponse.redirect(new URL(ADMIN_DEFAULT_REDIRECT, request.url)),
      refreshedAccessToken,
      backendRefreshResponse
    });
  }

  if (role === "Admin" && isSuperAdminOnlyRoute(pathname)) {
    return applyRefreshedCookies({
      request,
      response: NextResponse.redirect(new URL("/dashboard/forbidden", request.url)),
      refreshedAccessToken,
      backendRefreshResponse
    });
  }

  return applyRefreshedCookies({
    request,
    response: NextResponse.next(),
    refreshedAccessToken,
    backendRefreshResponse
  });
}

export async function middleware(request: NextRequest) {
  const pathname = request.nextUrl.pathname;

  if (pathname.startsWith("/api/")) {
    return NextResponse.next();
  }

  if (isPublicWebsiteRoute(pathname)) {
    return intlMiddleware(request);
  }

  return handleDashboardAuth(request);
}

export const config = {
  matcher: [
    "/((?!api|_next|_vercel|.*\\..*).*)",
    "/",
    "/dashboard/:path*"
  ]
};

export async function proxy(request: NextRequest): Promise<Response> {
  const response = await handleDashboardAuth(request);
  return response;
}
