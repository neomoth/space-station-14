using Content.Server.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionSetCharges : OnSignAction
{
    /// <summary>
    /// What game rules are added once signatures are collected and with a bit of luck.
    /// </summary>
    [DataField]
    public int Charges = 1;
    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        component.Charges = Charges;
        return false;
    }

    public override void ResolveIoC()
    {
        
    }
}