using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Clothing.Components;

[NetworkedComponent, ComponentProtoName("GenericCloakData"), Access(typeof(ClothingSystem))]
public abstract partial class SharedGenericCloakDataComponent : Component
{
	/// <summary>
	/// The color of the cloak
	/// </summary>
	[DataField] public Color Color;

	/// <summary>
	/// If true, ignore set color and cycle through the rainbow.
	/// </summary>
	[DataField] public bool Rainbow;
}

[NetSerializable, Serializable]
public sealed class GenericCloakDataComponentState : ComponentState
{
	public readonly Color Color;
	public readonly bool Rainbow;

	public GenericCloakDataComponentState(Color color, bool rainbow)
	{
		Color = color;
		Rainbow = rainbow;
	}
}