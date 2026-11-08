using AtariGo.Server.Application.Rooms;

namespace AtariGo.Server.Application.RoomInvitations;

public sealed class RoomInvitationExpiryService : BackgroundService
{
    private static readonly TimeSpan s_interval = TimeSpan.FromSeconds(1);
    private readonly IRoomsService _rooms;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RoomInvitationExpiryService> _logger;

    public RoomInvitationExpiryService(
        IRoomsService rooms,
        TimeProvider timeProvider,
        ILogger<RoomInvitationExpiryService> logger)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _rooms = rooms;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(s_interval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _rooms.ExpireRoomInvitations();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Room invitation expiry worker stopped with the host.");
        }
    }
}
