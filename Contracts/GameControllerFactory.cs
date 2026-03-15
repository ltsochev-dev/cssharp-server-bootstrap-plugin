using ServerBootstrap.Config.Dto;
using ServerBootstrap.Controllers;

namespace ServerBootstrap.Contracts
{
    public static class GameModeControllerFactory
    {
        public static IGameModeController? Create(
            ServerBootstrap plugin,
            ServerAnnotationsDto dto)
        {
            return dto.Mode.ToLowerInvariant() switch
            {
                "deathmatch" => new DeathmatchController(plugin, dto),
                "competitive" => new CompetitiveController(plugin, dto),
                //"scrim" => new ScrimController(plugin, dto),
                _ => null
            };
        }
    }
}
