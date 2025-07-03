using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionSetTarget : OnSignAction
{
    /// <summary>
    /// how many charges should the paper have?
    /// </summary>
    [DataField(required: true)]
    public ProtoId<OnSignActionsPrototype> Actions = default;
    
    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        component.OnSignActionProto = Actions;
        return false;
    }

    public override void ResolveIoC()
    {
    }
}