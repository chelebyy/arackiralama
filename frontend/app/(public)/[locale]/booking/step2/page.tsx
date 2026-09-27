"use client";

import { useParams, useSearchParams, useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { useExactAvailableVehicles, useOffices } from "@/hooks/useVehicles";
import { useBookingStore } from "@/hooks/useBooking";
import { rentalDateTimeUtc } from "@/lib/rental-datetime";
import { catalogueOffice, vehiclePhotos, vehicleGroupName } from "@/lib/vehicle-catalogue";
import VehicleImage from "@/components/public/VehicleImage";
import VehicleFacts from "@/components/public/VehicleFacts";
import CurrencyAmount from "@/components/public/CurrencyAmount";

const emptyGroup = "00000000-0000-0000-0000-000000000000";

export default function BookingStep2Page() {
  const { locale } = useParams<{ locale: string }>();
  const search = useSearchParams();
  const router = useRouter();
  const t = useTranslations("booking"), tv = useTranslations("vehicles");
  const requested = search.get("preferredVehicleId");
  const [selected, setSelected] = useState<string | null>(requested);
  const advanced = useRef<string | null>(null);
  const setDates = useBookingStore(s => s.setDates), setVehicle = useBookingStore(s => s.setVehicle);
  const { offices, isLoading: officesLoading } = useOffices();
  const pickupId = catalogueOffice(offices, search.get("pickup") ?? "");
  const returnId = catalogueOffice(offices, search.get("return") ?? search.get("pickup") ?? "");
  const pickupDate = search.get("pickupDate") ?? "", returnDate = search.get("returnDate") ?? "";
  const pickupTime = search.get("pickupTime") ?? "10:00", returnTime = search.get("returnTime") ?? "09:00";
  const { vehicles, isLoading, isError, mutate } = useExactAvailableVehicles(
    pickupId && returnId && pickupDate && returnDate ? {
      pickupOfficeId: pickupId, returnOfficeId: returnId,
      pickupDateTimeUtc: rentalDateTimeUtc(pickupDate, pickupTime),
      returnDateTimeUtc: rentalDateTimeUtc(returnDate, returnTime)
    } : null);
  const selectedOffer = vehicles.find(offer => offer.vehicle.id === selected);
  const proceed = useCallback(() => {
    const pickup = offices.find(o => o.id === pickupId), returned = offices.find(o => o.id === returnId);
    if (!selectedOffer || !pickup || !returned || isLoading || isError) return;
    const vehicle = selectedOffer.vehicle;
    setDates({ pickupOfficeId: pickup.id, pickupOfficeName: pickup.name, returnOfficeId: returned.id,
      returnOfficeName: returned.name, pickupDate, pickupTime, returnDate, returnTime });
    setVehicle({ vehicleId: vehicle.id, vehicleGroupId: vehicle.groupId ?? emptyGroup,
      vehicleName: `${vehicle.brand} ${vehicle.model}`, vehicleImage: vehiclePhotos(vehicle)[0] ?? "",
      dailyPrice: vehicle.dailyPrice!, groupName: vehicleGroupName(vehicle, locale) });
    const query = new URLSearchParams(search.toString());
    query.set("vehicle", vehicle.groupId ?? emptyGroup);
    query.set("preferredVehicleId", vehicle.id);
    query.set("vehicleName", `${vehicle.brand} ${vehicle.model}`);
    query.delete("vehicleGroupId");
    router.push(`/${locale}/booking/step3?${query}`);
  }, [selectedOffer, offices, pickupId, returnId, pickupDate, pickupTime, returnDate, returnTime, setDates, setVehicle, locale, search, router, isLoading, isError]);
  useEffect(() => {
    if (requested && selected === requested && selectedOffer && !isLoading && !isError && advanced.current !== requested) {
      advanced.current = requested;
      proceed();
    }
  }, [requested, selected, selectedOffer, isLoading, isError, proceed]);

  return <div className="mx-auto max-w-6xl space-y-6">
    <header><h1 className="text-3xl font-bold text-slate-900">{t("step2.title")}</h1><p className="mt-2 text-slate-600">{tv("subtitle")}</p></header>
    <p className="text-sm text-slate-600">{t("quoteAuthoritative")}</p>
    {(isLoading || officesLoading) && <p role="status">{t("loadingVehicles")}</p>}
    {isError && <div role="alert"><p>{t("failedToLoadVehicles")}</p><button type="button" className="mt-3 rounded border p-3" onClick={() => mutate()}>{t("retry")}</button></div>}
    {!isLoading && !officesLoading && !isError && !vehicles.length && <p>{tv("unavailable")}</p>}
    {requested && !isLoading && !isError && !vehicles.some(o => o.vehicle.id === requested) && <p role="alert">{tv("unavailable")}</p>}
    <div className="grid gap-6 md:grid-cols-2">{vehicles.map(offer => <button type="button" key={offer.vehicle.id} aria-pressed={selected === offer.vehicle.id} onClick={() => setSelected(offer.vehicle.id)} className="overflow-hidden rounded-xl border border-slate-200 bg-white text-start aria-pressed:border-sky-600 aria-pressed:ring-2 aria-pressed:ring-sky-600">
      <div className="aspect-[16/9] bg-slate-100"><VehicleImage src={vehiclePhotos(offer.vehicle)[0]} alt={`${offer.vehicle.brand} ${offer.vehicle.model}`} /></div>
      <div className="space-y-4 p-6"><h2 className="text-xl font-semibold">{offer.vehicle.brand} {offer.vehicle.model}</h2><p>{offer.vehicle.year} · {offer.vehicle.color}</p><VehicleFacts vehicle={offer.vehicle} />
        <p className="font-semibold"><CurrencyAmount locale={locale} currency={offer.currency} amount={offer.finalTotal} /> <span className="text-sm font-normal">/ {offer.rentalDays} {t("days")}</span></p>
        <p className="text-sm text-slate-600">{tv("detail.minAge")}: {offer.vehicle.minAge} · {tv("detail.minLicenseYears")}: {offer.vehicle.minLicenseYears}</p>
      </div></button>)}</div>
    <div className="flex justify-between"><button type="button" className="rounded border px-6 py-3" onClick={() => router.push(`/${locale}/booking?${search}`)}>{t("back")}</button><button type="button" disabled={!selectedOffer || isLoading || Boolean(isError)} onClick={proceed} className="rounded-lg bg-sky-700 px-6 py-3 text-white disabled:opacity-40">{t("continueToPayment")}</button></div>
  </div>;
}
