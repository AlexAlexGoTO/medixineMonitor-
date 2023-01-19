﻿using MediatR;
using MedixineMonitor.Application.Common.Abstraction;
using MedixineMonitor.Application.Common.Interfaces;
using MedixineMonitor.Domain.Entities;
using MedixineMonitor.Domain.Events;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MedixineMonitor.Application.Observations.EventHandlers;

public class ObservationCreatedEventHandler : INotificationHandler<ObservationCreatedEvent>
{
    private readonly ILogger<ObservationCreatedEventHandler> _logger;
    private readonly ICacheStore _cacheStore;
    private readonly IHubContext<BaseAlertHub> _hubContext;
    private readonly IAlertService _alertService;

    public ObservationCreatedEventHandler(
        ILogger<ObservationCreatedEventHandler> logger, 
        ICacheStore cacheStore,
        IHubContext<BaseAlertHub> hubContext,
        IAlertService alertService)
    {
        _logger = logger;
        _cacheStore = cacheStore;
        _hubContext = hubContext;
        _alertService = alertService;
    }

    public async Task Handle(ObservationCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("CleanArchitecture Domain Event: {DomainEvent}", notification.GetType().Name);

        var observation = notification.Observation;

        //the idea is to skip another database round trip and keep alerts in MemoryStore, it can be Redis or something similar
        //in current scenario we keep only last few observations in memory
        //also it should be based on some logic and criterias so it can be different for each Type of observation
        //Also criterias should be specific individially for each person +/or default value
        var cashedObservation = _cacheStore.Get<List<Observation>>($"{observation.PatientId}_{observation.Type}");
        if (cashedObservation == null || cashedObservation.Count == 0)
        {
            var toCache = new List<Observation>() { observation };

            _cacheStore.Add(toCache, $"{observation.PatientId}_{observation.Type}", TimeSpan.FromDays(5));
        }
        else
        {
            if (cashedObservation.Count > 2)
            {
                var avarage = cashedObservation.TakeLast(3).Average(co => co.Value);

                var permissibleValue = avarage += avarage * 0.05;

                if (observation.Value > permissibleValue)
                {
                    var alert = new SystemAlert
                    {
                        Id = Guid.NewGuid(),
                        ItemId = observation.Id,
                        PatientId = observation.PatientId,
                        Message = $"Allowed value exceeded. Type: {observation.Type}; Description: {observation.Description}"
                    };

                    await _hubContext.Clients.All.SendAsync("channel", alert);

                    await _alertService.CreateAlert(alert);
                }
            }

            cashedObservation.Add(observation);
        }
    }
}
