using System.Text;
using Kernel.Boxing;
using Kernel.Common;
using Kernel.Configuration;
using Kernel.Email;
using Kernel.Json;
using Kernel.Messaging;
using Kernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Shopping.Tests;

public class EmailChannelTests : IClassFixture<ShoppingApiTestFactory>
{
    private readonly ShoppingApiTestFactory _factory;

    public EmailChannelTests(ShoppingApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void EmailMessage_Initialization_SetsPropertiesCorrectly()
    {
        var attachmentContent = Encoding.UTF8.GetBytes("Test Attachment");
        var attachment = new EmailAttachment
        {
            FileName = "test.txt",
            Content = attachmentContent,
            ContentType = "text/plain"
        };

        var message = new EmailMessage
        {
            To = "recipient@example.com",
            Cc = "cc@example.com",
            Bcc = "bcc@example.com",
            Subject = "Test Subject",
            Body = "<p>Test Body</p>",
            IsHtml = true,
            Attachments = [attachment]
        };

        Assert.Equal("recipient@example.com", message.To);
        Assert.Equal("cc@example.com", message.Cc);
        Assert.Equal("bcc@example.com", message.Bcc);
        Assert.Equal("Test Subject", message.Subject);
        Assert.Equal("<p>Test Body</p>", message.Body);
        Assert.True(message.IsHtml);
        Assert.NotNull(message.Attachments);
        Assert.Single(message.Attachments);
        Assert.Equal("test.txt", message.Attachments[0].FileName);
        Assert.Equal("text/plain", message.Attachments[0].ContentType);
        Assert.Equal(attachmentContent, message.Attachments[0].Content);
    }

    [Fact]
    public void EmailSettings_DefaultValues_AreCorrect()
    {
        var jsonSerializer = new JsonSerializer();
        var settings = new EmailSettings(jsonSerializer);

        Assert.Equal("Email", settings.SectionName);
        Assert.False(string.IsNullOrWhiteSpace(settings.SmtpHost));
        Assert.True(settings.SmtpPort > 0);
        Assert.True(settings.EnableSsl);
        Assert.False(string.IsNullOrWhiteSpace(settings.FromAddress));
        Assert.False(string.IsNullOrWhiteSpace(settings.FromName));
    }

    [Fact]
    public void EmailSettings_DynamicMutation_UpdatesMergedValues()
    {
        using var scope = _factory.Services.CreateScope();
        var dynamicSettings = scope.ServiceProvider.GetRequiredService<DynamicSettings>();
        var settings = dynamicSettings.GetSection<EmailSettings>();

        var originalHost = settings.SmtpHost;
        try
        {
            settings.SetValue("SmtpHost", "mail.example.org");
            Assert.Equal("mail.example.org", settings.SmtpHost);
        }
        finally
        {
            settings.SetValue("SmtpHost", originalHost);
        }
    }

    [Fact]
    public async Task DefaultEmailTemplateRenderer_RendersVariables_WithDoubleAndSingleBraces()
    {
        var renderer = new DefaultEmailTemplateRenderer();
        var template = "Hello {{Name}}, your confirmation code is {Code}. Welcome to {{Company}}!";
        var variables = new Dictionary<string, string>
        {
            ["Name"] = "Alice",
            ["code"] = "123456",
            ["COMPANY"] = "MyStore"
        };

        var result = await renderer.RenderAsync(template, variables);

        Assert.Equal("Hello Alice, your confirmation code is 123456. Welcome to MyStore!", result);
    }

    [Fact]
    public async Task DefaultEmailTemplateRenderer_NonExistentFile_FallsBackToRawString()
    {
        var renderer = new DefaultEmailTemplateRenderer();
        var rawText = "Simple plain text with no tokens.";
        var result = await renderer.RenderAsync(rawText, []);

        Assert.Equal(rawText, result);
    }

    [Fact]
    public async Task SmtpEmailSender_InvalidRecipient_ReturnsFailure()
    {
        var jsonSerializer = new JsonSerializer();
        var settings = new EmailSettings(jsonSerializer);
        var sender = new SmtpEmailSender(settings, NullLogger<SmtpEmailSender>.Instance);

        var message = new EmailMessage
        {
            To = "",
            Subject = "Test",
            Body = "Test"
        };

        var result = await sender.SendAsync(message);

        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.IsType<ArgumentException>(result.Error);
    }

    [Fact]
    public async Task EmailMessageProcessor_GetChannel_ReturnsEmailChannel()
    {
        var fakeSender = new FakeEmailSender();
        var processor = new EmailMessageProcessor(
            fakeSender,
            new JsonSerializer(),
            NullLogger<EmailMessageProcessor>.Instance);

        Assert.Equal(MessageChannel.Email, processor.GetChannel());
    }

    [Fact]
    public async Task EmailMessageProcessor_ValidMessage_InvokesEmailSender()
    {
        var fakeSender = new FakeEmailSender();
        var jsonSerializer = new JsonSerializer();
        var processor = new EmailMessageProcessor(
            fakeSender,
            jsonSerializer,
            NullLogger<EmailMessageProcessor>.Instance);

        var email = new EmailMessage
        {
            To = "user@test.com",
            Subject = "Welcome!",
            Body = "Hello world",
            Attachments =
            [
                new EmailAttachment
                {
                    FileName = "doc.pdf",
                    Content = [1, 2, 3, 4],
                    ContentType = "application/pdf"
                }
            ]
        };

        var message = new Message
        {
            Type = typeof(EmailMessage).AssemblyQualifiedName!,
            Content = jsonSerializer.Serialize(email),
            Channel = MessageChannel.Email,
            Partition = 0
        };

        var result = await processor.ProcessMessage(message);

        Assert.True(result.IsSuccess);
        Assert.Single(fakeSender.SentMessages);
        var sent = fakeSender.SentMessages[0];
        Assert.Equal("user@test.com", sent.To);
        Assert.Equal("Welcome!", sent.Subject);
        Assert.NotNull(sent.Attachments);
        Assert.Single(sent.Attachments);
        Assert.Equal("doc.pdf", sent.Attachments[0].FileName);
    }

    [Fact]
    public async Task EmailMessageProcessor_InvalidJson_ReturnsFailure()
    {
        var fakeSender = new FakeEmailSender();
        var processor = new EmailMessageProcessor(
            fakeSender,
            new JsonSerializer(),
            NullLogger<EmailMessageProcessor>.Instance);

        var message = new Message
        {
            Type = typeof(EmailMessage).AssemblyQualifiedName!,
            Content = "invalid-json-content",
            Channel = MessageChannel.Email,
            Partition = 0
        };

        var result = await processor.ProcessMessage(message);

        Assert.True(result.IsFailure);
        Assert.IsType<InvalidOperationException>(result.Error);
        Assert.Empty(fakeSender.SentMessages);
    }

    [Fact]
    public async Task EmailMessageProcessor_SenderFailure_ReturnsFailure()
    {
        var failingSender = new FailingEmailSender();
        var jsonSerializer = new JsonSerializer();
        var processor = new EmailMessageProcessor(
            failingSender,
            jsonSerializer,
            NullLogger<EmailMessageProcessor>.Instance);

        var email = new EmailMessage
        {
            To = "user@test.com",
            Subject = "Test",
            Body = "Test"
        };

        var message = new Message
        {
            Type = typeof(EmailMessage).AssemblyQualifiedName!,
            Content = jsonSerializer.Serialize(email),
            Channel = MessageChannel.Email,
            Partition = 0
        };

        var result = await processor.ProcessMessage(message);

        Assert.True(result.IsFailure);
        Assert.Equal("SMTP connection timeout", result.Error!.Message);
    }

    [Fact]
    public async Task MessageDispatcher_RoutesEmailChannel_ToEmailProcessor()
    {
        var fakeSender = new FakeEmailSender();
        var jsonSerializer = new JsonSerializer();
        var emailProcessor = new EmailMessageProcessor(
            fakeSender,
            jsonSerializer,
            NullLogger<EmailMessageProcessor>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IMessageProcessor>(emailProcessor);
        var sp = services.BuildServiceProvider();

        var dispatcher = new MessageDispatcher(sp, NullLogger<MessageDispatcher>.Instance);

        var email = new EmailMessage
        {
            To = "dispatcher@test.com",
            Subject = "Routed Email",
            Body = "Through dispatcher"
        };

        var message = new Message
        {
            Type = typeof(EmailMessage).AssemblyQualifiedName!,
            Content = jsonSerializer.Serialize(email),
            Channel = MessageChannel.Email,
            Partition = 0
        };

        var result = await dispatcher.DispatchAsync(message);

        Assert.True(result.IsSuccess);
        Assert.Single(fakeSender.SentMessages);
        Assert.Equal("dispatcher@test.com", fakeSender.SentMessages[0].To);
    }

    [Fact]
    public async Task OutboxPipeline_WritingEmailMessage_PersistsToKernelDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var messageWriter = scope.ServiceProvider.GetRequiredService<IMessageWriter>();
        var db = scope.ServiceProvider.GetRequiredService<KernelDbContext>();
        var jsonSerializer = scope.ServiceProvider.GetRequiredService<IJsonSerializer>();

        var email = new EmailMessage
        {
            To = "outbox@test.com",
            Subject = "Outbox Email",
            Body = "Persisted to outbox",
            Attachments =
            [
                new EmailAttachment
                {
                    FileName = "receipt.pdf",
                    Content = Encoding.UTF8.GetBytes("receipt content"),
                    ContentType = "application/pdf"
                }
            ]
        };

        var writeResult = await messageWriter.WriteAsync(email, MessageChannel.Email);
        Assert.True(writeResult.IsSuccess);

        // Verify message exists in database
        var persistedMessage = db.Messages
            .OrderByDescending(m => m.OccurredAt)
            .FirstOrDefault(m => m.Channel == MessageChannel.Email);

        Assert.NotNull(persistedMessage);
        Assert.Equal(MessageChannel.Email, persistedMessage.Channel);

        // Verify message payload can be deserialized back to EmailMessage with attachment
        var deserialized = jsonSerializer.Deserialize<EmailMessage>(persistedMessage.Content);
        Assert.NotNull(deserialized);
        Assert.Equal("outbox@test.com", deserialized.To);
        Assert.Equal("Outbox Email", deserialized.Subject);
        Assert.NotNull(deserialized.Attachments);
        Assert.Single(deserialized.Attachments);
        Assert.Equal("receipt.pdf", deserialized.Attachments[0].FileName);
        Assert.Equal(Encoding.UTF8.GetBytes("receipt content"), deserialized.Attachments[0].Content);
    }

    private class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> SentMessages { get; } = [];

        public Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return Task.FromResult(Result.Success());
        }
    }

    private class FailingEmailSender : IEmailSender
    {
        public Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Failure(new InvalidOperationException("SMTP connection timeout")));
        }
    }
}
