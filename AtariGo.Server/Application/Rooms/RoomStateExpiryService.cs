namespace AtariGo.Server.Application.Rooms;

public sealed class RoomStateExpiryService : BackgroundService
{
    private const int ExpiryCheckIntervalSeconds = 1;

    private static readonly TimeSpan expiryCheckInterval =
        TimeSpan.FromSeconds(ExpiryCheckIntervalSeconds);

    private readonly IRoomsService _roomsService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RoomStateExpiryService> _logger;

    public RoomStateExpiryService(
        IRoomsService roomsService,
        TimeProvider timeProvider,
        ILogger<RoomStateExpiryService> logger)
    {
        ArgumentNullException.ThrowIfNull(roomsService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _roomsService = roomsService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(expiryCheckInterval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _roomsService.RemoveExpiredRoomsAndInvitations();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Room state expiry worker stopped with the host.");
        }
    }
}
