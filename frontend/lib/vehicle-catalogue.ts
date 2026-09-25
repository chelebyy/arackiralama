import { API_CONFIG } from "@/lib/api/config";
import type { PublicVehicle } from "@/lib/api/types";

export const equipmentCodes = [
  "airConditioning",
  "bluetooth",
  "navigation",
  "parkingSensors",
  "rearCamera",
  "cruiseControl",
  "childSeatAnchors"
] as const;
const searchKeys = [
  "pickup",
  "return",
  "pickupDate",
  "pickupTime",
  "returnDate",
  "returnTime"
] as const;

export function catalogueSearch(source: Pick<URLSearchParams, "get">): URLSearchParams {
  const query = new URLSearchParams();
  for (const key of searchKeys) {
    const value = source.get(key);
    if (value) query.set(key, value);
  }
  return query;
}

export function vehicleDetailHref(
  id: string,
  locale: string,
  search: Pick<URLSearchParams, "get">
): string {
  const query = catalogueSearch(search).toString();
  return `/${locale}/vehicles/${encodeURIComponent(id)}${query ? `?${query}` : ""}`;
}

export function vehiclePhotos(vehicle: Pick<PublicVehicle, "photoUrls" | "photoUrl">): string[] {
  const photos = vehicle.photoUrls?.length
    ? vehicle.photoUrls
    : vehicle.photoUrl
      ? [vehicle.photoUrl]
      : [];
  return photos.map(resolveVehicleMedia).filter(Boolean);
}

export function resolveVehicleMedia(url: string): string {
  if (!url) return "";
  if (/^https?:\/\//i.test(url)) return url;
  if (!url.startsWith("/uploads/vehicles/") || url.includes("..")) return "";
  return `${new URL(API_CONFIG.baseUrl).origin}${url}`;
}

export function vehicleGroupName(vehicle: PublicVehicle, locale: string): string {
  return locale === "tr"
    ? vehicle.groupName || vehicle.groupNameEn
    : vehicle.groupNameEn || vehicle.groupName;
}

export function validCatalogueDates(search: Pick<URLSearchParams, "get">, now = Date.now()) {
  const parse = (prefix: string) => {
    const date = search.get(`${prefix}Date`);
    const time = search.get(`${prefix}Time`);
    if (
      !date ||
      !time ||
      !/^\d{4}-\d{2}-\d{2}$/.test(date) ||
      !/^([01]\d|2[0-3]):[0-5]\d$/.test(time)
    )
      return null;
    const value = new Date(`${date}T${time}:00+03:00`);
    const calendar = new Date(`${date}T00:00:00Z`);
    if (!Number.isFinite(value.getTime()) || calendar.toISOString().slice(0, 10) !== date)
      return null;
    return value;
  };
  const pickup = parse("pickup"),
    returned = parse("return");
  return pickup && returned && pickup.getTime() > now && returned > pickup
    ? { pickup: pickup.toISOString(), returned: returned.toISOString() }
    : null;
}

export function catalogueOffice(offices: { id: string; name: string }[], value: string) {
  const patterns: Record<string, string> = {
    ala: "alanya",
    gzp: "gazipaşa",
    ayt: "antalya",
    mahmutlar: "mahmutlar",
    kargicak: "kargıcak",
    konakli: "konaklı",
    avsallar: "avsallar"
  };
  return (
    offices.find((office) => office.id === value)?.id ??
    offices.find(
      (office) => patterns[value] && office.name.toLocaleLowerCase("tr").includes(patterns[value])
    )?.id
  );
}
