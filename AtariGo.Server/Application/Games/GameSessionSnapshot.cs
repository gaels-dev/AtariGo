using System;
using System.Collections.Generic;

namespace AtariGo.Server.Application.Games;

public sealed record GameSessionSnapshot
{
    public long GameId { get; init; }

    public int BoardSize { get; init; }

    public int CaptureGoal { get; init; }

    public int GameTimeSeconds { get; init; }

    public int TurnTimeSeconds { get; init; }

    public GameSessionParticipant Player { get; init; } = null!;

    public GameSessionParticipant Opponent { get; init; } = null!;

    public bool OpponentConnected { get; init; }

    public bool OpponentDisconnected { get; init; }

    public int MaximumChatMessageLength { get; init; }

    public IReadOnlyList<GameSessionChatMessage> ChatHistory { get; init; } =
        Array.Empty<GameSessionChatMessage>();

    public IReadOnlyList<GameSessionBoardCell> Cells { get; init; } =
        Array.Empty<GameSessionBoardCell>();
}
