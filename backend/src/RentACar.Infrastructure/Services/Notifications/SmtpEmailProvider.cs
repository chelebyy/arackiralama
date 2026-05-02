using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentACar.Core.Interfaces.Notifications;

namespace RentACar.Infrastructure.Services.Notifications;

public sealed class SmtpEmailProvider(
    IOptions<NotificationOptions> notificationOptions,
    ILogger<SmtpEmailProvider> logger) : IEmailProvider
{
    private readonly NotificationOptions _options = notificationOptions.Value;
    private readonly ILogger<SmtpEmailProvider> _logger = logger;

    public async Task<EmailSendResult> SendAsync(
        EmailMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var emailOptions = _options.Email;
        if (!emailOptions.Enabled ||
            string.IsNullOrWhiteSpace(emailOptions.Smtp.Host) ||
            string.IsNullOrWhiteSpace(emailOptions.FromEmail))
        {
            return new EmailSendResult
            {
                Provider = "Smtp",
                FailureCode = "EMAIL_NOT_CONFIGURED",
                FailureMessage = "SMTP notification provider is not configured."
            };
        }

        using var message = new MailMessage
        {
            From = CreateFromAddress(emailOptions),
            Subject = request.Subject,
            Body = request.HtmlBody ?? request.PlainTextBody,
            IsBodyHtml = !string.IsNullOrWhiteSpace(request.HtmlBody)
        };
        message.To.Add(request.ToEmail);

        using var client = new SmtpClient(emailOptions.Smtp.Host, emailOptions.Smtp.Port)
        {
            EnableSsl = emailOptions.Smtp.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(emailOptions.Smtp.Username))
        {
            client.Credentials = new NetworkCredential(
                emailOptions.Smtp.Username,
                emailOptions.Smtp.Password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation(
                "SMTP email sent to {DestinationEmail} with subject {Subject}",
                request.ToEmail,
                request.Subject);

            return new EmailSendResult
            {
                Success = true,
                Provider = "Smtp"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP email send failed for {DestinationEmail}", request.ToEmail);

            return new EmailSendResult
            {
                Provider = "Smtp",
                FailureCode = "EMAIL_SEND_FAILED",
                FailureMessage = ex.Message
            };
        }
    }

    private static MailAddress CreateFromAddress(EmailNotificationOptions emailOptions)
    {
        return string.IsNullOrWhiteSpace(emailOptions.FromName)
            ? new MailAddress(emailOptions.FromEmail)
            : new MailAddress(emailOptions.FromEmail, emailOptions.FromName);
    }
}
