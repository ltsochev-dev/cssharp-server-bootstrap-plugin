using Microsoft.Extensions.Logging;

namespace ServerBootstrap.Controllers
{
    public sealed class DeathmatchController : GameControllerBase
    {
        public override string Mode => "deathmatch";
        public DeathmatchController(ServerBootstrap plugin) : base(plugin) { }

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
