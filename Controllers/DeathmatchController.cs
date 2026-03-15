using Microsoft.Extensions.Logging;
using ServerBootstrap.Config.Dto;

namespace ServerBootstrap.Controllers
{
    public sealed class DeathmatchController : GameControllerBase
    {
        public override string Mode => "deathmatch";
        public DeathmatchController(ServerBootstrap plugin, ServerAnnotationsDto dto) : base(plugin, dto) { }

        public override void Activate()
        {
            Logger.LogInformation("[Deathmatch] Activated.");
        }

        public override void Deactivate()
        {
            Logger.LogInformation("[Deathmatch] Deactivated.");
        }
    }
}
