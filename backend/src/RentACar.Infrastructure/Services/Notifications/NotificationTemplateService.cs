using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Notifications;

namespace RentACar.Infrastructure.Services.Notifications;

public sealed class NotificationTemplateService(IOptions<NotificationOptions>? notificationOptions = null) : INotificationTemplateService
{
    private const string BuiltInDefaultLocale = "tr-TR";
    private readonly string _defaultLocale = NormalizeLocale(notificationOptions?.Value.DefaultLocale, BuiltInDefaultLocale);
    private static readonly IReadOnlyDictionary<string, NotificationTemplateDefinition> EmailTemplates =
        new Dictionary<string, NotificationTemplateDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [BuildKey(NotificationTemplateKeys.PasswordResetCustomer, "en-US")] = new(
                "Password reset request",
                "We received a password reset request.\n\nToken: {{Token}}\nExpires at: {{ExpiresAtUtc}}\n\nIgnore this message if you did not request it.",
                "<p>We received a password reset request.</p><p><strong>Token:</strong> {{Token}}</p><p><strong>Expires at:</strong> {{ExpiresAtUtc}}</p><p>Ignore this message if you did not request it.</p>"),
            [BuildKey(NotificationTemplateKeys.PasswordResetCustomer, "ru-RU")] = new(
                "Запрос на сброс пароля",
                "Мы получили запрос на сброс пароля.\n\nТокен: {{Token}}\nДействителен до: {{ExpiresAtUtc}}\n\nЕсли это были не вы, проигнорируйте сообщение.",
                "<p>Мы получили запрос на сброс пароля.</p><p><strong>Токен:</strong> {{Token}}</p><p><strong>Действителен до:</strong> {{ExpiresAtUtc}}</p><p>Если это были не вы, проигнорируйте сообщение.</p>"),
            [BuildKey(NotificationTemplateKeys.PasswordResetCustomer, "ar-SA")] = new(
                "طلب إعادة تعيين كلمة المرور",
                "تلقينا طلبًا لإعادة تعيين كلمة المرور.\n\nالرمز: {{Token}}\nينتهي في: {{ExpiresAtUtc}}\n\nتجاهل هذه الرسالة إذا لم تطلب ذلك.",
                "<p>تلقينا طلبًا لإعادة تعيين كلمة المرور.</p><p><strong>الرمز:</strong> {{Token}}</p><p><strong>ينتهي في:</strong> {{ExpiresAtUtc}}</p><p>تجاهل هذه الرسالة إذا لم تطلب ذلك.</p>"),
            [BuildKey(NotificationTemplateKeys.PasswordResetCustomer, "de-DE")] = new(
                "Passwort zurücksetzen",
                "Wir haben eine Anfrage zum Zurücksetzen Ihres Passworts erhalten.\n\nToken: {{Token}}\nGültig bis: {{ExpiresAtUtc}}\n\nIgnorieren Sie diese Nachricht, wenn Sie die Anfrage nicht gestellt haben.",
                "<p>Wir haben eine Anfrage zum Zurücksetzen Ihres Passworts erhalten.</p><p><strong>Token:</strong> {{Token}}</p><p><strong>Gültig bis:</strong> {{ExpiresAtUtc}}</p><p>Ignorieren Sie diese Nachricht, wenn Sie die Anfrage nicht gestellt haben.</p>"),
            [BuildKey(NotificationTemplateKeys.PasswordResetCustomer, "tr-TR")] = new(
                "Parola sifirlama talebi",
                "Parola sifirlama talebiniz alindi.\n\nToken: {{Token}}\nGecerlilik: {{ExpiresAtUtc}}\n\nBu islemi siz yapmadiysaniz bu mesaji dikkate almayin.",
                "<p>Parola sifirlama talebiniz alindi.</p><p><strong>Token:</strong> {{Token}}</p><p><strong>Gecerlilik:</strong> {{ExpiresAtUtc}}</p><p>Bu islemi siz yapmadiysaniz bu mesaji dikkate almayin.</p>"),
            [BuildKey(NotificationTemplateKeys.PasswordResetAdmin, "en-US")] = new(
                "Admin password reset",
                "Admin password reset request received.\n\nToken: {{Token}}\nExpires at: {{ExpiresAtUtc}}\n\nIgnore this message if this was not you.",
                "<p>Admin password reset request received.</p><p><strong>Token:</strong> {{Token}}</p><p><strong>Expires at:</strong> {{ExpiresAtUtc}}</p><p>Ignore this message if this was not you.</p>"),
            [BuildKey(NotificationTemplateKeys.PasswordResetAdmin, "ru-RU")] = new(
                "Сброс пароля администратора",
                "Получен запрос на сброс пароля администратора.\n\nТокен: {{Token}}\nДействителен до: {{ExpiresAtUtc}}\n\nЕсли это были не вы, проигнорируйте сообщение.",
                "<p>Получен запрос на сброс пароля администратора.</p><p><strong>Токен:</strong> {{Token}}</p><p><strong>Действителен до:</strong> {{ExpiresAtUtc}}</p><p>Если это были не вы, проигнорируйте сообщение.</p>"),
            [BuildKey(NotificationTemplateKeys.PasswordResetAdmin, "ar-SA")] = new(
                "إعادة تعيين كلمة مرور المسؤول",
                "تم استلام طلب إعادة تعيين كلمة مرور المسؤول.\n\nالرمز: {{Token}}\nينتهي في: {{ExpiresAtUtc}}\n\nتجاهل الرسالة إذا لم تكن أنت.",
                "<p>تم استلام طلب إعادة تعيين كلمة مرور المسؤول.</p><p><strong>الرمز:</strong> {{Token}}</p><p><strong>ينتهي في:</strong> {{ExpiresAtUtc}}</p><p>تجاهل الرسالة إذا لم تكن أنت.</p>"),
            [BuildKey(NotificationTemplateKeys.PasswordResetAdmin, "de-DE")] = new(
                "Admin-Passwort zurücksetzen",
                "Anfrage zum Zurücksetzen des Admin-Passworts wurde erhalten.\n\nToken: {{Token}}\nGültig bis: {{ExpiresAtUtc}}\n\nIgnorieren Sie diese Nachricht, wenn die Anfrage nicht von Ihnen stammt.",
                "<p>Anfrage zum Zurücksetzen des Admin-Passworts wurde erhalten.</p><p><strong>Token:</strong> {{Token}}</p><p><strong>Gültig bis:</strong> {{ExpiresAtUtc}}</p><p>Ignorieren Sie diese Nachricht, wenn die Anfrage nicht von Ihnen stammt.</p>"),
            [BuildKey(NotificationTemplateKeys.PasswordResetAdmin, "tr-TR")] = new(
                "Yonetici parola sifirlama",
                "Yonetici parola sifirlama talebiniz alindi.\n\nToken: {{Token}}\nGecerlilik: {{ExpiresAtUtc}}\n\nBu islemi siz yapmadiysaniz bu mesaji dikkate almayin.",
                "<p>Yonetici parola sifirlama talebiniz alindi.</p><p><strong>Token:</strong> {{Token}}</p><p><strong>Gecerlilik:</strong> {{ExpiresAtUtc}}</p><p>Bu islemi siz yapmadiysaniz bu mesaji dikkate almayin.</p>"),
            [BuildKey(NotificationTemplateKeys.ReservationConfirmed, "en-US")] = new(
                "Reservation confirmed",
                "Your reservation has been created. Code: {{PublicCode}}",
                "<p>Your reservation has been created. Code: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReservationConfirmed, "ru-RU")] = new(
                "Бронирование подтверждено",
                "Ваше бронирование создано. Код: {{PublicCode}}",
                "<p>Ваше бронирование создано. Код: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReservationConfirmed, "ar-SA")] = new(
                "تم تأكيد الحجز",
                "تم إنشاء حجزك. الرمز: {{PublicCode}}",
                "<p>تم إنشاء حجزك. الرمز: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReservationConfirmed, "de-DE")] = new(
                "Reservierung bestätigt",
                "Ihre Reservierung wurde erstellt. Code: {{PublicCode}}",
                "<p>Ihre Reservierung wurde erstellt. Code: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReservationConfirmed, "tr-TR")] = new(
                "Rezervasyon onaylandi",
                "Rezervasyonunuz olusturuldu. Kod: {{PublicCode}}",
                "<p>Rezervasyonunuz olusturuldu. Kod: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReservationCancelled, "en-US")] = new(
                "Reservation cancelled",
                "Your reservation has been cancelled. Code: {{PublicCode}}",
                "<p>Your reservation has been cancelled. Code: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReservationCancelled, "ru-RU")] = new(
                "Бронирование отменено",
                "Ваше бронирование отменено. Код: {{PublicCode}}",
                "<p>Ваше бронирование отменено. Код: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReservationCancelled, "ar-SA")] = new(
                "تم إلغاء الحجز",
                "تم إلغاء حجزك. الرمز: {{PublicCode}}",
                "<p>تم إلغاء حجزك. الرمز: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReservationCancelled, "de-DE")] = new(
                "Reservierung storniert",
                "Ihre Reservierung wurde storniert. Code: {{PublicCode}}",
                "<p>Ihre Reservierung wurde storniert. Code: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReservationCancelled, "tr-TR")] = new(
                "Rezervasyon iptal edildi",
                "Rezervasyonunuz iptal edildi. Kod: {{PublicCode}}",
                "<p>Rezervasyonunuz iptal edildi. Kod: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.PaymentReceived, "en-US")] = new(
                "Payment received",
                "Your payment has been received. Reservation code: {{PublicCode}}",
                "<p>Your payment has been received. Reservation code: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.PaymentReceived, "ru-RU")] = new(
                "Платеж получен",
                "Ваш платеж получен. Код бронирования: {{PublicCode}}",
                "<p>Ваш платеж получен. Код бронирования: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.PaymentReceived, "ar-SA")] = new(
                "تم استلام الدفع",
                "تم استلام دفعتك. رمز الحجز: {{PublicCode}}",
                "<p>تم استلام دفعتك. رمز الحجز: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.PaymentReceived, "de-DE")] = new(
                "Zahlung erhalten",
                "Ihre Zahlung wurde erhalten. Reservierungscode: {{PublicCode}}",
                "<p>Ihre Zahlung wurde erhalten. Reservierungscode: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.PaymentReceived, "tr-TR")] = new(
                "Odeme alindi",
                "Odemeniz alindi. Rezervasyon kodunuz: {{PublicCode}}",
                "<p>Odemeniz alindi. Rezervasyon kodunuz: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.DepositReleased, "en-US")] = new(
                "Deposit released",
                "Your deposit has been released. Reservation code: {{PublicCode}}",
                "<p>Your deposit has been released. Reservation code: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.DepositReleased, "ru-RU")] = new(
                "Депозит разблокирован",
                "Ваш депозит разблокирован. Код бронирования: {{PublicCode}}",
                "<p>Ваш депозит разблокирован. Код бронирования: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.DepositReleased, "ar-SA")] = new(
                "تم فك حجز التأمين",
                "تم فك حجز مبلغ التأمين. رمز الحجز: {{PublicCode}}",
                "<p>تم فك حجز مبلغ التأمين. رمز الحجز: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.DepositReleased, "de-DE")] = new(
                "Kaution freigegeben",
                "Ihre Kaution wurde freigegeben. Reservierungscode: {{PublicCode}}",
                "<p>Ihre Kaution wurde freigegeben. Reservierungscode: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.DepositReleased, "tr-TR")] = new(
                "Depozito serbest birakildi",
                "Depozitonuz serbest birakildi. Rezervasyon kodunuz: {{PublicCode}}",
                "<p>Depozitonuz serbest birakildi. Rezervasyon kodunuz: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.PickupReminder, "en-US")] = new(
                "Vehicle pickup reminder",
                "Reminder: Your vehicle pickup time is approaching. Code: {{PublicCode}}",
                "<p>Reminder: Your vehicle pickup time is approaching. Code: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.PickupReminder, "ru-RU")] = new(
                "Напоминание о выдаче автомобиля",
                "Напоминание: Время выдачи автомобиля приближается. Код: {{PublicCode}}",
                "<p>Напоминание: Время выдачи автомобиля приближается. Код: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.PickupReminder, "ar-SA")] = new(
                "تذكير باستلام السيارة",
                "تذكير: وقت استلام السيارة يقترب. الرمز: {{PublicCode}}",
                "<p>تذكير: وقت استلام السيارة يقترب. الرمز: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.PickupReminder, "de-DE")] = new(
                "Erinnerung an Fahrzeugabholung",
                "Erinnerung: Ihre Fahrzeugabholung steht bald an. Code: {{PublicCode}}",
                "<p>Erinnerung: Ihre Fahrzeugabholung steht bald an. Code: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.PickupReminder, "tr-TR")] = new(
                "Arac teslim hatirlatmasi",
                "Hatirlatma: Rezervasyonunuzun arac teslim saati yaklasiyor. Kod: {{PublicCode}}",
                "<p>Hatirlatma: Rezervasyonunuzun arac teslim saati yaklasiyor. Kod: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReturnReminder, "en-US")] = new(
                "Vehicle return reminder",
                "Reminder: Your vehicle return time is approaching. Code: {{PublicCode}}",
                "<p>Reminder: Your vehicle return time is approaching. Code: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReturnReminder, "ru-RU")] = new(
                "Напоминание о возврате автомобиля",
                "Напоминание: Время возврата автомобиля приближается. Код: {{PublicCode}}",
                "<p>Напоминание: Время возврата автомобиля приближается. Код: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReturnReminder, "ar-SA")] = new(
                "تذكير بإعادة السيارة",
                "تذكير: وقت إعادة السيارة يقترب. الرمز: {{PublicCode}}",
                "<p>تذكير: وقت إعادة السيارة يقترب. الرمز: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReturnReminder, "de-DE")] = new(
                "Erinnerung an Fahrzeugrückgabe",
                "Erinnerung: Ihre Fahrzeugrückgabe steht bald an. Code: {{PublicCode}}",
                "<p>Erinnerung: Ihre Fahrzeugrückgabe steht bald an. Code: <strong>{{PublicCode}}</strong></p>"),
            [BuildKey(NotificationTemplateKeys.ReturnReminder, "tr-TR")] = new(
                "Arac iade hatirlatmasi",
                "Hatirlatma: Rezervasyonunuzun arac iade saati yaklasiyor. Kod: {{PublicCode}}",
                "<p>Hatirlatma: Rezervasyonunuzun arac iade saati yaklasiyor. Kod: <strong>{{PublicCode}}</strong></p>")
        };

    private static readonly IReadOnlyDictionary<string, string> SmsTemplates =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [BuildKey(NotificationTemplateKeys.ReservationConfirmed, "en-US")] = "Your reservation has been created. Code: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReservationConfirmed, "ru-RU")] = "Ваше бронирование создано. Код: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReservationConfirmed, "ar-SA")] = "تم إنشاء حجزك. الرمز: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReservationConfirmed, "de-DE")] = "Ihre Reservierung wurde erstellt. Code: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReservationConfirmed, "tr-TR")] = "Rezervasyonunuz olusturuldu. Kod: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReservationCancelled, "en-US")] = "Your reservation has been cancelled. Code: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReservationCancelled, "ru-RU")] = "Ваше бронирование отменено. Код: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReservationCancelled, "ar-SA")] = "تم إلغاء حجزك. الرمز: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReservationCancelled, "de-DE")] = "Ihre Reservierung wurde storniert. Code: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReservationCancelled, "tr-TR")] = "Rezervasyonunuz iptal edildi. Kod: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.PaymentReceived, "en-US")] = "Your payment has been received. Reservation code: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.PaymentReceived, "ru-RU")] = "Ваш платеж получен. Код бронирования: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.PaymentReceived, "ar-SA")] = "تم استلام دفعتك. رمز الحجز: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.PaymentReceived, "de-DE")] = "Ihre Zahlung wurde erhalten. Reservierungscode: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.PaymentReceived, "tr-TR")] = "Odemeniz alindi. Rezervasyon kodunuz: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.DepositReleased, "en-US")] = "Your deposit has been released. Reservation code: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.DepositReleased, "ru-RU")] = "Ваш депозит разблокирован. Код бронирования: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.DepositReleased, "ar-SA")] = "تم فك حجز مبلغ التأمين. رمز الحجز: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.DepositReleased, "de-DE")] = "Ihre Kaution wurde freigegeben. Reservierungscode: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.DepositReleased, "tr-TR")] = "Depozitonuz serbest birakildi. Rezervasyon kodunuz: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.PickupReminder, "en-US")] = "Reminder: Your vehicle pickup time is approaching. Code: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.PickupReminder, "ru-RU")] = "Напоминание: Время выдачи автомобиля приближается. Код: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.PickupReminder, "ar-SA")] = "تذكير: وقت استلام السيارة يقترب. الرمز: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.PickupReminder, "de-DE")] = "Erinnerung: Ihre Fahrzeugabholung steht bald an. Code: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.PickupReminder, "tr-TR")] = "Hatirlatma: Rezervasyonunuzun arac teslim saati yaklasiyor. Kod: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReturnReminder, "en-US")] = "Reminder: Your vehicle return time is approaching. Code: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReturnReminder, "ru-RU")] = "Напоминание: Время возврата автомобиля приближается. Код: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReturnReminder, "ar-SA")] = "تذكير: وقت إعادة السيارة يقترب. الرمز: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReturnReminder, "de-DE")] = "Erinnerung: Ihre Fahrzeugrückgabe steht bald an. Code: {{PublicCode}}",
            [BuildKey(NotificationTemplateKeys.ReturnReminder, "tr-TR")] = "Hatirlatma: Rezervasyonunuzun arac iade saati yaklasiyor. Kod: {{PublicCode}}"
        };

    public EmailMessageRequest RenderEmail(QueuedEmailNotificationRequest request)
    {
        var template = ResolveEmailTemplate(request.TemplateKey, request.Locale);

        return new EmailMessageRequest
        {
            ToEmail = request.ToEmail,
            Subject = ReplaceVariables(template.Subject, request.Variables),
            PlainTextBody = ReplaceVariables(template.PlainTextBody, request.Variables),
            HtmlBody = ReplaceVariables(template.HtmlBody, request.Variables)
        };
    }

    public SmsMessageRequest RenderSms(QueuedSmsNotificationRequest request)
    {
        var template = ResolveSmsTemplate(request.TemplateKey, request.Locale);

        return new SmsMessageRequest
        {
            ToPhoneNumber = request.ToPhoneNumber,
            Body = ReplaceVariables(template, request.Variables)
        };
    }

    private NotificationTemplateDefinition ResolveEmailTemplate(string templateKey, string locale)
    {
        if (EmailTemplates.TryGetValue(BuildKey(templateKey, locale, _defaultLocale), out var localizedTemplate))
        {
            return localizedTemplate;
        }

        if (EmailTemplates.TryGetValue(BuildKey(templateKey, _defaultLocale, _defaultLocale), out var fallbackTemplate))
        {
            return fallbackTemplate;
        }

        throw new InvalidOperationException($"Email template could not be resolved for key '{templateKey}'.");
    }

    private string ResolveSmsTemplate(string templateKey, string locale)
    {
        if (SmsTemplates.TryGetValue(BuildKey(templateKey, locale, _defaultLocale), out var localizedTemplate))
        {
            return localizedTemplate;
        }

        if (SmsTemplates.TryGetValue(BuildKey(templateKey, _defaultLocale, _defaultLocale), out var fallbackTemplate))
        {
            return fallbackTemplate;
        }

        throw new InvalidOperationException($"SMS template could not be resolved for key '{templateKey}'.");
    }

    private static string ReplaceVariables(string template, IReadOnlyDictionary<string, string> variables)
    {
        var result = template;
        foreach (var pair in variables)
        {
            result = result.Replace("{{" + pair.Key + "}}", pair.Value, StringComparison.Ordinal);
        }

        return result;
    }

    private static string BuildKey(string templateKey, string locale) => BuildKey(templateKey, locale, BuiltInDefaultLocale);

    private static string BuildKey(string templateKey, string locale, string defaultLocale) => $"{templateKey}:{NormalizeLocale(locale, defaultLocale)}";

    private static string NormalizeLocale(string? locale, string defaultLocale)
    {
        return string.IsNullOrWhiteSpace(locale) ? defaultLocale : locale.Trim();
    }

    private sealed record NotificationTemplateDefinition(string Subject, string PlainTextBody, string HtmlBody);
}
