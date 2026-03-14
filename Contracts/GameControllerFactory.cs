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
                "deathmatch" => new DeathmatchController(plugin),
                "competitive" => new CompetitiveController(plugin),
                //"scrim" => new ScrimController(plugin),
                _ => null
            };
        }
    }
}
