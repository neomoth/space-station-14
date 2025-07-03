namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionClearSigners : OnSignAction
{
    /// <summary>
    /// What game rules are added once signatures are collected and with a bit of luck.
    /// </summary>
    [DataField]
    public int Charges = 1;
    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        component.Signers = [];
        return false;
    }

    public override void ResolveIoC()
    {
    }
}