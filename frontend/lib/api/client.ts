import { API_CONFIG, ErrorCode, HttpStatus } from './config';
import type { ApiErrorResponse } from './types';

export class ApiError extends Error {
  public readonly code: ErrorCode;
  public readonly statusCode: number;
  public readonly details?: Record<string, string[]>;
  public readonly timestamp: string;
  public readonly path: string;

  constructor(response: ApiErrorResponse) {
    super(response.message);
    this.name = 'ApiError';
    this.code = this.mapStatusCodeToErrorCode(response.statusCode);
    this.statusCode = response.statusCode;
    this.details = response.details;
    this.timestamp = response.timestamp;
    this.path = response.path;
  }

  private mapStatusCodeToErrorCode(statusCode: number): ErrorCode {
    switch (statusCode) {
      case HttpStatus.UNAUTHORIZED:
        return ErrorCode.UNAUTHORIZED;
      case HttpStatus.FORBIDDEN:
        return ErrorCode.FORBIDDEN;
      case HttpStatus.NOT_FOUND:
        return ErrorCode.NOT_FOUND;
      case HttpStatus.BAD_REQUEST:
        return ErrorCode.VALIDATION_ERROR;
      case HttpStatus.UNPROCESSABLE_ENTITY:
        return ErrorCode.VALIDATION_ERROR;
      case HttpStatus.CONFLICT:
        return ErrorCode.CONFLICT;
      case HttpStatus.TOO_MANY_REQUESTS:
        return ErrorCode.RATE_LIMITED;
      case HttpStatus.INTERNAL_SERVER_ERROR:
      case HttpStatus.SERVICE_UNAVAILABLE:
        return ErrorCode.SERVER_ERROR;
      default:
        return ErrorCode.UNKNOWN_ERROR;
    }
  }
}

export class NetworkError extends Error {
  public readonly code = ErrorCode.NETWORK_ERROR;

  constructor(message = 'Network error occurred') {
    super(message);
    this.name = 'NetworkError';
  }
}

export class TimeoutError extends Error {
  public readonly code = ErrorCode.TIMEOUT_ERROR;

  constructor(message = 'Request timeout') {
    super(message);
    this.name = 'TimeoutError';
  }
}

interface RequestConfig extends RequestInit {
  timeout?: number;
  retries?: number;
  retryDelay?: number;
}

async function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function fetchWithTimeout(
  url: string,
  config: RequestConfig
): Promise<Response> {
  const { timeout = API_CONFIG.timeout, ...fetchConfig } = config;

  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), timeout);

  try {
    const response = await fetch(url, {
      ...fetchConfig,
      signal: controller.signal,
    });
    return response;
  } catch (error) {
    if (error instanceof Error) {
      if (error.name === 'AbortError') {
        throw new TimeoutError();
      }
    }
    throw new NetworkError(error instanceof Error ? error.message : undefined);
  } finally {
    clearTimeout(timeoutId);
  }
}

async function retryFetch(
  url: string,
  config: RequestConfig,
  attempt = 1
): Promise<Response> {
  const { retries = API_CONFIG.retryAttempts, retryDelay = API_CONFIG.retryDelay } = config;

  try {
    const response = await fetchWithTimeout(url, config);
    return response;
  } catch (error) {
    if (error instanceof TimeoutError || error instanceof NetworkError) {
      if (attempt < retries) {
        await delay(retryDelay * attempt);
        return retryFetch(url, config, attempt + 1);
      }
    }
    throw error;
  }
}

function getAuthToken(): string | null {
  if (typeof window === 'undefined') return null;
  return localStorage.getItem('auth_token');
}

export async function apiClient<T>(
  endpoint: string,
  config: RequestConfig = {}
): Promise<T> {
  const url = `${API_CONFIG.baseUrl}${endpoint}`;
  const token = getAuthToken();

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    Accept: 'application/json',
    ...((config.headers as Record<string, string>) || {}),
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const requestConfig: RequestConfig = {
    ...config,
    headers,
  };

  const response = await retryFetch(url, requestConfig);

  if (!response.ok) {
    let errorData: ApiErrorResponse;
    try {
      errorData = await response.json();
    } catch {
      errorData = {
        statusCode: response.status,
        message: response.statusText || 'An error occurred',
        code: 'UNKNOWN_ERROR',
        timestamp: new Date().toISOString(),
        path: endpoint,
      };
    }
    throw new ApiError(errorData);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json();
}

export function get<T>(endpoint: string, config?: RequestConfig): Promise<T> {
  return apiClient<T>(endpoint, { ...config, method: 'GET' });
}

export function post<T>(
  endpoint: string,
  data?: unknown,
  config?: RequestConfig
): Promise<T> {
  return apiClient<T>(endpoint, {
    ...config,
    method: 'POST',
    body: data ? JSON.stringify(data) : undefined,
  });
}

export function put<T>(
  endpoint: string,
  data?: unknown,
  config?: RequestConfig
): Promise<T> {
  return apiClient<T>(endpoint, {
    ...config,
    method: 'PUT',
    body: data ? JSON.stringify(data) : undefined,
  });
}

export function patch<T>(
  endpoint: string,
  data?: unknown,
  config?: RequestConfig
): Promise<T> {
  return apiClient<T>(endpoint, {
    ...config,
    method: 'PATCH',
    body: data ? JSON.stringify(data) : undefined,
  });
}

export function del<T>(endpoint: string, config?: RequestConfig): Promise<T> {
  return apiClient<T>(endpoint, { ...config, method: 'DELETE' });
}
