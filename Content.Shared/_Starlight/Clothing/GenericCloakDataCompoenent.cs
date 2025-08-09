using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Clothing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(ClothingSystem))]
public sealed partial class GenericCloakDataComponent : Component
{
    [DataField, AutoNetworkedField] public Color Color;

    [DataField, AutoNetworkedField] public bool Rainbow;
}