using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// Information for a borg paint that can be selected by <see cref="BorgSwitchableTypeComponent"/> for borg type.
/// </summary>
/// <seealso cref="SharedBorgSwitchableTypeSystem"/>
[Prototype]
public sealed partial class BorgPaintPrototype : IPrototype
{
    [IdDataField]
    public required string ID { get; set; }

    [DataField]
    public string? Name;

    //
    // Visual information
    //

    /// <summary>
    /// The path to the borg type's sprites.
    /// </summary>
    [DataField]
    public string SpritePath { get; set; } = "Mobs/Silicon/Chassis/generic.rsi";

    /// <summary>
    /// The sprite state for the main borg body.
    /// </summary>
    [DataField]
    public string SpriteBodyState { get; set; } = "borg";

    /// <summary>
    /// An optional movement sprite state for the main borg body.
    /// </summary>
    [DataField]
    public string? SpriteBodyMovementState { get; set; }

    /// <summary>
    /// Sprite state used to indicate that the borg has a mind in it.
    /// </summary>
    /// <seealso cref="BorgChassisComponent.HasMindState"/>
    [DataField]
    public string SpriteHasMindState { get; set; } = "borg_e";

    /// <summary>
    /// Sprite state used to indicate that the borg has no mind in it.
    /// </summary>
    /// <seealso cref="BorgChassisComponent.NoMindState"/>
    [DataField]
    public string SpriteNoMindState { get; set; } = "borg_e_r";

    /// <summary>
    /// Sprite state used when the borg's flashlight is on.
    /// </summary>
    [DataField]
    public string SpriteToggleLightState { get; set; } = "borg_l";

    /// <summary>
    /// Optional price for this paint. If null, the paint is free.
    /// </summary>
    [DataField]
    public int? Price = null;
}