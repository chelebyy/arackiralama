# Payment Method Visibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the unpaid reservation option into the payment method card list and let admins control actionable public payment methods from Feature Flags.

**Architecture:** Use the existing `FeatureFlags` table and admin Feature Flags page instead of adding a new PostgreSQL table. Public settings expose a derived payment-method visibility object, while backend command paths enforce the same flags so direct API calls cannot bypass admin configuration.

**Tech Stack:** .NET 10, EF Core, PostgreSQL, xUnit, Next.js 16 App Router, React 19, TypeScript, Vitest, Testing Library.

---

## Decisions Locked

- Admin location: existing `/dashboard/settings/feature-flags`.
- Default actionable methods: credit card on, debit card on, unpaid reservation request on.
- PayPal: not actionable in v1 because no provider/payment-intent path exists; do not expose it as a selectable public card.
- If every actionable method is disabled, Step 4 must block completion with a clear unavailable state.
- Backend enforcement is mandatory for both paid payment intent creation and unpaid request creation.

## File Structure

- Modify `backend/src/RentACar.API/Services/PaymentMethodFeatureFlags.cs`
  - New small helper for flag names, defaults, and derived method availability.
- Modify `backend/src/RentACar.API/Services/FeatureFlagService.cs`
  - Add required payment method flags and admin-facing descriptions.
- Modify `backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs`
  - Add public `PaymentMethods` DTO.
- Modify `backend/src/RentACar.API/Services/PublicSiteSettingsService.cs`
  - Read payment method flags and include them in public settings.
- Modify `backend/src/RentACar.API/Contracts/Payments/PaymentRequests.cs`
  - Add optional `PaymentMethod` for `credit_card` and `debit_card`; old clients default to credit card.
- Modify `backend/src/RentACar.API/Services/PaymentService.cs`
  - Enforce online payment plus selected card method flag before provider intent creation.
- Modify `backend/src/RentACar.API/Services/ReservationService.cs`
  - Enforce unpaid request flag in `CreateUnpaidRequestAsync`.
- Modify `frontend/lib/api/publicSiteSettings.ts`, `frontend/lib/api/admin/types.ts`, `frontend/lib/api/payments.ts`
  - Add matching TypeScript types.
- Modify `frontend/lib/api/admin/settings.ts`, `frontend/hooks/admin/useAdminSettings.ts`
  - Make feature flag update name-based, not id-based.
- Modify `frontend/app/(admin)/dashboard/(auth)/settings/feature-flags/page.tsx`
  - Add grouped admin UI for payment method flags.
- Modify `frontend/app/(public)/[locale]/booking/step4/page.tsx`
  - Render active payment methods as one card group, including unpaid request.
- Modify `frontend/i18n/messages/*.json`
  - Add public unavailable/payment-method labels in all locales.
- Update tests under existing backend unit test files and `frontend/app/(public)/[locale]/booking/step4/BookingStep4.test.tsx`.

---

### Task 1: Backend Payment Method Flag Helper

**Files:**
- Create: `backend/src/RentACar.API/Services/PaymentMethodFeatureFlags.cs`
- Test: `backend/tests/RentACar.Tests/Unit/Services/FeatureFlagServiceTests.cs`

- [x] **Step 1: Add the helper**

```csharp
using Microsoft.EntityFrameworkCore;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces;

namespace RentACar.API.Services;

public sealed record PaymentMethodAvailability(
    bool OnlinePaymentEnabled,
    bool CreditCardEnabled,
    bool DebitCardEnabled,
    bool UnpaidRequestEnabled,
    bool PaypalEnabled)
{
    public bool AnyActionableEnabled => CreditCardEnabled || DebitCardEnabled || UnpaidRequestEnabled;
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
        (OnlinePayment, true, "Online odeme altyapisini etkinlestirir. Kart yontemleri ayrica yonetilir."),
        (CreditCard, true, "Public rezervasyon akisi icin kredi karti odeme kartini gosterir."),
        (DebitCard, true, "Public rezervasyon akisi icin banka karti odeme kartini gosterir."),
        (UnpaidRequest, true, "Public rezervasyon akisi icin odeme yapmadan 24 saat blokeli talep secenegini gosterir."),
        (PayPal, false, "PayPal provider entegrasyonu henuz aktif degildir; public secenek olarak kullanilmaz.")
    ];

    public static async Task<PaymentMethodAvailability> GetAvailabilityAsync(
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var flags = await dbContext.FeatureFlags
            .AsNoTracking()
            .Where(x => Required.Select(flag => flag.Name).Contains(x.Name))
            .ToDictionaryAsync(x => x.Name, x => x.Enabled, cancellationToken);

        static bool Resolve(
            IReadOnlyDictionary<string, bool> flags,
            string name,
            bool defaultValue)
        {
            return flags.TryGetValue(name, out var enabled) ? enabled : defaultValue;
        }

        return new PaymentMethodAvailability(
            Resolve(flags, OnlinePayment, true),
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
}
```

