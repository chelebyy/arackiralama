import { NextRequest, NextResponse } from "next/server";

import { ACCESS_COOKIE_NAME, DEFAULT_BACKEND_BASE_URL } from "@/lib/auth/constants";

const ALLOWED_METHODS = new Set(["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"]);

function buildTargetUrl(req: NextRequest, pathSegments: string[]) {
  const base = DEFAULT_BACKEND_BASE_URL.endsWith("/")
    ? DEFAULT_BACKEND_BASE_URL.slice(0, -1)
    : DEFAULT_BACKEND_BASE_URL;
  const search = req.nextUrl.search;
  const normalizedSegments = pathSegments[0] === "v1"
    ? pathSegments.slice(1)
    : pathSegments;

  return `${base}/api/admin/v1/${normalizedSegments.join("/")}${search}`;
}

function copyResponseHeaders(src: Response): Headers {
  const headers = new Headers();
  const passthrough = [
    "content-type",
    "cache-control",
    "x-correlation-id",
    "x-pagination",
    "x-total-count",
  ];
  src.headers.forEach((value, key) => {
    if (passthrough.includes(key.toLowerCase())) {
      headers.set(key, value);
    }
  });
  return headers;
}

function makeErrorResponse(status: number, message: string) {
  return NextResponse.json({ success: false, message }, { status });
}

export async function GET(req: NextRequest, ctx: { params: Promise<{ path: string[] }> }) {
  return forward(req, ctx);
}
export async function POST(req: NextRequest, ctx: { params: Promise<{ path: string[] }> }) {
  return forward(req, ctx);
}
export async function PUT(req: NextRequest, ctx: { params: Promise<{ path: string[] }> }) {
  return forward(req, ctx);
}
export async function PATCH(req: NextRequest, ctx: { params: Promise<{ path: string[] }> }) {
  return forward(req, ctx);
}
export async function DELETE(req: NextRequest, ctx: { params: Promise<{ path: string[] }> }) {
  return forward(req, ctx);
}

export async function OPTIONS() {
  return new NextResponse(null, {
    status: 204,
    headers: {
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Methods": "GET, POST, PUT, PATCH, DELETE, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type, Authorization",
    },
  });
}

async function forward(
  req: NextRequest,
  ctx: { params: Promise<{ path: string[] }> }
) {
  if (!ALLOWED_METHODS.has(req.method)) {
    return makeErrorResponse(405, "Method not allowed");
  }

  const { path } = await ctx.params;
  if (!path || path.length === 0) {
    return makeErrorResponse(400, "Missing admin path");
  }

  const accessToken = req.cookies.get(ACCESS_COOKIE_NAME)?.value;
  if (!accessToken) {
    return makeErrorResponse(401, "Yetkisiz erişim");
  }

  const targetUrl = buildTargetUrl(req, path);
  const headers = new Headers();
  headers.set("authorization", `Bearer ${accessToken}`);
  headers.set("accept", "application/json");

  const contentType = req.headers.get("content-type");
  if (contentType) {
    headers.set("content-type", contentType);
  }

  const init: RequestInit = {
    method: req.method,
    headers,
    cache: "no-store",
  };

  if (req.method !== "GET" && req.method !== "HEAD") {
    init.body = await req.text();
  }

  const upstream = await fetch(targetUrl, init);
  const responseHeaders = copyResponseHeaders(upstream);
  responseHeaders.set("Access-Control-Allow-Origin", "*");

  return new NextResponse(upstream.body, {
    status: upstream.status,
    headers: responseHeaders,
  });
}
