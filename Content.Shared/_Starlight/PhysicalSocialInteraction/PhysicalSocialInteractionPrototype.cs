using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.PhysicalSocialInteraction;

[Prototype("physicalsocialinteraction")]
public sealed partial class PhysicalSocialInteractionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
}