- [x] **Step 2: Run helper compile check**

Run:

```bash
dotnet build backend/RentACar.sln --no-restore
```

Expected: build fails only if namespace/import details need adjustment; fix those before continuing.

---

### Task 2: Required Feature Flags

**Files:**
- Modify: `backend/src/RentACar.API/Services/FeatureFlagService.cs`
- Test: `backend/tests/RentACar.Tests/Unit/Services/FeatureFlagServiceTests.cs`

- [x] **Step 1: Replace local required flag definitions**

In `FeatureFlagService.cs`, remove the private `OnlinePaymentDescription` constant and make `RequiredFlags` use the helper:

```csharp
private static readonly (string Name, bool Enabled, string Description)[] RequiredFlags =
[
    .. PaymentMethodFeatureFlags.Required,
    ("EnableSmsNotifications", true, "SMS bildirimlerinin gönderimini etkinleştirir"),
    ("EnableCampaigns", true, "Campaign and discount rules toggle"),
    ("EnableArabicLanguage", true, "Arabic (RTL) dil desteğini etkinleştirir"),
    ("MaintenanceMode", false, "Sistemi bakım moduna alır")
];
```

- [x] **Step 2: Update feature flag tests**

Add this test to `FeatureFlagServiceTests`:

```csharp
[Fact]
public async Task GetAllAsync_WhenPaymentMethodFlagsAreMissing_CreatesExpectedDefaults()
{
    await using var dbContext = CreateDbContext();
    var service = new FeatureFlagService(dbContext);

    var flags = await service.GetAllAsync(CancellationToken.None);

    flags.Should().Contain(x => x.Name == "EnableCreditCardPayment" && x.Enabled);
    flags.Should().Contain(x => x.Name == "EnableDebitCardPayment" && x.Enabled);
    flags.Should().Contain(x => x.Name == "EnableUnpaidReservationRequest" && x.Enabled);
    flags.Should().Contain(x => x.Name == "EnablePayPalPayment" && !x.Enabled);
}
```

Update existing online-payment expectations to the new description string:

```csharp
private const string OnlinePaymentDescription =
    "Online odeme altyapisini etkinlestirir. Kart yontemleri ayrica yonetilir.";
```

- [x] **Step 3: Run backend flag tests**

Run:

```bash
dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore --filter "FullyQualifiedName~RentACar.Tests.Unit.Services.FeatureFlagServiceTests"
```

Expected: all `FeatureFlagServiceTests` pass.

---

### Task 3: Public Settings Payment Methods Contract

**Files:**
- Modify: `backend/src/RentACar.API/Contracts/PublicSiteSettings/PublicSiteSettingsDtos.cs`
- Modify: `backend/src/RentACar.API/Services/PublicSiteSettingsService.cs`
- Test: `backend/tests/RentACar.Tests/Unit/Services/PublicSiteSettingsServiceTests.cs`

- [x] **Step 1: Add DTO**

Add before `PublicSiteSettingsDto`:

```csharp
public sealed record PublicPaymentMethodsDto(
    bool CreditCardEnabled,
    bool DebitCardEnabled,
    bool UnpaidRequestEnabled,
    bool PaypalEnabled,
    bool AnyEnabled);
```

Add this parameter to `PublicSiteSettingsDto` immediately before `bool OnlinePaymentEnabled`:

```csharp
PublicPaymentMethodsDto PaymentMethods,
```

- [x] **Step 2: Read all payment method flags**

In both `GetAsync` and `UpdateAsync`, replace the manual `EnableOnlinePayment` query with:

```csharp
var paymentMethods = await PaymentMethodFeatureFlags.GetAvailabilityAsync(dbContext, cancellationToken);
return Map(settings, paymentMethods);
```

Change the map signature:

```csharp
private static PublicSiteSettingsDto Map(PublicSiteSettings settings, PaymentMethodAvailability paymentMethods) => new(
```

Replace the old `onlinePaymentEnabled` argument with:

