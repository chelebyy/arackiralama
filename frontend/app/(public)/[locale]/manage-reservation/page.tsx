"use client";

import { useEffect, useMemo, useRef, useState, type FormEvent } from "react";
import { useParams } from "next/navigation";
import { guestCopy } from "@/lib/guest-reservation-copy";
import type { DriverDeclaration } from "@/lib/api/types";

type View = {
  publicCode: string; status: string; version: number; vehicle: string;
  pickupOffice: string; returnOffice: string; pickupDateTime: string; returnDateTime: string;
  totalAmount: number; currency: string; canCancel: boolean; canChangeDates: boolean;
  cancellationFee: number | null; changeFee: number | null; csrfToken: string;
};
type Offer = { amendmentId: string; expiresAtUtc: string; rentalTotal: number; fee: number; previousFees?: number;
  finalTotal: number; difference: number; currency: string; conditions: { minAge: number; minLicenseYears: number } };
const inputStyle = "mt-2 w-full rounded-lg border border-slate-300 bg-white p-3";
const buttonStyle = "rounded-lg bg-sky-700 px-5 py-3 font-medium text-white disabled:opacity-50";
const emptyDeclaration: Omit<DriverDeclaration, "ageAtPickup" | "licenseYearsAtPickup"> & {
  ageAtPickup: number | ""; licenseYearsAtPickup: number | "";
} = { ageAtPickup: "", licenseYearsAtPickup: "",
  licenseValidThroughReturn: false, documentsAvailableAtPickup: false };

