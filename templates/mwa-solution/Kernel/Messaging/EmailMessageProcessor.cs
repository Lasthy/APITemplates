using Kernel.Boxing;
using Kernel.Common;
using Kernel.Email;
using Kernel.Json;
using Microsoft.Extensions.Logging;

namespace Kernel.Messaging;

/// <summary>
/// Processes <see cref="MessageChannel.Email"/> messages from the outbox.
/// Deserializes the message content into an <see cref="EmailMessage"/> and sends it via <see cref="IEmailSender"/>.
/// </summary>
public class EmailMessageProcessor(
    IEmailSender _emailSender,
    IJsonSerializer _jsonSerializer,
    ILogger<EmailMessageProcessor> _logger
) : IMessageProcessor
{
    public MessageChannel GetChannel() => MessageChannel.Email;

    public async Task<Result> ProcessMessage(Message message, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Processing email message: Id={MessageId}, Channel={Channel}",
                message.Id, message.Channel);

            EmailMessage? emailMessage;
            try
            {
                emailMessage = _jsonSerializer.Deserialize<EmailMessage>(message.Content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize content of message {MessageId} to EmailMessage.", message.Id);
                return Result.Failure(
                    new InvalidOperationException(
                        $"Message {message.Id} content could not be deserialized to EmailMessage.", ex));
            }

            if (emailMessage is null)
            {
                return Result.Failure(
                    new InvalidOperationException(
                        $"Message {message.Id} content could not be deserialized to EmailMessage."));
            }

            var sendResult = await _emailSender.SendAsync(emailMessage, cancellationToken);
            if (sendResult.IsFailure)
            {
                _logger.LogError(sendResult.Error, "Failed to send email for message {MessageId}.", message.Id);
                return sendResult;
            }

            _logger.LogInformation("Processed email message Id={MessageId} successfully.", message.Id);
            return Result.Success();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while processing email message Id={MessageId}.", message.Id);
            return Result.Failure(e);
        }
    }
}
