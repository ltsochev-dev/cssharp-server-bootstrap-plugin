using CounterStrikeSharp.API.Core;

namespace ServerBootstrap.Contracts
{
    public interface IGameModeController
    {
        string Mode { get; }

        void Activate();
        void Deactivate();
    }
}