```csharp
new PublicPaymentMethodsDto(
    paymentMethods.OnlinePaymentEnabled && paymentMethods.CreditCardEnabled,
    paymentMethods.OnlinePaymentEnabled && paymentMethods.DebitCardEnabled,
    paymentMethods.UnpaidRequestEnabled,
    false,
    paymentMethods.AnyActionableEnabled),
paymentMethods.OnlinePaymentEnabled,
settings.UpdatedAt);
```

- [x] **Step 3: Add public settings tests**

Add to `PublicSiteSettingsServiceTests`:

```csharp
[Fact]
public async Task GetAsync_WhenPaymentMethodFlagsMissing_UsesSafeDefaults()
{
    await using var dbContext = CreateDbContext();
    var service = new PublicSiteSettingsService(dbContext);

    var settings = await service.GetAsync(CancellationToken.None);

    settings.PaymentMethods.CreditCardEnabled.Should().BeTrue();
    settings.PaymentMethods.DebitCardEnabled.Should().BeTrue();
    settings.PaymentMethods.UnpaidRequestEnabled.Should().BeTrue();
    settings.PaymentMethods.PaypalEnabled.Should().BeFalse();
    settings.PaymentMethods.AnyEnabled.Should().BeTrue();
}

[Fact]
public async Task GetAsync_WhenOnlinePaymentDisabled_HidesCardMethodsButKeepsUnpaidFlag()
{
    await using var dbContext = CreateDbContext();
    dbContext.FeatureFlags.AddRange(
        new RentACar.Core.Entities.FeatureFlag { Name = "EnableOnlinePayment", Enabled = false, Description = "test" },
        new RentACar.Core.Entities.FeatureFlag { Name = "EnableCreditCardPayment", Enabled = true, Description = "test" },
        new RentACar.Core.Entities.FeatureFlag { Name = "EnableDebitCardPayment", Enabled = true, Description = "test" },
        new RentACar.Core.Entities.FeatureFlag { Name = "EnableUnpaidReservationRequest", Enabled = true, Description = "test" });
    await dbContext.SaveChangesAsync();
    var service = new PublicSiteSettingsService(dbContext);

    var settings = await service.GetAsync(CancellationToken.None);

    settings.PaymentMethods.CreditCardEnabled.Should().BeFalse();
    settings.PaymentMethods.DebitCardEnabled.Should().BeFalse();
    settings.PaymentMethods.UnpaidRequestEnabled.Should().BeTrue();
    settings.PaymentMethods.AnyEnabled.Should().BeTrue();
}
```

- [x] **Step 4: Run public settings tests**

Run:

```bash
dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore --filter "FullyQualifiedName~RentACar.Tests.Unit.Services.PublicSiteSettingsServiceTests"
```

Expected: all `PublicSiteSettingsServiceTests` pass.

---

### Task 4: Backend Command Enforcement

**Files:**
- Modify: `backend/src/RentACar.API/Contracts/Payments/PaymentRequests.cs`
- Modify: `backend/src/RentACar.API/Services/PaymentService.cs`
- Modify: `backend/src/RentACar.API/Services/ReservationService.cs`
- Test: `backend/tests/RentACar.Tests/Unit/Services/PaymentServiceTests.cs`
- Test: `backend/tests/RentACar.Tests/Unit/Services/ReservationServiceTests.cs`

- [x] **Step 1: Add payment method to payment request**

In `CreatePaymentIntentApiRequest`:

```csharp
public string PaymentMethod { get; init; } = "credit_card";
```

- [x] **Step 2: Enforce selected card method**

In `PaymentService.CreateIntentAsync`, replace `EnsureOnlinePaymentEnabled();` with:

```csharp
var normalizedPaymentMethod = NormalizePaymentMethod(request.PaymentMethod);
EnsurePaymentMethodEnabled(normalizedPaymentMethod);
```

Replace `EnsureOnlinePaymentEnabled` with:

```csharp
private void EnsurePaymentMethodEnabled(string paymentMethod)
{
    var availability = PaymentMethodFeatureFlags.GetAvailabilityAsync(_dbContext, CancellationToken.None)
        .GetAwaiter()
        .GetResult();

    if (!PaymentMethodFeatureFlags.IsCardMethodEnabled(availability, paymentMethod))
    {
        throw new InvalidOperationException("Secilen odeme yontemi su anda aktif degil.");
    }
}

private static string NormalizePaymentMethod(string? paymentMethod)
{
    var normalized = string.IsNullOrWhiteSpace(paymentMethod)
        ? "credit_card"
        : paymentMethod.Trim().ToLowerInvariant();

    return normalized is "credit_card" or "debit_card"
        ? normalized
        : throw new InvalidOperationException("Desteklenmeyen odeme yontemi.");
}
```

