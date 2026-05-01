export const API_CONFIG = {
  baseUrl: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api',
  timeout: 30000,
  retryAttempts: 3,
  retryDelay: 1000,
} as const;

export const API_ENDPOINTS = {
  vehicles: {
    list: '/vehicles/available',
    groups: '/vehicles/groups',
    detail: (id: string) => `/vehicles/${id}`,
  },
  offices: {
    list: '/offices',
    detail: (id: string) => `/offices/${id}`,
  },
  reservations: {
    create: '/reservations',
    detail: (code: string) => `/reservations/${code}`,
    hold: (id: string) => `/reservations/${id}/hold`,
    extendHold: (id: string) => `/reservations/${id}/hold/extend`,
  },
  pricing: {
    breakdown: '/pricing/breakdown',
    validateCampaign: '/pricing/campaigns/validate',
  },
} as const;

export enum HttpStatus {
  OK = 200,
  CREATED = 201,
  BAD_REQUEST = 400,
  UNAUTHORIZED = 401,
  FORBIDDEN = 403,
  NOT_FOUND = 404,
  CONFLICT = 409,
  UNPROCESSABLE_ENTITY = 422,
  TOO_MANY_REQUESTS = 429,
  INTERNAL_SERVER_ERROR = 500,
  SERVICE_UNAVAILABLE = 503,
}

export enum ErrorCode {
  NETWORK_ERROR = 'NETWORK_ERROR',
  TIMEOUT_ERROR = 'TIMEOUT_ERROR',
  UNAUTHORIZED = 'UNAUTHORIZED',
  FORBIDDEN = 'FORBIDDEN',
  NOT_FOUND = 'NOT_FOUND',
  VALIDATION_ERROR = 'VALIDATION_ERROR',
  CONFLICT = 'CONFLICT',
  RATE_LIMITED = 'RATE_LIMITED',
  SERVER_ERROR = 'SERVER_ERROR',
  UNKNOWN_ERROR = 'UNKNOWN_ERROR',
}
