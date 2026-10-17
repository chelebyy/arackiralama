using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using StackExchange.Redis;
using RentACar.API.Attributes;

namespace RentACar.API.Middleware;

/// <summary>
/// Middleware that implements idempotency for POST/PUT/PATCH requests.
/// If an Idempotency-Key header is provided, the response is cached in Redis
/// and subsequent requests with the same key return the cached response.
/// </summary>
public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string IdempotencyStatusHeader = "X-Idempotency-Status";
    private const string RedisKeyPrefix = "idempotent:v2:";

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConnectionMultiplexer redis)
    {
        // Only process POST, PUT, PATCH requests
        if (!HttpMethods.IsPost(context.Request.Method) &&
            !HttpMethods.IsPut(context.Request.Method) &&
            !HttpMethods.IsPatch(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Check if endpoint has IdempotentAttribute
        var endpoint = context.GetEndpoint();
        var idempotentAttribute = endpoint?.Metadata.GetMetadata<IdempotentAttribute>();

        if (idempotentAttribute == null)
        {
            await _next(context);
            return;
        }

        // Get Idempotency-Key from header
        if (!context.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var keyValues) ||
            StringValues.IsNullOrEmpty(keyValues))
        {
            if (idempotentAttribute.RequireKey)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"success\":false,\"message\":\"Idempotency-Key header is required.\"}");
                return;
            }

            // No key provided and not required - proceed normally
            await _next(context);
            return;
        }

        var idempotencyKey = keyValues.ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await _next(context);
            return;
        }

        // Validate key format (should be a valid UUID or similar)
        if (idempotencyKey.Length < 8 || idempotencyKey.Length > 128)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"success\":false,\"message\":\"Invalid Idempotency-Key format.\"}");
            return;
        }

        if (idempotentAttribute.UseQuoteReplay && await HasQuoteRequestAsync(context.Request))
        {
            await _next(context);
            return;
        }

        var redisKey = CreateRedisKey(context, idempotencyKey);
        var requestFingerprint = await GetRequestFingerprintAsync(context.Request);

        // Try to get cached response from Redis
        CachedResponse? cached = null;
        try
        {
            var db = redis.GetDatabase();
            var cachedResponse = await db.StringGetAsync(redisKey);
            if (cachedResponse.HasValue)
            {
                cached = JsonSerializer.Deserialize<CachedResponse>(cachedResponse.ToString());
            }
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, skipping idempotency check");
            context.Response.Headers[IdempotencyStatusHeader] = "BYPASS";
            await _next(context);
            return;
        }

        if (cached != null)
        {
            if (!string.Equals(cached.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"success\":false,\"message\":\"Idempotency-Key was already used with a different request.\"}");
                return;
            }

            _logger.LogInformation(
                "Returning cached idempotent response");

            context.Response.StatusCode = cached.StatusCode;
            context.Response.ContentType = cached.ContentType ?? "application/json";
            context.Response.Headers[IdempotencyStatusHeader] = "HIT";
            await context.Response.WriteAsync(cached.Body);
            return;
        }

        // No cached response - execute request and cache result
        var originalBodyStream = context.Response.Body;
        var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            // Read response body
            memoryStream.Position = 0;
            var responseBody = await new StreamReader(memoryStream, leaveOpen: true).ReadToEndAsync();

            // Cache response if successful (2xx status codes)
            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                var cacheEntry = new CachedResponse
                {
                    StatusCode = context.Response.StatusCode,
                    ContentType = context.Response.ContentType,
                    Body = responseBody,
                    RequestFingerprint = requestFingerprint
                };

                var expiration = TimeSpan.FromHours(idempotentAttribute.ExpirationHours);
                try
                {
                    var db = redis.GetDatabase();
                    await db.StringSetAsync(
                        redisKey,
                        JsonSerializer.Serialize(cacheEntry),
                        expiration);

                    _logger.LogInformation(
                        "Cached idempotent response, expires in {Hours}h",
                        idempotentAttribute.ExpirationHours);
                }
                catch (RedisConnectionException ex)
                {
                    _logger.LogWarning(ex, "Redis unavailable, response not cached");
                }
            }

            context.Response.Headers[IdempotencyStatusHeader] = "MISS";
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
            await memoryStream.DisposeAsync();
        }
    }

    private static string CreateRedisKey(HttpContext context, string idempotencyKey)
    {
        var principalId = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub")
            : null;
        var scope = JsonSerializer.Serialize(new
        {
            context.Request.Method,
            Path = context.Request.PathBase.Add(context.Request.Path).Value,
            Query = context.Request.QueryString.Value,
            context.Request.ContentType,
            PrincipalId = principalId,
            PrincipalType = principalId is null ? null : context.User.FindFirstValue("principal_type"),
            SessionId = context.Request.Headers["X-Session-Id"].ToString().Trim(),
            IdempotencyKey = idempotencyKey
        });
        return RedisKeyPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope)));
    }

    private static async Task<string> GetRequestFingerprintAsync(HttpRequest request)
    {
        request.EnableBuffering();
        try
        {
            var hash = await SHA256.HashDataAsync(request.Body, request.HttpContext.RequestAborted);
            return Convert.ToHexString(hash);
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static async Task<bool> HasQuoteRequestAsync(HttpRequest request)
    {
        request.EnableBuffering();
        try
        {
            using var body = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
            return body.RootElement.ValueKind != JsonValueKind.Object ||
                body.RootElement.EnumerateObject().Any(property =>
                    (string.Equals(property.Name, "quoteId", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(property.Name, "vehicleId", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind != JsonValueKind.Null);
        }
        catch (JsonException)
        {
            return true;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private sealed class CachedResponse
    {
        public int StatusCode { get; set; }
        public string? ContentType { get; set; }
        public string Body { get; set; } = string.Empty;
        public string RequestFingerprint { get; set; } = string.Empty;
    }
}