- [x] **Step 3: Enforce unpaid request method**

At the beginning of `ReservationService.CreateUnpaidRequestAsync`, before creating customer/reservation state, add:

```csharp
var paymentMethods = await PaymentMethodFeatureFlags.GetAvailabilityAsync(_applicationDbContext, cancellationToken);
if (!paymentMethods.UnpaidRequestEnabled)
{
    throw new InvalidOperationException("Odeme yapmadan rezervasyon talebi su anda aktif degil.");
}
```

- [x] **Step 4: Update payment service tests**

Update `SeedFeatureFlag()` in `PaymentServiceTests` to seed:

```csharp
_dbContext.FeatureFlags.AddRange(
    new FeatureFlag { Name = "EnableOnlinePayment", Enabled = true, Description = "test" },
    new FeatureFlag { Name = "EnableCreditCardPayment", Enabled = true, Description = "test" },
    new FeatureFlag { Name = "EnableDebitCardPayment", Enabled = true, Description = "test" },
    new FeatureFlag { Name = "EnableUnpaidReservationRequest", Enabled = true, Description = "test" });
```

Update old disabled/missing online payment assertions to:

```csharp
.WithMessage("Secilen odeme yontemi su anda aktif degil.");
```

Add:

```csharp
[Fact]
public async Task CreateIntentAsync_WhenDebitCardFlagDisabled_ThrowsInvalidOperationException()
{
    var reservation = await SeedReservationAsync();
    _dbContext.FeatureFlags.Single(x => x.Name == "EnableDebitCardPayment").Enabled = false;
    await _dbContext.SaveChangesAsync();

    var request = CreatePaymentIntentRequest(reservation.Id, "debit-disabled") with
    {
        PaymentMethod = "debit_card"
    };

    var action = () => _sut.CreateIntentAsync(request, CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("Secilen odeme yontemi su anda aktif degil.");
}

[Fact]
public async Task CreateIntentAsync_WhenPaymentMethodIsPayPal_ThrowsInvalidOperationException()
{
    var reservation = await SeedReservationAsync();
    var request = CreatePaymentIntentRequest(reservation.Id, "paypal") with
    {
        PaymentMethod = "paypal"
    };

    var action = () => _sut.CreateIntentAsync(request, CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("Desteklenmeyen odeme yontemi.");
}
```

- [x] **Step 5: Add unpaid enforcement test**

In `ReservationServiceTests`, add a test using the existing unpaid request fixture style:

```csharp
[Fact]
public async Task CreateUnpaidRequestAsync_WhenUnpaidRequestFlagDisabled_ThrowsInvalidOperationException()
{
    _dbContext.FeatureFlags.Add(new FeatureFlag
    {
        Name = "EnableUnpaidReservationRequest",
        Enabled = false,
        Description = "test"
    });
    await _dbContext.SaveChangesAsync();

    var request = CreateValidReservationRequest();

    var action = () => _sut.CreateUnpaidRequestAsync(request, CancellationToken.None);

    await action.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("Odeme yapmadan rezervasyon talebi su anda aktif degil.");
}
```

- [x] **Step 6: Run backend enforcement tests**

Run:

```bash
dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore --filter "FullyQualifiedName~RentACar.Tests.Unit.Services.PaymentServiceTests|FullyQualifiedName~RentACar.Tests.Unit.Services.ReservationServiceTests"
```

Expected: all targeted service tests pass.

---

### Task 5: Admin Feature Flag UI

**Files:**
- Modify: `frontend/lib/api/admin/settings.ts`
- Modify: `frontend/hooks/admin/useAdminSettings.ts`
- Modify: `frontend/app/(admin)/dashboard/(auth)/settings/feature-flags/page.tsx`
- Test: `frontend/lib/api/admin/admin-api.test.ts`
- Test: `frontend/hooks/admin/admin-hooks.test.ts`
- Test: `frontend/app/(admin)/dashboard/(auth)/admin-pages-smoke.test.tsx`

- [x] **Step 1: Make update name-based**

In `frontend/lib/api/admin/settings.ts`:

