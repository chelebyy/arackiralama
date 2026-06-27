import { adminGet, adminPost, adminPut } from '../client';
import type {
  AdminPublicContent,
  AdminResponse,
  PublicSettingsLocale,
  UpdateAdminPublicContactData,
  UpdateAdminPublicPageDraftData,
} from './types';

const PUBLIC_CONTENT_ENDPOINT = '/v1/public-content';

function unwrapResponse<T>(response: AdminResponse<T>): T {
  if (response && typeof response === 'object' && 'data' in response) {
    return (response as { data: T }).data;
  }
  return response as T;
}

function getPublicContentPageEndpoint(
  slug: string,
  locale: PublicSettingsLocale,
  action: 'draft' | 'publish' | 'unpublish'
) {
  return `${PUBLIC_CONTENT_ENDPOINT}/pages/${encodeURIComponent(slug)}/${encodeURIComponent(locale)}/${action}`;
}

export async function getAdminPublicContent() {
  const response = await adminGet<AdminResponse<AdminPublicContent>>(PUBLIC_CONTENT_ENDPOINT);
  return unwrapResponse(response);
}

export async function updateAdminPublicPageDraft(
  slug: string,
  locale: PublicSettingsLocale,
  data: UpdateAdminPublicPageDraftData
) {
  const response = await adminPut<AdminResponse<AdminPublicContent>>(
    getPublicContentPageEndpoint(slug, locale, 'draft'),
    data
  );
  return unwrapResponse(response);
}

export async function publishAdminPublicPage(
  slug: string,
  locale: PublicSettingsLocale,
  version: string
) {
  const response = await adminPost<AdminResponse<AdminPublicContent>>(
    getPublicContentPageEndpoint(slug, locale, 'publish'),
    { version }
  );
  return unwrapResponse(response);
}

export async function unpublishAdminPublicPage(
  slug: string,
  locale: PublicSettingsLocale,
  version: string
) {
  const response = await adminPost<AdminResponse<AdminPublicContent>>(
    getPublicContentPageEndpoint(slug, locale, 'unpublish'),
    { version }
  );
  return unwrapResponse(response);
}

export async function updateAdminPublicContact(data: UpdateAdminPublicContactData) {
  const response = await adminPut<AdminResponse<AdminPublicContent>>(
    `${PUBLIC_CONTENT_ENDPOINT}/contact`,
    data
  );
  return unwrapResponse(response);
}
