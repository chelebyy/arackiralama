"use client";

import FullCalendar from "@fullcalendar/react";
import dayGridPlugin from "@fullcalendar/daygrid";
import interactionPlugin from "@fullcalendar/interaction";
import trLocale from "@fullcalendar/core/locales/tr";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useAdminReservations } from "@/hooks/admin";
import { useState } from "react";
import { Badge } from "@/components/ui/badge";

export default function ReservationCalendarPage() {
  const { reservations, isLoading } = useAdminReservations({ pageSize: 100 });
  const [selectedEvent, setSelectedEvent] = useState<(typeof reservations)[number] | null>(
    null
  );

  const events = reservations.map((r) => ({
    id: r.id,
    title: `${r.customerName || r.customer?.name || "Müşteri"} — ${
      r.vehicleName || r.vehicle?.name || "Araç"
    }`,
    start: r.pickupDate,
    end: r.returnDate,
    extendedProps: { reservation: r },
  }));

  return (
    <div className="grid gap-6 lg:grid-cols-3">
      <Card className="lg:col-span-2">
        <CardHeader>
          <CardTitle className="text-base">Rezervasyon Takvimi</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="h-[500px] flex items-center justify-center text-muted-foreground">
              Yükleniyor...
            </div>
          ) : (
            <FullCalendar
              plugins={[dayGridPlugin, interactionPlugin]}
              initialView="dayGridMonth"
              locale={trLocale}
              events={events}
              eventClick={(info) => {
                setSelectedEvent(info.event.extendedProps.reservation);
              }}
              headerToolbar={{
                left: "prev,next today",
                center: "title",
                right: "dayGridMonth,dayGridWeek",
              }}
              height={600}
            />
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Rezervasyon Detayı</CardTitle>
        </CardHeader>
        <CardContent>
          {selectedEvent ? (
            <div className="space-y-3 text-sm">
              <div className="flex justify-between">
                <span className="text-muted-foreground">Kod</span>
                <span className="font-medium">{selectedEvent.reservationCode}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Müşteri</span>
                <span className="font-medium">
                  {selectedEvent.customerName || selectedEvent.customer?.name}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Araç</span>
                <span className="font-medium">
                  {selectedEvent.vehicleName || selectedEvent.vehicle?.name}
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Alış</span>
                <span>{selectedEvent.pickupDate}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">İade</span>
                <span>{selectedEvent.returnDate}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Tutar</span>
                <span className="font-medium">
                  ₺{selectedEvent.totalPrice?.toLocaleString("tr-TR")}
                </span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-muted-foreground">Durum</span>
                <Badge variant="outline">{selectedEvent.status}</Badge>
              </div>
            </div>
          ) : (
            <div className="text-muted-foreground text-sm">
              Detayları görmek için takvimden bir rezervasyon seçin.
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
