using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json.Serialization;

namespace ServerBootstrap.Config.Dto
{
    public sealed class TeamsDto
    {
        [JsonPropertyName("teamA")]
        public List<string> TeamA { get; init; } = new();

        [JsonPropertyName("teamB")]
        public List<string> TeamB { get; init; } = new();

        public List<string> AllPlayers => [.. TeamA, .. TeamB];

        public CsTeam? FindPlayerTeam(ulong? steamID)
        {
            if (steamID == null) return null;

            string sidString = steamID.ToString()!;

            if (TeamA.Contains(sidString))
            {
                return CsTeam.CounterTerrorist;
            }

            if (TeamB.Contains(sidString))
            {
                return CsTeam.Terrorist;
            }

            return null;
        }
    }
}