```ts
export async function updateFeatureFlag(name: string, enabled: boolean) {
  const response = await adminPatch<AdminResponse<FeatureFlag>>(
    `${FEATURE_FLAGS_ENDPOINT}/${encodeURIComponent(name)}`,
    { enabled }
  );
  return unwrapResponse(response);
}
```

In `frontend/hooks/admin/useAdminSettings.ts`:

```ts
export async function mutateUpdateFeatureFlag(name: string, enabled: boolean) {
  return updateFeatureFlag(name, enabled);
}
```

- [x] **Step 2: Group payment method flags in UI**

In `feature-flags/page.tsx`, define:

```ts
const PAYMENT_FLAG_LABELS: Record<string, { title: string; description: string; disabled?: boolean }> = {
  EnableCreditCardPayment: {
    title: "Kredi Kartı",
    description: "Public ödeme ekranında kredi kartı seçeneğini gösterir.",
  },
  EnableDebitCardPayment: {
    title: "Banka Kartı",
    description: "Public ödeme ekranında banka kartı seçeneğini gösterir.",
  },
  EnableUnpaidReservationRequest: {
    title: "Ödemeden Rezervasyon",
    description: "Public ödeme ekranında ödeme yapmadan talep seçeneğini gösterir.",
  },
  EnablePayPalPayment: {
    title: "PayPal",
    description: "Provider entegrasyonu olmadığı için şimdilik public tarafta kullanılamaz.",
    disabled: true,
  },
};
```

Derive groups:

```ts
const paymentFlags = flags.filter((flag) => flag.name in PAYMENT_FLAG_LABELS);
const otherFlags = flags.filter((flag) => !(flag.name in PAYMENT_FLAG_LABELS));
```

Render payment flags first and call:

```tsx
<Switch
  checked={f.enabled}
  disabled={PAYMENT_FLAG_LABELS[f.name]?.disabled}
  onCheckedChange={(checked) => handleToggle(f.name, checked)}
/>
```

Render `otherFlags` below with the existing generic layout and `handleToggle(f.name, checked)`.

- [x] **Step 3: Update admin API tests**

Change the expectation in `admin-api.test.ts`:

```ts
await updateFeatureFlag("EnableCreditCardPayment", false);
expect(mockedPatch).toHaveBeenCalledWith(
  "/v1/feature-flags/EnableCreditCardPayment",
  { enabled: false }
);
```

- [x] **Step 4: Update admin UI smoke test**

In `admin-pages-smoke.test.tsx`, include payment flags in the mocked flags and assert:

```ts
expect(screen.getByText("Ödeme Yöntemleri")).toBeInTheDocument();
expect(screen.getByText("Kredi Kartı")).toBeInTheDocument();
expect(screen.getByText("Ödemeden Rezervasyon")).toBeInTheDocument();
```

For toggle:

```ts
await waitFor(() =>
  expect(mocks.mutateUpdateFeatureFlag).toHaveBeenCalledWith("EnableCreditCardPayment", true)
);
```

- [x] **Step 5: Run admin frontend tests**

Run:

```bash
corepack pnpm -C frontend test -- admin-api.test.ts admin-hooks.test.ts admin-pages-smoke.test.tsx
```

Expected: targeted frontend tests pass.

---

### Task 6: Public Step 4 Payment Method Cards

**Files:**
- Modify: `frontend/lib/api/publicSiteSettings.ts`
- Modify: `frontend/lib/api/admin/types.ts`
- Modify: `frontend/lib/api/payments.ts`
- Modify: `frontend/app/(public)/[locale]/booking/step4/page.tsx`
- Modify: `frontend/i18n/messages/en.json`
- Modify: `frontend/i18n/messages/tr.json`
- Modify: `frontend/i18n/messages/de.json`
- Modify: `frontend/i18n/messages/ru.json`
- Modify: `frontend/i18n/messages/ar.json`
- Test: `frontend/app/(public)/[locale]/booking/step4/BookingStep4.test.tsx`

- [x] **Step 1: Add TypeScript contracts**

In `frontend/lib/api/publicSiteSettings.ts`:

```ts
export interface PublicPaymentMethods {
  creditCardEnabled: boolean;
  debitCardEnabled: boolean;
  unpaidRequestEnabled: boolean;
  paypalEnabled: boolean;
  anyEnabled: boolean;
}

export type PublicRuntimeSiteSettings = PublicSiteSettings & {
  onlinePaymentEnabled: boolean;
  paymentMethods: PublicPaymentMethods;
};
```

