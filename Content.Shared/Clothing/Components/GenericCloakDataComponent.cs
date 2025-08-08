using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.Clothing.Components;

[NetworkedComponent, ComponentProtoName("GenericCapeData"), Access(typeof(ClothingSystem))]
public sealed partial class GenericCloakDataComponent : Component
{
    /// <summary>
    /// The color of the cloak
    /// </summary>
    [DataField]
    public Color Color;

    /// <summary>
    /// If true, ignore set color and cycle through the rainbow.
    /// </summary>
    [DataField]
    public bool Rainbow;
}
