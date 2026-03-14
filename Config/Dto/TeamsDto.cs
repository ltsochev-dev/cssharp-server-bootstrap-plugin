using System.Text.Json.Serialization;

namespace ServerBootstrap.Config.Dto
{
    public sealed class TeamsDto
    {
        [JsonPropertyName("teamA")]
        public List<string> TeamA { get; init; } = new();

        [JsonPropertyName("teamB")]
        public List<string> TeamB { get; init; } = new();
    }
}