export default function ManageReservationPage() {
  const { locale } = useParams<{ locale: string }>();
  const copy = guestCopy(locale);
  const [view, setView] = useState<View | null>(null);
  const [challengeId, setChallengeId] = useState("");
  const [requestCoolingDown, setRequestCoolingDown] = useState(false);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const inFlight = useRef(false);
  const csrf = useRef("");
  const [offer, setOffer] = useState<Offer | null>(null);
  const [cancelAccepted, setCancelAccepted] = useState(false);
  const [declaration, setDeclaration] = useState(emptyDeclaration);
  const [dates, setDates] = useState({ pickup: "", returnDate: "" });

  useEffect(() => {
    if (!requestCoolingDown) return;
    const timer = setTimeout(() => setRequestCoolingDown(false), 60000);
    return () => clearTimeout(timer);
  }, [requestCoolingDown]);

  async function request<T>(path: string, body?: unknown): Promise<T> {
    const response = await fetch("/api/guest/" + path, {
      method: body === undefined ? "GET" : "POST", cache: "no-store",
      headers: { "Content-Type": "application/json", "X-Guest-CSRF": csrf.current },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) {
      if (response.status === 401 || response.status === 403) {
        setView(null); setOffer(null); csrf.current = "";
        throw new Error(copy.expired);
      }
      if (response.status === 409) setOffer(null);
      throw new Error(copy.error);
    }
    return response.json() as Promise<T>;
  }
  async function refresh() {
    const next = await request<View>("view");
    csrf.current = next.csrfToken;
    setView(next);
  }
  useEffect(() => {
    let cancelled = false;
    fetch("/api/guest/view", { cache: "no-store" }).then(async response => {
      if (!response.ok) return;
      const next: View = await response.json();
      if (!cancelled) { csrf.current = next.csrfToken; setView(next); }
    }).catch(() => {});
    return () => { cancelled = true; };
  }, []);
  async function run(action: () => Promise<void>) {
    if (inFlight.current) return;
    inFlight.current = true; setBusy(true); setMessage("");
    try { await action(); } catch (error) { setMessage(error instanceof Error ? error.message : copy.error); }
    finally { inFlight.current = false; setBusy(false); }
  }
  function access(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (requestCoolingDown) return;
    const form = new FormData(event.currentTarget);
    void run(async () => {
      const result = await request<{ challengeId: string }>("request",
        { publicCode: form.get("reference"), email: form.get("email"), locale });
      setChallengeId(result.challengeId); setRequestCoolingDown(true); setMessage(copy.sent);
    });
  }
  function verify(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const code = new FormData(event.currentTarget).get("code");
    void run(async () => {
      const result = await request<{ csrfToken: string }>("verify", { challengeId, code });
      csrf.current = result.csrfToken; await refresh(); setChallengeId("");
    });
  }
  const dateFormatter = useMemo(() => new Intl.DateTimeFormat(locale, {
    dateStyle: "medium", timeStyle: "short", timeZone: "Europe/Istanbul",
  }), [locale]);
  const viewCurrency = view?.currency ?? "TRY";
  const offerCurrency = offer?.currency ?? viewCurrency;
  const viewMoney = useMemo(() => new Intl.NumberFormat(locale, { style: "currency", currency: viewCurrency }), [locale, viewCurrency]);
  const offerMoney = useMemo(() => new Intl.NumberFormat(locale, { style: "currency", currency: offerCurrency }), [locale, offerCurrency]);
  const formatDate = (value: string) => dateFormatter.format(new Date(value));
  const money = (value: number, currency = viewCurrency) =>
    (currency === viewCurrency ? viewMoney : offerMoney).format(value);

  return <main className="mx-auto max-w-3xl px-4 py-12 text-slate-900" dir={locale === "ar" ? "rtl" : "ltr"}>
    <h1 className="mb-6 text-3xl font-bold">{copy.title}</h1>
    {message && <p role="status" className="mb-5 rounded-lg border border-slate-300 p-4">{message}</p>}
    <div aria-busy={busy}>
      {!view ? <div className="space-y-6">
        <form onSubmit={access} className="space-y-4">
          <label className="block">{copy.reference}<input name="reference" required maxLength={24} autoComplete="off" className={inputStyle} /></label>
          <label className="block">{copy.email}<input name="email" type="email" required maxLength={254} autoComplete="email" className={inputStyle} /></label>
          <button disabled={busy || requestCoolingDown} className={buttonStyle}>{busy ? copy.loading : copy.sendCode}</button>
        </form>
        {challengeId && <form onSubmit={verify} className="space-y-4">
          <label className="block">{copy.code}<input name="code" required maxLength={64} autoComplete="one-time-code" className={inputStyle} /></label>
          <button disabled={busy} className={buttonStyle}>{copy.verify}</button>
        </form>}
      </div> : <div className="space-y-8">
        <section className="rounded-xl border border-slate-200 p-6">
          <h2 className="text-xl font-semibold">{view.vehicle}</h2>
          <p>{view.publicCode}</p>
          <p>{copy.status}: {view.status === "Confirmed" || view.status === "Cancelled" || view.status === "Active" || view.status === "Completed" ? copy[view.status] : copy.unavailable}</p>
          <p>{view.pickupOffice} — {formatDate(view.pickupDateTime)}</p>
          <p>{view.returnOffice} — {formatDate(view.returnDateTime)}</p>
          <p className="mt-3 font-semibold">{copy.total}: {money(view.totalAmount)}</p>
          <button type="button" disabled={busy} className="mt-4 underline" onClick={() => void run(async () => {
            await request("logout", {}); setView(null); setOffer(null); csrf.current = "";
          })}>{copy.logout}</button>
        </section>
        {!view.canCancel && !view.canChangeDates && <p>{copy.unavailable}</p>}
        {view.canCancel && <section className="space-y-4 rounded-xl border border-slate-200 p-6">
          <h2 className="text-xl font-semibold">{copy.cancel}</h2>
          <p>{copy.fee}: {money(view.cancellationFee ?? 0)}</p>
          <label className="flex items-start gap-3"><input type="checkbox" checked={cancelAccepted} onChange={e => setCancelAccepted(e.target.checked)} />{copy.confirmCancel}</label>
          <button type="button" disabled={busy || !cancelAccepted} className={buttonStyle} onClick={() => void run(async () => {
            await request("cancel", { version: view.version, acceptedFee: view.cancellationFee });
            await refresh(); setOffer(null); setCancelAccepted(false); setMessage(copy.updated);
          })}>{copy.cancel}</button>
        </section>}
        {view.canChangeDates && <section className="space-y-4 rounded-xl border border-slate-200 p-6">
          <h2 className="text-xl font-semibold">{copy.changeDates}</h2><p>{copy.timeZone}</p>
          <form className="space-y-4" onSubmit={event => { event.preventDefault(); void run(async () => {
            setOffer(await request<Offer>("amendment/quote", {
              pickupDateTimeUtc: new Date(dates.pickup + ":00+03:00").toISOString(),
              returnDateTimeUtc: new Date(dates.returnDate + ":00+03:00").toISOString(),
              declaration: { ...declaration, ageAtPickup: Number(declaration.ageAtPickup), licenseYearsAtPickup: Number(declaration.licenseYearsAtPickup) },
            }));
          }); }}>
            {(["pickup", "returnDate"] as const).map(field => <label key={field} className="block">{copy[field]}
              <input type="datetime-local" required value={dates[field]} className={inputStyle} onChange={e => { setDates({ ...dates, [field]: e.target.value }); setOffer(null); }} />
            </label>)}
            {(["ageAtPickup", "licenseYearsAtPickup"] as const).map(field => <label key={field} className="block">{copy[field]}
              <input type="number" required min={field === "ageAtPickup" ? 18 : 0} max={field === "ageAtPickup" ? 100 : 82} value={declaration[field]} className={inputStyle}
                onChange={e => { setDeclaration({ ...declaration, [field]: e.target.value === "" ? "" : Number(e.target.value) }); setOffer(null); }} />
            </label>)}
            {(["licenseValidThroughReturn", "documentsAvailableAtPickup"] as const).map(field => <label key={field} className="flex gap-3">
              <input type="checkbox" required checked={declaration[field]} onChange={e => { setDeclaration({ ...declaration, [field]: e.target.checked }); setOffer(null); }} />{copy[field]}
            </label>)}
            <button disabled={busy} className={buttonStyle}>{copy.quote}</button>
          </form>
          {offer && <div className="space-y-3 border-t pt-4">
            <p>{copy.total}: {money(offer.finalTotal, offer.currency)}</p>
            <p>{copy.fee}: {money(offer.fee, offer.currency)}</p>
            {!!offer.previousFees && <p>{copy.previousFees}: {money(offer.previousFees, offer.currency)}</p>}
            <p>{copy.difference}: {money(offer.difference, offer.currency)}</p>
            <p>{copy.ageAtPickup}: ≥ {offer.conditions.minAge}; {copy.licenseYearsAtPickup}: ≥ {offer.conditions.minLicenseYears}</p>
            <p>{copy.expires}: {formatDate(offer.expiresAtUtc)}</p>
            <button disabled={busy} className={buttonStyle} onClick={() => void run(async () => {
              await request("amendment/confirm", { amendmentId: offer.amendmentId, acceptedTotal: offer.finalTotal });
              await refresh(); setOffer(null); setMessage(copy.updated);
            })}>{copy.accept}</button>
          </div>}
        </section>}
      </div>}
    </div>
  </main>;
}
