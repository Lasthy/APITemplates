using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Kernel.Common;
using Microsoft.Extensions.Logging;

namespace Kernel.Email;

/// <summary>
/// Default SMTP implementation of <see cref="IEmailSender"/>.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(EmailSettings settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.To))
        {
            return Result.Failure(new ArgumentException("Recipient address 'To' cannot be empty.", nameof(message)));
        }

        try
        {
            using var mailMessage = new MailMessage();
            var from = string.IsNullOrWhiteSpace(_settings.FromName)
                ? new MailAddress(_settings.FromAddress)
                : new MailAddress(_settings.FromAddress, _settings.FromName);

            mailMessage.From = from;

            // Support comma- or semicolon-separated recipient addresses
            foreach (var to in SplitAddresses(message.To))
            {
                mailMessage.To.Add(new MailAddress(to));
            }

            if (!string.IsNullOrWhiteSpace(message.Cc))
            {
                foreach (var cc in SplitAddresses(message.Cc))
                {
                    mailMessage.CC.Add(new MailAddress(cc));
                }
            }

            if (!string.IsNullOrWhiteSpace(message.Bcc))
            {
                foreach (var bcc in SplitAddresses(message.Bcc))
                {
                    mailMessage.Bcc.Add(new MailAddress(bcc));
                }
            }

            mailMessage.Subject = message.Subject;
            mailMessage.Body = message.Body;
            mailMessage.IsBodyHtml = message.IsHtml;

            // Add attachments if any
            if (message.Attachments != null && message.Attachments.Count > 0)
            {
                foreach (var attachment in message.Attachments)
                {
                    var stream = new MemoryStream(attachment.Content);
                    var mailAttachment = string.IsNullOrWhiteSpace(attachment.ContentType)
                        ? new Attachment(stream, attachment.FileName, MediaTypeNames.Application.Octet)
                        : new Attachment(stream, attachment.FileName, attachment.ContentType);

                    mailMessage.Attachments.Add(mailAttachment);
                }
            }

            using var smtpClient = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                EnableSsl = _settings.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_settings.SmtpUsername))
            {
                smtpClient.Credentials = new NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword);
            }

            _logger.LogInformation(
                "Sending email to '{To}' with subject '{Subject}' via {SmtpHost}:{SmtpPort}",
                message.To, message.Subject, _settings.SmtpHost, _settings.SmtpPort);

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);

            _logger.LogInformation("Email sent successfully to '{To}'.", message.To);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to '{To}' with subject '{Subject}'.", message.To, message.Subject);
            return Result.Failure(ex);
        }
    }

    private static IEnumerable<string> SplitAddresses(string addressList)
    {
        return addressList.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
