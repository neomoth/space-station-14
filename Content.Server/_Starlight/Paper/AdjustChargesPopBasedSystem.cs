using Robust.Shared.Player;

namespace Content.Server._Starlight.Paper;

public sealed class AdjustChargesPopBasedSystem : EntitySystem
{

    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AdjustChargesPopBasedComponent, ComponentInit>(OnComponentInit);
    }

    private void OnComponentInit(EntityUid uid, AdjustChargesPopBasedComponent comp, ComponentInit init)
    {
        if (!TryComp<ActionsOnSignComponent>(uid, out var actions))
            return;
        actions.Charges = (int)Math.Ceiling(_playerManager.PlayerCount * comp.Percent.Float());
    }
}
