using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Play.Common.Settings;
using Play.Identity.Contracts;
using Play.Identity.Service.Entities;
using Play.Identity.Service.Exceptions;

namespace Play.Identity.Service.Consumers;

public class DebitGilConsumer : IConsumer<DebitGil>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DebitGilConsumer> _logger;
    private readonly Counter<int> _gilDebitedCounter;

    public DebitGilConsumer(UserManager<ApplicationUser> userManager, ILogger<DebitGilConsumer> logger, IConfiguration configuration)
    {
        _userManager = userManager;
        _logger = logger;

        /*
        * Premetheus: we're going to be needing the service name of our microservice to define what we call a Meter that will also
        * lat us create the counters. The Meter is the entry point for all the metrics tracking of your microservice. So usually 
        * you'll have at least one Meter that owns everything related to metrics in your microservice.
        */
        var settings = configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
        Meter meter = new(settings.ServiceName);
        _gilDebitedCounter = meter.CreateCounter<int>("GilDebited");
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

        _gilDebitedCounter.Add(1, new KeyValuePair<string, object>(nameof(message.Gil), message.Gil)); // boxing ItemId to object

        var gilDebitedTask = context.Publish(new GilDebited(message.CorrelationId));
        var userUpdatedTask = context.Publish(new UserUpdated(user.Id, user.Email, user.Gil));

        await Task.WhenAll(gilDebitedTask, userUpdatedTask);
    }
}