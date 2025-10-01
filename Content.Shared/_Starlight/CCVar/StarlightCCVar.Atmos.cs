using Robust.Shared.Configuration;

namespace Content.Shared.Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Toggles for every (Hopefully) Pipe to allow docking
    /// </summary>
    public static readonly CVarDef<bool> DockPipeStraight =
        CVarDef.Create("atmos.DockPipeStraight", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockPipeHalf =
        CVarDef.Create("atmos.DockPipeHalf", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockPipeBend =
        CVarDef.Create("atmos.DockPipeBend", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockPipeTJunction =
        CVarDef.Create("atmos.DockPipeTJunction", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockPipeFourway =
        CVarDef.Create("atmos.DockPipeFourway", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockPipeManifold =
        CVarDef.Create("atmos.DockPipeManifold", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockPressurePump =
        CVarDef.Create("atmos.DockPressurePump", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockVolumePump =
        CVarDef.Create("atmos.DockVolumePump", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockPassiveGate =
        CVarDef.Create("atmos.DockPassiveGate", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockValve =
        CVarDef.Create("atmos.DockValve", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockSignalValve =
        CVarDef.Create("atmos.DockSignalValve", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockPressureRegulator =
        CVarDef.Create("atmos.DockPressureRegulator", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockPipeSensor =
        CVarDef.Create("atmos.DockPipeSensor", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockFilter =
        CVarDef.Create("atmos.DockFilter", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockMixer =
        CVarDef.Create("atmos.DockMixer", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockPneumaticValve =
        CVarDef.Create("atmos.DockPneumaticValve", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockHeatExchanger =
        CVarDef.Create("atmos.DockHeatExchanger", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockHeatExchangerBend =
        CVarDef.Create("atmos.DockHeatExchangerBend", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockRecycler =
        CVarDef.Create("atmos.DockRecycler", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockVentPump =
        CVarDef.Create("atmos.DockVentPump", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockPassiveVent =
        CVarDef.Create("atmos.DockPassiveVent", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockVentScrubber =
        CVarDef.Create("atmos.DockVentScrubber", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockOutletInjector =
        CVarDef.Create("atmos.DockOutletInjector", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockThermoMachineFreezer =
        CVarDef.Create("atmos.DockThermoMachineFreezer", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockThermoMachineHeater =
        CVarDef.Create("atmos.DockThermoMachineHeater", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockThermoMachineHellfireFreezer =
        CVarDef.Create("atmos.DockThermoMachineHellfireFreezer", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockThermoMachineHellfireHeater =
        CVarDef.Create("atmos.DockThermoMachineHellfireHeater", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockCondenser =
        CVarDef.Create("atmos.DockCondenser", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> DockPort =
        CVarDef.Create("atmos.DockPort", true, CVar.REPLICATED | CVar.SERVER);
}