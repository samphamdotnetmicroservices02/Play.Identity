using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Play.Identity.Contracts;
using Play.Identity.Service.Entities;
using Play.Identity.Service.Exceptions;

namespace Play.Identity.Service.Consumers;

public class DebitGilConsumer : IConsumer<DebitGil>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DebitGilConsumer> _logger;

    public DebitGilConsumer(UserManager<ApplicationUser> userManager, ILogger<DebitGilConsumer> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DebitGil> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received debit gil request of {Gil} gil from user {UserId} with CorrelationId {CorrelationId}"
            , message.Gil
            , message.UserId
            , message.CorrelationId);

        var user = await _userManager.FindByIdAsync(message.UserId.ToString());

        if (user is null)
        {
            throw new UnknownUserException(message.UserId);
        }

        if (user.MessageIds.Contains(context.MessageId.Value))
        {
            await context.Publish(new GilDebited(message.CorrelationId));
            return;
        }

        user.Gil -= message.Gil;

        if (user.Gil < 0)
        {
            _logger.LogError(
            "InsufficientFunds  of {Gil} gil from user {UserId} with CorrelationId {CorrelationId}"
            , message.Gil
            , message.UserId
            , message.CorrelationId);
            throw new InsufficientFundsException(message.UserId, message.Gil);
        }

        user.MessageIds.Add(context.MessageId.Value);

        await _userManager.UpdateAsync(user);

        var gilDebitedTask = context.Publish(new GilDebited(message.CorrelationId));
        var userUpdatedTask = context.Publish(new UserUpdated(user.Id, user.Email, user.Gil));

        await Task.WhenAll(gilDebitedTask, userUpdatedTask);
    }
}