In `frontend/lib/api/admin/types.ts`, add the same `paymentMethods` shape to `PublicSiteSettings` so admin/public type imports stay aligned.

In `frontend/lib/api/payments.ts`:

```ts
export type CardPaymentMethod = 'credit_card' | 'debit_card';

export interface CreatePaymentIntentRequest {
  reservationId: string;
  idempotencyKey: string;
  installmentCount?: number;
  paymentMethod?: CardPaymentMethod;
  card: PaymentCardRequest;
}
```

- [x] **Step 2: Refactor Step 4 method state**

Replace `onlinePaymentEnabled` state with:

```ts
type PaymentMethodId = "credit_card" | "debit_card" | "unpaid";

const defaultPaymentMethods = {
  creditCardEnabled: true,
  debitCardEnabled: true,
  unpaidRequestEnabled: true,
  paypalEnabled: false,
  anyEnabled: true,
};

const [paymentMethodsAvailability, setPaymentMethodsAvailability] = useState(defaultPaymentMethods);
const submitModeRef = useRef<PaymentMethodId>("credit_card");
```

When settings load:

```ts
setPaymentMethodsAvailability(settings.paymentMethods ?? {
  ...defaultPaymentMethods,
  creditCardEnabled: Boolean(settings.onlinePaymentEnabled),
  debitCardEnabled: Boolean(settings.onlinePaymentEnabled),
});
```

- [x] **Step 3: Build a single visible methods list**

Replace the current `paymentMethods` array with:

```ts
const paymentMethods = [
  paymentMethodsAvailability.creditCardEnabled
    ? {
        id: "credit_card" as const,
        name: t("payment.creditCard"),
        description: t("payment.creditCardDesc"),
        icon: <CreditCard className="h-5 w-5" />,
      }
    : null,
  paymentMethodsAvailability.debitCardEnabled
    ? {
        id: "debit_card" as const,
        name: t("payment.debitCard"),
        description: t("payment.debitCardDesc"),
        icon: <Banknote className="h-5 w-5" />,
      }
    : null,
  paymentMethodsAvailability.unpaidRequestEnabled
    ? {
        id: "unpaid" as const,
        name: t("unpaidRequest.title"),
        description: t("unpaidRequest.description"),
        icon: <Check className="h-5 w-5" />,
      }
    : null,
].filter((method): method is NonNullable<typeof method> => method !== null);
```

After this array is available, keep the selected method valid:

```ts
useEffect(() => {
  if (paymentMethods.length === 0) return;
  const selected = watch("paymentMethod");
  if (!selected || !paymentMethods.some((method) => method.id === selected)) {
    setValue("paymentMethod", paymentMethods[0].id);
  }
}, [paymentMethods, setValue, watch]);
```

- [x] **Step 4: Adjust schema and submit path**

Use:

```ts
paymentMethod: z.enum(["credit_card", "debit_card", "unpaid"]).optional(),
```

Validation should skip card fields when:

```ts
if (submitModeRef.current === "unpaid") return true;
```

On submit:

```ts
const selectedMethod = data.paymentMethod ?? paymentMethods[0]?.id;
if (!selectedMethod) {
  toast.error(t("paymentMethodsUnavailable"));
  return;
}

if (selectedMethod === "unpaid") {
  const reservation = await createUnpaidReservationRequest(reservationData);
  const queryParams = new URLSearchParams(searchParams.toString());
  queryParams.set("code", reservation.publicCode);
  queryParams.set("request", "unpaid");
  router.push(`/${locale}/booking/confirmation?${queryParams.toString()}`);
  return;
}
```

When creating a card payment intent:

```ts
const paymentResult: PaymentIntentResponse = await createPaymentIntent({
  reservationId: reservation.id,
  idempotencyKey: crypto.randomUUID(),
  paymentMethod: selectedMethod,
  card: {
    holderName: data.cardHolder ?? "",
    number: data.cardNumber?.replace(/\s/g, "") ?? "",
    expiryMonth,
    expiryYear,
    cvv: data.cvv ?? "",
  },
});
```

- [x] **Step 5: Remove bottom unpaid button**

Delete the secondary button that renders `sendUnpaidRequest` below the primary submit. The only unpaid entry point must be the method card.

Primary button text:

```tsx
{isSubmitting
  ? t("completing")
  : selectedPaymentMethod === "unpaid"
    ? t("completeRequest")
    : t("completeBooking")}
```

Disable when no methods:

```tsx
disabled={isSubmitting || paymentMethods.length === 0}
```

