import { DEFAULT_BACKEND_BASE_URL } from "@/lib/auth/constants";
import type {
  ApiEnvelope,
  LoginResponseData,
  PrincipalScope,
  RefreshResponseData
} from "@/lib/auth/types";

export function buildBackendUrl(path: string) {
  const base = DEFAULT_BACKEND_BASE_URL.endsWith("/")
    ? DEFAULT_BACKEND_BASE_URL.slice(0, -1)
    : DEFAULT_BACKEND_BASE_URL;

  return `${base}${path.startsWith("/") ? path : `/${path}`}`;
}

function endpointForScope(scope: PrincipalScope) {
  return scope === "Admin" ? "/api/admin/v1/auth" : "/api/customer/v1/auth";
}

async function parseEnvelope<T>(response: Response): Promise<ApiEnvelope<T> | null> {
  try {
    return (await response.json()) as ApiEnvelope<T>;
  } catch {
    return null;
  }
}

export async function callLoginEndpoint(payload: {
  principalScope: PrincipalScope;
  email: string;
  password: string;
}) {
  const backendResponse = await fetch(buildBackendUrl(`${endpointForScope(payload.principalScope)}/login`), {
    method: "POST",
    headers: {
      "content-type": "application/json"
    },
    body: JSON.stringify({
      email: payload.email,
      password: payload.password
    }),
    cache: "no-store"
  });

  const envelope = await parseEnvelope<LoginResponseData>(backendResponse);

  return {
    backendResponse,
    envelope
  };
}

export async function callRegisterEndpoint(payload: {
  email: string;
  password: string;
  fullName?: string;
  phone?: string;
}) {
  const backendResponse = await fetch(buildBackendUrl("/api/customer/v1/auth/register"), {
    method: "POST",
    headers: {
      "content-type": "application/json"
    },
    body: JSON.stringify(payload),
    cache: "no-store"
  });

  const envelope = await parseEnvelope<Record<string, unknown>>(backendResponse);

  return {
    backendResponse,
    envelope
  };
}

export async function callPasswordResetRequest(payload: {
  email: string;
  principalScope: PrincipalScope;
}) {
  const backendResponse = await fetch(buildBackendUrl("/api/v1/auth/password-reset/request"), {
    method: "POST",
    headers: {
      "content-type": "application/json"
    },
    body: JSON.stringify(payload),
    cache: "no-store"
  });

  const envelope = await parseEnvelope<Record<string, unknown>>(backendResponse);

  return {
    backendResponse,
    envelope
  };
}

export async function callPasswordResetConfirm(payload: {
  token: string;
  newPassword: string;
  principalScope: PrincipalScope;
}) {
  const backendResponse = await fetch(buildBackendUrl("/api/v1/auth/password-reset/confirm"), {
    method: "POST",
    headers: {
      "content-type": "application/json"
    },
    body: JSON.stringify(payload),
    cache: "no-store"
  });

  const envelope = await parseEnvelope<Record<string, unknown>>(backendResponse);

  return {
    backendResponse,
    envelope
  };
}

export async function tryRefreshWithBackend(options: {
  preferredScope?: PrincipalScope | null;
  cookieHeader: string | null;
}) {
  if (!options.cookieHeader) {
    return null;
  }

  const candidateScopes: PrincipalScope[] = options.preferredScope
    ? [
        options.preferredScope,
        ...(options.preferredScope === "Admin"
          ? (["Customer"] as PrincipalScope[])
          : (["Admin"] as PrincipalScope[]))
      ]
    : ["Admin", "Customer"];

  for (const scope of candidateScopes) {
    const backendResponse = await fetch(buildBackendUrl(`${endpointForScope(scope)}/refresh`), {
      method: "POST",
      headers: {
        cookie: options.cookieHeader
      },
      cache: "no-store"
    });

    const envelope = await parseEnvelope<RefreshResponseData>(backendResponse);

    if (backendResponse.ok && envelope?.success && envelope.data?.accessToken) {
      return {
        scope,
        backendResponse,
        envelope
      };
    }
  }

  return null;
}

export async function callLogoutEndpoint(options: {
  principalScope: PrincipalScope;
  accessToken: string;
  cookieHeader: string | null;
}) {
  const headers = new Headers();
  headers.set("authorization", `Bearer ${options.accessToken}`);
  if (options.cookieHeader) {
    headers.set("cookie", options.cookieHeader);
  }

  const backendResponse = await fetch(buildBackendUrl(`${endpointForScope(options.principalScope)}/logout`), {
    method: "POST",
    headers,
    cache: "no-store"
  });

  const envelope = await parseEnvelope<Record<string, unknown>>(backendResponse);

  return {
    backendResponse,
    envelope
  };
}

export async function validateAccessTokenWithBackend(options: {
  accessToken: string;
  preferredScope?: PrincipalScope | null;
}) {
  const candidateScopes: PrincipalScope[] = options.preferredScope
    ? [
        options.preferredScope,
        ...(options.preferredScope === "Admin"
          ? (["Customer"] as PrincipalScope[])
          : (["Admin"] as PrincipalScope[]))
      ]
    : ["Admin", "Customer"];

  for (const scope of candidateScopes) {
    const backendResponse = await fetch(buildBackendUrl(`${endpointForScope(scope)}/me`), {
      method: "GET",
      headers: {
        authorization: `Bearer ${options.accessToken}`
      },
      cache: "no-store"
    });

    const envelope = await parseEnvelope<Record<string, unknown>>(backendResponse);
    if (backendResponse.ok && envelope?.success) {
      return {
        scope,
        backendResponse,
        envelope
      };
    }
  }

  return null;
}
