using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared._Starlight.Silicons.Borgs; // Starlight-edit
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client.Silicons.Borgs;

/// <summary>
/// Client side logic for borg type switching. Sets up primarily client-side visual information.
/// </summary>
/// <seealso cref="SharedBorgSwitchableTypeSystem"/>
/// <seealso cref="BorgSwitchableTypeComponent"/>
public sealed class BorgSwitchableTypeSystem : SharedBorgSwitchableTypeSystem
{
    [Dependency] private readonly BorgSystem _borgSystem = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgSwitchableTypeComponent, AfterAutoHandleStateEvent>(AfterStateHandler);
        SubscribeLocalEvent<BorgSwitchableTypeComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(Entity<BorgSwitchableTypeComponent> ent, ref ComponentStartup args)
    {
        UpdateEntityAppearance(ent);
    }

    private void AfterStateHandler(Entity<BorgSwitchableTypeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateEntityAppearance(ent);
    }

    protected override void UpdateEntityAppearance(
        Entity<BorgSwitchableTypeComponent> entity,
        BorgTypePrototype prototype, // Starlight-edit
        BorgPaintPrototype paint) // Starlight-edit
    {
        if (TryComp(entity, out SpriteComponent? sprite))
        {
            if (_resourceCache.TryGetResource<RSIResource>(
                    SpriteSpecifierSerializer.TextureRoot / paint.SpritePath, // Starlight-edit
                    out var res))
            {
                sprite.BaseRSI = res.RSI;
            }
            _sprite.LayerSetRsiState((entity, sprite), BorgVisualLayers.Body, paint.SpriteBodyState); // Starlight-edit
            _sprite.LayerSetRsiState((entity, sprite), BorgVisualLayers.LightStatus, paint.SpriteToggleLightState); // Starlight-edit
        }

        if (TryComp(entity, out BorgChassisComponent? chassis))
        {
            _borgSystem.SetMindStates(
                (entity.Owner, chassis),
                paint.SpriteHasMindState, // Starlight-edit
                paint.SpriteNoMindState); // Starlight-edit

            if (TryComp(entity, out AppearanceComponent? appearance))
            {
                // Queue update so state changes apply.
                _appearance.QueueUpdate(entity, appearance);
            }
        }

        base.UpdateEntityAppearance(entity, prototype, paint); // Starlight-edit
    }
}
