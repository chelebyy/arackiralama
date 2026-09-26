const rentalDateTimeFormatter = new Intl.DateTimeFormat("en-GB", {
  timeZone: "Europe/Istanbul",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23"
});

export function rentalDateTimeLocal(value: string): { date: string; time: string } {
  const parts = rentalDateTimeFormatter.formatToParts(new Date(value));
  const part = (type: string) => parts.find((item) => item.type === type)?.value ?? "";
  return { date: `${part("year")}-${part("month")}-${part("day")}`, time: `${part("hour")}:${part("minute")}` };
}

export function rentalDateTimeUtc(date: string, time: string): string {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || !/^([01]\d|2[0-3]):[0-5]\d$/.test(time)) return "";
  const calendar = new Date(`${date}T00:00:00Z`);
  if (!Number.isFinite(calendar.getTime()) || calendar.toISOString().slice(0, 10) !== date) return "";
  return new Date(`${date}T${time}:00+03:00`).toISOString();
}

export function defaultRentalSearchDates(now = Date.now()) {
  const today = rentalDateTimeLocal(new Date(now).toISOString()).date;
  const todayPickup = Date.parse(rentalDateTimeUtc(today, "10:00"));
  const pickup = todayPickup > now ? todayPickup : todayPickup + 24 * 60 * 60 * 1000;
  return {
    pickupDate: rentalDateTimeLocal(new Date(pickup).toISOString()).date,
    returnDate: rentalDateTimeLocal(new Date(pickup + 7 * 24 * 60 * 60 * 1000).toISOString()).date
  };
}
