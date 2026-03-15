using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace ServerBootstrap
{
    public sealed class Utils
    {
        public static bool isInvalidPlayer(CCSPlayerController? player)
        {
            return player == null || player.IsHLTV || player.IsBot || !player.IsValid;
        }

        public static CsTeam GetTeamWithLeastPlayers()
        {
            int tCount = Utilities.GetPlayers().Count(p => p.TeamNum == (byte)CsTeam.Terrorist && !p.IsBot);
            int ctCount = Utilities.GetPlayers().Count(p => p.TeamNum == (byte)CsTeam.CounterTerrorist && !p.IsBot);

            return tCount <= ctCount ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
        }
        public static int GetConnectedHumanPlayers()
        {
            return Utilities.GetPlayers().Count(player =>
                player != null &&
                player.IsValid &&
                !player.IsBot);
        }
        public static int GetConnectedCompetitivePlayers()
        {
            return Utilities.GetPlayers().Count(player =>
                player != null &&
                player.IsValid &&
                !player.IsBot &&
                (player.Team == CsTeam.Terrorist || player.Team == CsTeam.CounterTerrorist));
        }
    }
}
