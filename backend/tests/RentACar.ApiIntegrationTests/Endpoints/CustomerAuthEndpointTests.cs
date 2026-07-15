using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentACar.API.Contracts.Auth;
using RentACar.API.Services;
using RentACar.ApiIntegrationTests.Infrastructure;
using RentACar.Core.Constants;
using RentACar.Core.Entities;
using RentACar.Core.Interfaces.Notifications;
using Xunit;

namespace RentACar.ApiIntegrationTests.Endpoints;

public sealed class CustomerAuthEndpointTests(RedisFixture redisFixture) : ApiIntegrationTestBase(redisFixture)
{
    [Fact]
    public async Task CustomerRegister_ConcurrentClaimRequests_QueueOnlyOneClaimEmail()
    {
        var email = $"claim-register-{Guid.NewGuid():N}@rentacar.test";
        var customerId = await WithDbContextAsync(async dbContext =>
        {
            var customer = new Customer
            {
                Email = email,
                FullName = "Claim Registration Customer",
                Phone = "+90 555 000 00 07",
                IdentityNumber = string.Empty,
                Nationality = "TR",
                LicenseYear = 0,
                PasswordHash = null
            };

            dbContext.Customers.Add(customer);
            await dbContext.SaveChangesAsync();
            return customer.Id;
        });

        var requests = Enumerable.Range(0, 2)
            .Select(_ => Client.PostAsJsonAsync(
                "/api/customer/v1/auth/register",
                new CustomerRegisterRequest(email, "IgnoredPassword1!", null, null)));

        var responses = await Task.WhenAll(requests);
        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);

        await WithDbContextAsync(async dbContext =>
        {
            (await dbContext.CustomerAccountClaimTokens.CountAsync(token => token.CustomerId == customerId))
                .Should().Be(1);
            (await dbContext.BackgroundJobs.CountAsync(job =>
                job.Type == BackgroundJobTypes.NotificationEmailSend
                && job.Payload.Contains(NotificationTemplateKeys.CustomerAccountClaim)
                && job.Payload.Contains(email)))
                .Should().Be(1);

            return true;
        });
    }

    [Fact]
    public async Task CustomerAccountClaim_ConcurrentRequests_OnlyOneSucceeds()
    {
        var rawToken = $"claim-{Guid.NewGuid():N}";
        string tokenHash;
        using (var scope = Services.CreateScope())
        {
            tokenHash = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().HashRefreshToken(rawToken);
        }

        var customerId = await WithDbContextAsync(async dbContext =>
        {
            var customer = new Customer
            {
                Email = $"claim-{Guid.NewGuid():N}@rentacar.test",
                FullName = "Claim Integration Customer",
                Phone = "+90 555 000 00 06",
                IdentityNumber = "22222222222",
                Nationality = "TR",
                LicenseYear = 2018
            };

            dbContext.Customers.Add(customer);
            await dbContext.SaveChangesAsync();

            dbContext.CustomerAccountClaimTokens.Add(new CustomerAccountClaimToken
            {
                CustomerId = customer.Id,
                TokenHash = tokenHash,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
            });
            await dbContext.SaveChangesAsync();

            return customer.Id;
        });

        var requests = Enumerable.Range(0, 2)
            .Select(_ => Client.PostAsJsonAsync(
                "/api/customer/v1/auth/claim",
                new CustomerAccountClaimRequest(rawToken, "ConcurrentClaimPassword1!")));

        var responses = await Task.WhenAll(requests);
        var responseDetails = await Task.WhenAll(responses.Select(async response =>
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"));

        responses.Count(response => response.StatusCode == HttpStatusCode.OK)
            .Should().Be(1, string.Join(Environment.NewLine, responseDetails));
        responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest)
            .Should().Be(1, string.Join(Environment.NewLine, responseDetails));

        await WithDbContextAsync(async dbContext =>
        {
            var customer = await dbContext.Customers.SingleAsync(candidate => candidate.Id == customerId);
            var token = await dbContext.CustomerAccountClaimTokens.SingleAsync(candidate => candidate.TokenHash == tokenHash);

            customer.HasPassword.Should().BeTrue();
            customer.TokenVersion.Should().Be(1);
            token.ConsumedAtUtc.Should().NotBeNull();
            (await dbContext.AuditLogs.CountAsync(candidate =>
                candidate.Action == "CustomerClaimRejected"
                && candidate.EntityId == customerId.ToString())).Should().Be(1);

            return true;
        });
    }
}
