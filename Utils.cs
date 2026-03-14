using CounterStrikeSharp.API.Core;

namespace ServerBootstrap
{
    public sealed class Utils
    {
        public static bool isInvalidPlayer(CCSPlayerController? player)
        {
            return player == null || player.IsHLTV || player.IsBot || !player.IsValid;
        }
    }
}
