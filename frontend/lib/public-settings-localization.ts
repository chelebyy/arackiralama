import type { PublicLocalizedText, PublicLocalizedTextMap } from "@/lib/api/admin/types";

export type PublicLocalizedField = keyof PublicLocalizedText;

function normalizeLocale(locale: string) {
  return locale.toLowerCase().split("-")[0];
}

function clean(value: string | null | undefined) {
  const text = value?.trim();
  return text ? text : undefined;
}

export function getLocalizedPublicSettingText(
  translations: PublicLocalizedTextMap | null | undefined,
  locale: string,
  field: PublicLocalizedField,
  fallback: string
) {
  const normalizedLocale = normalizeLocale(locale);
  const direct = clean(translations?.[normalizedLocale]?.[field]);

  if (direct) {
    return direct;
  }

  return fallback;
}
