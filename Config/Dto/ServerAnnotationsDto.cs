namespace ServerBootstrap.Config.Dto
{
    public sealed class ServerAnnotationsDto
    {
        public string Mode { get; init; } = default!;

        public string MatchId { get; init; } = default!;
        public string? Map { get; init; }
        public int? MaxPlayers { get; init; }
        public int? WarmupTimeoutSeconds { get; init; }
        public bool EnableHltv { get; init; }
        public TeamsDto? Teams { get; init; }
    }
}
