import { NextRequest, NextResponse } from "next/server";

import { DEFAULT_BACKEND_BASE_URL } from "@/lib/auth/constants";

function buildTargetUrl(pathSegments: string[]) {
  const base = DEFAULT_BACKEND_BASE_URL.endsWith("/")
    ? DEFAULT_BACKEND_BASE_URL.slice(0, -1)
    : DEFAULT_BACKEND_BASE_URL;

  return `${base}/uploads/${pathSegments.join("/")}`;
}

function copyResponseHeaders(src: Response): Headers {
  const headers = new Headers();
  const passthrough = [
    "content-type",
    "content-length",
    "cache-control",
    "etag",
    "last-modified",
  ];

  src.headers.forEach((value, key) => {
    if (passthrough.includes(key.toLowerCase())) {
      headers.set(key, value);
    }
  });

  return headers;
}

export async function GET(_req: NextRequest, ctx: { params: Promise<{ path: string[] }> }) {
  const { path } = await ctx.params;
  if (!path || path.length === 0) {
    return NextResponse.json({ success: false, message: "Missing upload path" }, { status: 400 });
  }

  const upstream = await fetch(buildTargetUrl(path), {
    method: "GET",
    cache: "no-store",
  });

  return new NextResponse(upstream.body, {
    status: upstream.status,
    headers: copyResponseHeaders(upstream),
  });
}