Show unavailable state near the payment card area:

```tsx
{paymentMethods.length === 0 && (
  <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
    {t("paymentMethodsUnavailable")}
  </div>
)}
```

- [x] **Step 6: Add translations**

Add under `booking` in every locale file:

```json
"paymentMethodsUnavailable": "Şu anda online rezervasyon için aktif ödeme yöntemi bulunmuyor. Lütfen bizimle iletişime geçin."
```

Use equivalent localised text for `en`, `de`, `ru`, and `ar`.

- [x] **Step 7: Update Step 4 tests**

Replace the old disabled-online-payment unpaid test with:

```ts
it("shows unpaid reservation as a payment method card and submits unpaid request", async () => {
  const user = userEvent.setup();
  getPublicSiteSettingsMock.mockResolvedValueOnce({
    onlinePaymentEnabled: true,
    paymentMethods: {
      creditCardEnabled: true,
      debitCardEnabled: true,
      unpaidRequestEnabled: true,
      paypalEnabled: false,
      anyEnabled: true,
    },
  });

  render(<BookingStep4Page />);

  await user.click(await screen.findByLabelText(/request without online payment/i));
  await user.click(screen.getByRole("checkbox"));
  await user.click(screen.getByRole("button", { name: /send request/i }));

  await waitFor(() => {
    expect(createUnpaidReservationRequestMock).toHaveBeenCalled();
  });
  expect(createReservationMock).not.toHaveBeenCalled();
  expect(createPaymentIntentMock).not.toHaveBeenCalled();
});
```

Add all-disabled test:

```ts
it("blocks submission when no payment methods are enabled", async () => {
  getPublicSiteSettingsMock.mockResolvedValueOnce({
    onlinePaymentEnabled: false,
    paymentMethods: {
      creditCardEnabled: false,
      debitCardEnabled: false,
      unpaidRequestEnabled: false,
      paypalEnabled: false,
      anyEnabled: false,
    },
  });

  render(<BookingStep4Page />);

  expect(await screen.findByText(/no active payment method/i)).toBeInTheDocument();
  expect(screen.getByRole("button", { name: /complete/i })).toBeDisabled();
});
```

Update card payment expectation:

```ts
expect(createPaymentIntentMock).toHaveBeenCalledWith({
  reservationId: "res-123",
  idempotencyKey: "uuid-123",
  paymentMethod: "credit_card",
  card: {
    holderName: "Jane Doe",
    number: "4111111111111111",
    expiryMonth: "12",
    expiryYear: "30",
    cvv: "123",
  },
});
```

- [x] **Step 8: Run Step 4 tests**

Run:

```bash
corepack pnpm -C frontend test -- BookingStep4.test.tsx
```

Expected: Step 4 tests pass.

---

### Task 7: Unit Verification and Security Review

**Files:**
- No planned file edits unless tests reveal a concrete defect.

- [x] **Step 1: Run backend targeted tests**

```bash
dotnet test backend/tests/RentACar.Tests/RentACar.Tests.csproj --no-restore --filter "FullyQualifiedName~FeatureFlagServiceTests|FullyQualifiedName~PublicSiteSettingsServiceTests|FullyQualifiedName~PaymentServiceTests|FullyQualifiedName~ReservationServiceTests"
```

Expected: all targeted backend tests pass.

- [x] **Step 2: Run frontend targeted tests**

```bash
corepack pnpm -C frontend test -- BookingStep4.test.tsx admin-api.test.ts admin-hooks.test.ts admin-pages-smoke.test.tsx
```

Expected: all targeted frontend tests pass.

- [ ] **Step 3: Run full relevant suites if targeted tests pass**

```bash
dotnet test backend/RentACar.sln --no-build
corepack pnpm -C frontend test
```

Expected: both suites pass, except for known environment-only blockers that must be documented with exact error output.

- [ ] **Step 4: Scoped security review**

Review only these trust-boundary concerns:

- Direct `POST /api/v1/payments/intents` cannot create a payment when the selected card method is disabled.
- Direct `POST /api/v1/reservations/unpaid-requests` cannot create unpaid requests when the unpaid method is disabled.
- PayPal cannot be used to create a reservation or payment intent in v1.
- Admin feature flag mutation remains SuperAdmin-only through existing controller policy.

Expected: no material bypass remains in the changed scope.

---

### Task 8: Final Docker and Browser Verification

**Files:**
- No planned file edits unless Docker/browser verification reveals a concrete defect.

