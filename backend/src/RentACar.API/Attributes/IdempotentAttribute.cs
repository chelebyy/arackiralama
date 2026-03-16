namespace RentACar.API.Attributes;

/// <summary>
/// Marks an endpoint as idempotent. Requests with the same Idempotency-Key
/// header will return the cached response instead of re-executing the operation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class IdempotentAttribute : Attribute
{
    /// <summary>
    /// How long the idempotency key and cached response should be stored.
    /// Default is 24 hours.
    /// </summary>
    public int ExpirationHours { get; set; } = 24;

    /// <summary>
    /// Whether to require the Idempotency-Key header. If false, missing header
    /// will allow the request through without caching.
    /// </summary>
    public bool RequireKey { get; set; } = false;
}
