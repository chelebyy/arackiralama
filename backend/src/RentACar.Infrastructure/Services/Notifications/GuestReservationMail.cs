using Microsoft.AspNetCore.DataProtection;
using RentACar.Core.Interfaces.Notifications;

namespace RentACar.Infrastructure.Services.Notifications;

public static class GuestReservationMail
{
    public static bool IsExpiredAccess(QueuedEmailNotificationRequest request, DateTimeOffset now) =>
        request.TemplateKey == "guest-reservation-access" &&
        (!request.Variables.TryGetValue("ExpiresAtUtc", out var value) ||
         !DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
             System.Globalization.DateTimeStyles.AssumeUniversal, out var expiry) || expiry <= now);

    public static EmailMessageRequest Render(QueuedEmailNotificationRequest request, IDataProtectionProvider? protection)
    {
        var locale = request.Locale.Split('-')[0];
        var access = request.TemplateKey == "guest-reservation-access";
        var cancelled = request.TemplateKey == "guest-reservation-cancelled";
        var text = locale switch
        {
            "tr" => ("Rezervasyon doğrulama kodu", "Bu kodu rezervasyon yönetimi ekranına girin. İsteği siz yapmadıysanız bu e-postayı yok sayın. Son kullanım (UTC):", "Rezervasyon iptal edildi", "Rezervasyon tarihleri güncellendi", "Rezervasyon", "Teslim (UTC)", "İade (UTC)", "Toplam", "İptal ücreti"),
            "de" => ("Bestätigungscode für die Reservierung", "Geben Sie diesen Code in der Reservierungsverwaltung ein. Ignorieren Sie diese E-Mail, wenn Sie die Anfrage nicht gestellt haben. Gültig bis (UTC):", "Reservierung storniert", "Reservierungsdaten aktualisiert", "Reservierung", "Abholung (UTC)", "Rückgabe (UTC)", "Gesamt", "Stornogebühr"),
            "ru" => ("Код подтверждения бронирования", "Введите код на странице управления бронированием. Если вы не отправляли запрос, проигнорируйте письмо. Действителен до (UTC):", "Бронирование отменено", "Даты бронирования обновлены", "Бронирование", "Получение (UTC)", "Возврат (UTC)", "Итого", "Сбор за отмену"),
            "ar" => ("رمز التحقق من الحجز", "أدخل الرمز في صفحة إدارة الحجز. تجاهل الرسالة إذا لم تطلبها. صالح حتى (UTC):", "تم إلغاء الحجز", "تم تحديث تواريخ الحجز", "الحجز", "الاستلام (UTC)", "الإرجاع (UTC)", "المجموع", "رسوم الإلغاء"),
            _ => ("Reservation verification code", "Enter this code on the reservation management page. Ignore this email if you did not request it. Valid until (UTC):", "Reservation cancelled", "Reservation dates updated", "Reservation", "Pickup (UTC)", "Return (UTC)", "Total", "Cancellation fee")
        };
        string body;
        string subject;
        if (access)
        {
            var code = (protection ?? throw new InvalidOperationException("Mail protection is not configured."))
                .CreateProtector("GuestReservationEmail.v1").Unprotect(request.Variables["ProtectedCode"]);
            subject = text.Item1;
            body = text.Item2 + " " + request.Variables["ExpiresAtUtc"] + "\n\n" + code;
        }
        else
        {
            subject = cancelled ? text.Item3 : text.Item4;
            body = $"{text.Item5}: {request.Variables["PublicCode"]}\n{text.Item6}: {request.Variables["PickupDate"]}\n{text.Item7}: {request.Variables["ReturnDate"]}\n{text.Item8}: {request.Variables["Total"]} {request.Variables["Currency"]}";
            if (cancelled) body += $"\n{text.Item9}: {request.Variables["Fee"]} {request.Variables["Currency"]}";
        }
        return new EmailMessageRequest { ToEmail = request.ToEmail, Subject = subject, PlainTextBody = body };
    }
}