- [ ] **Step 1: Start the full local Docker stack**

Run from the repository root:

```bash
docker compose -f backend/docker-compose.yml up --build -d
```

Expected:

- `rentacar-postgres` is healthy.
- `rentacar-redis` is healthy.
- `rentacar-api` is running on `http://localhost:5000`.
- `rentacar-web` is running on `http://localhost:3001`.

- [ ] **Step 2: Check container status and logs**

Run:

```bash
docker compose -f backend/docker-compose.yml ps
docker compose -f backend/docker-compose.yml logs api --tail 120
docker compose -f backend/docker-compose.yml logs web --tail 120
```

Expected:

- No API startup migration failure.
- No frontend runtime startup failure.
- No repeated 500-level startup errors in the visible logs.

- [ ] **Step 3: Verify public API contracts from Docker runtime**

Run:

```bash
curl http://localhost:5000/api/v1/public-site-settings
curl http://localhost:5000/api/admin/v1/feature-flags
```

Expected:

- Public settings response includes `paymentMethods`.
- `paymentMethods.creditCardEnabled`, `paymentMethods.debitCardEnabled`, and `paymentMethods.unpaidRequestEnabled` are `true` by default.
- `paymentMethods.paypalEnabled` is `false`.
- Admin feature flags endpoint may return `401` or `403` without admin auth; that is acceptable and confirms it is not public.

- [ ] **Step 4: Open Docker web app in browser**

Open:

```text
http://localhost:3001
```

Use the browser tool or Playwright to verify:

- Home page loads without blank screen.
- Public booking flow reaches `/tr/booking/step4` or equivalent locale URL.
- Step 4 shows credit card, debit card, and unpaid reservation as cards in the same payment method group.
- The old bottom secondary unpaid-request button is not present.
- PayPal is not selectable as an actionable payment method.

- [ ] **Step 5: Browser-check admin payment method controls**

Open:

```text
http://localhost:3001/dashboard/settings/feature-flags
```

Use the existing local admin credentials from the project seed/test setup. Verify:

- Admin can access the Feature Flags page after login.
- A dedicated payment-method section is visible.
- Credit card, debit card, and unpaid reservation toggles are visible and enabled by default.
- PayPal is visibly disabled or marked unavailable because provider integration is not implemented.
- Toggling credit card, debit card, or unpaid reservation persists after page refresh.

- [ ] **Step 6: Browser-check disabled-method behaviour**

Using the admin UI, disable all actionable methods:

- `EnableCreditCardPayment = false`
- `EnableDebitCardPayment = false`
- `EnableUnpaidReservationRequest = false`

Then open public Step 4 again in the browser and verify:

- The payment method card list is empty or replaced by the unavailable message.
- The submit button is disabled.
- No card form is visible.
- No unpaid request can be submitted.

Re-enable the default methods before leaving the stack:

- `EnableCreditCardPayment = true`
- `EnableDebitCardPayment = true`
- `EnableUnpaidReservationRequest = true`

- [ ] **Step 7: Browser-check direct happy paths**

With defaults re-enabled, verify:

- Selecting credit card shows card fields and attempts payment intent creation after valid form data.
- Selecting debit card shows the same card fields and sends `paymentMethod: "debit_card"`.
- Selecting unpaid reservation hides card fields, creates an unpaid request, and redirects to confirmation with `request=unpaid`.

- [ ] **Step 8: Browser/API bypass checks**

While all actionable methods are disabled, verify direct calls cannot bypass backend enforcement:

```bash
curl -X POST http://localhost:5000/api/v1/payments/intents
curl -X POST http://localhost:5000/api/v1/reservations/unpaid-requests
```

Expected:

- Calls without valid payload may return validation errors; if valid test payloads are used, disabled methods must return a business error instead of creating records.
- No reservation or payment intent is created for a disabled public method.

- [ ] **Step 9: Capture final evidence**

Record the final verification evidence in the implementation PR or handoff:

- Docker command used.
- `docker compose ps` result summary.
- Public settings `paymentMethods` response summary.
- Browser URLs checked.
- Screenshots or Playwright evidence for public Step 4 and admin Feature Flags.
- Any failed command output and whether it is product-related or environment-related.

- [ ] **Step 10: Stop Docker stack when verification is complete**

Run:

```bash
docker compose -f backend/docker-compose.yml down
```

Expected: local containers stop cleanly. Do not remove volumes unless the implementer explicitly needs a clean database rehearsal.
