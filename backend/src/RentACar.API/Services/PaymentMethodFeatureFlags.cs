using Microsoft.EntityFrameworkCore;
using RentACar.Core.Interfaces;

namespace RentACar.API.Services;

public sealed record PaymentMethodAvailability(
    bool OnlinePaymentEnabled,
    bool CreditCardEnabled,
    bool DebitCardEnabled,
    bool UnpaidRequestEnabled,
    bool PaypalEnabled)
{
    public bool AnyActionableEnabled => AnyCardEnabled || UnpaidRequestEnabled;
    public bool AnyCardEnabled => OnlinePaymentEnabled && (CreditCardEnabled || DebitCardEnabled);
}

public static class PaymentMethodFeatureFlags
{
    public const string OnlinePayment = "EnableOnlinePayment";
    public const string CreditCard = "EnableCreditCardPayment";
    public const string DebitCard = "EnableDebitCardPayment";
    public const string UnpaidRequest = "EnableUnpaidReservationRequest";
    public const string PayPal = "EnablePayPalPayment";

    public static readonly IReadOnlyList<(string Name, bool Enabled, string Description)> Required =
    [
        (OnlinePayment, false, "Online odeme altyapisini etkinlestirir. Kart yontemleri ayrica yonetilir."),
        (CreditCard, true, "Public rezervasyon akisi icin kredi karti odeme kartini gosterir."),
        (DebitCard, true, "Public rezervasyon akisi icin banka karti odeme kartini gosterir."),
        (UnpaidRequest, true, "Public rezervasyon akisi icin odeme yapmadan 24 saat blokeli talep secenegini gosterir."),
        (PayPal, false, "PayPal provider entegrasyonu henuz aktif degildir; public secenek olarak kullanilmaz.")
    ];

    private static readonly string[] RequiredNames = Required.Select(flag => flag.Name).ToArray();

    public static async Task<PaymentMethodAvailability> GetAvailabilityAsync(
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var flags = await dbContext.FeatureFlags
            .AsNoTracking()
            .Where(x => RequiredNames.Contains(x.Name))
            .ToDictionaryAsync(x => x.Name, x => x.Enabled, cancellationToken);

        return new PaymentMethodAvailability(
            Resolve(flags, OnlinePayment, false),
            Resolve(flags, CreditCard, true),
            Resolve(flags, DebitCard, true),
            Resolve(flags, UnpaidRequest, true),
            false);
    }

    public static bool IsCardMethodEnabled(PaymentMethodAvailability availability, string paymentMethod)
    {
        return paymentMethod switch
        {
            "credit_card" => availability.OnlinePaymentEnabled && availability.CreditCardEnabled,
            "debit_card" => availability.OnlinePaymentEnabled && availability.DebitCardEnabled,
            _ => false
        };
    }

    private static bool Resolve(
        IReadOnlyDictionary<string, bool> flags,
        string name,
        bool defaultValue)
    {
        return flags.TryGetValue(name, out var enabled) ? enabled : defaultValue;
    }
}
