"use client";

import type { OperatingPolicy, OperatingWindow } from "@/lib/rental-policy";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useState } from "react";

const labels = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];
const time = (minute: number) => minute < 0 ? "" : `${Math.floor(minute / 60).toString().padStart(2, "0")}:${(minute % 60).toString().padStart(2, "0")}`;
const minutes = (value: string) => { const [h, m] = value.split(":").map(Number); return h * 60 + m; };

type WindowRow = { id: string; window: OperatingWindow };

function OperatingWindowsEditor({ value, onChange, title }: {
  value: OperatingWindow[];
  onChange: (value: OperatingWindow[]) => void;
  title: string;
}) {
  const [state, setState] = useState(() => ({
    source: value,
    rows: value.map(window => ({ id: crypto.randomUUID(), window }))
  }));
  if (state.source !== value) {
    setState({ source: value, rows: value.map(window => ({ id: crypto.randomUUID(), window })) });
  }
  const change = (rows: WindowRow[]) => {
    const windows = rows.map(row => row.window);
    setState({ source: windows, rows });
    onChange(windows);
  };
  const update = (id: string, patch: Partial<OperatingWindow>) =>
    change(state.rows.map(row => row.id === id ? { ...row, window: { ...row.window, ...patch } } : row));

  return <fieldset className="space-y-2"><legend className="font-medium">{title} (Türkiye saati)</legend>
    {state.rows.map(({ id, window }, index) => <div key={id} className="flex flex-wrap gap-2">
      <select aria-label={`${title} günü ${index + 1}`} className="border rounded p-2" value={window.day} onChange={e => update(id, { day: Number(e.target.value) })}>{labels.map((label, day) => <option key={label} value={day}>{label}</option>)}</select>
      <Input className="w-28" type="time" required aria-label={`${title} başlangıç ${index + 1}`} value={time(window.startMinute)} onChange={e => update(id, { startMinute: minutes(e.target.value) })} />
      <Input className="w-28" type="time" required aria-label={`${title} bitiş ${index + 1}`} value={window.endMinute === 1440 ? "00:00" : time(window.endMinute)} onChange={e => update(id, { endMinute: minutes(e.target.value) || 1440 })} />
      <Button type="button" variant="outline" onClick={() => change(state.rows.filter(row => row.id !== id))}>Sil</Button>
    </div>)}
    <Button type="button" variant="outline" onClick={() => change([...state.rows, { id: crypto.randomUUID(), window: { day: 1, startMinute: -1, endMinute: -1 } }])}>Saat aralığı ekle</Button>
  </fieldset>;
}

export function OperatingPolicyEditor({ value, onChange }: { value?: OperatingPolicy | null; onChange: (value: OperatingPolicy) => void }) {
  const [closedDate, setClosedDate] = useState("");
  if (!value) return <section className="rounded border p-4 space-y-3">
    <h3 className="font-semibold">Otomatik rezervasyon ayarları</h3>
    <p className="text-sm">Zorunlu ayarlar henüz yapılmadı. Bu ofisten otomatik rezervasyon alınamaz.</p>
    <Button type="button" onClick={() => onChange({ minimumNoticeMinutes: null, preparationMinutes: null, pickupWindows: [], returnWindows: [], closedDates: [] })}>Ayarları yapılandır</Button>
  </section>;
  return <section className="rounded border p-4 space-y-4">
    <h3 className="font-semibold">Otomatik rezervasyon ayarları</h3>
    <p className="text-sm">Her iki işlem için en az bir saat aralığı ve süreler zorunludur. Bitiş saati aralığa dahil değildir; 00:00 gün sonudur.</p>
    <label className="block">En erken rezervasyon (dakika)<Input type="number" min={0} max={525600} required value={value.minimumNoticeMinutes ?? ""} onChange={e => onChange({ ...value, minimumNoticeMinutes: e.target.value === "" ? null : Number(e.target.value) })} /></label>
    <label className="block">İade sonrası hazırlık (dakika)<Input type="number" min={0} max={10080} required value={value.preparationMinutes ?? ""} onChange={e => onChange({ ...value, preparationMinutes: e.target.value === "" ? null : Number(e.target.value) })} /></label>
    <OperatingWindowsEditor value={value.pickupWindows} title="Teslim" onChange={pickupWindows => onChange({ ...value, pickupWindows })} />
    <OperatingWindowsEditor value={value.returnWindows} title="İade" onChange={returnWindows => onChange({ ...value, returnWindows })} />
    <label className="block">Kapalı tarih<Input type="date" value={closedDate} onChange={e => setClosedDate(e.target.value)} /></label>
    <Button type="button" variant="outline" disabled={!closedDate} onClick={() => { onChange({ ...value, closedDates: [...new Set([...value.closedDates, closedDate])] }); setClosedDate(""); }}>Kapalı tarih ekle</Button>
    <ul>{value.closedDates.map(date => <li key={date} className="flex items-center gap-3">{date}<Button type="button" variant="outline" aria-label={`${date} kapalı tarihini sil`} onClick={() => onChange({ ...value, closedDates: value.closedDates.filter(d => d !== date) })}>Sil</Button></li>)}</ul>
  </section>;
}
