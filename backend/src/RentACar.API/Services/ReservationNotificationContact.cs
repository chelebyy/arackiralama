using RentACar.Core.Entities;

namespace RentACar.API.Services;

internal sealed record ReservationNotificationContact(string Email, string Phone, string Nationality)
{
    public static ReservationNotificationContact FromGuest(Reservation reservation) =>
        new(reservation.GuestEmail ?? string.Empty, reservation.GuestPhone ?? string.Empty,
            reservation.GuestLocale ?? "tr");

    public static ReservationNotificationContact FromCustomer(Customer customer) =>
        new(customer.Email, customer.Phone, customer.Nationality);
}
