using System.Linq; // Starlight-edit
using Content.Shared._Starlight.Silicons.Borgs; // Starlight-edit
using Content.Shared.Starlight; // Starlight-edit
using Content.Shared.Actions;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared.Silicons.Borgs;

/// <summary>
/// Implements borg type switching.
/// </summary>
/// <seealso cref="BorgSwitchableTypeComponent"/>
public abstract class SharedBorgSwitchableTypeSystem : EntitySystem
{
    // TODO: Allow borgs to be reset to default configuration.

    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _userInterface = default!;
    [Dependency] protected readonly IPrototypeManager Prototypes = default!;
    [Dependency] private readonly InteractionPopupSystem _interactionPopup = default!;
    [Dependency] private readonly ISharedPlayersRoleManager _playerRoles = default!; // Starlight-edit

    public static readonly EntProtoId ActionId = "ActionSelectBorgType";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgSwitchableTypeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BorgSwitchableTypeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BorgSwitchableTypeComponent, BorgToggleSelectTypeEvent>(OnSelectBorgTypeAction);

        Subs.BuiEvents<BorgSwitchableTypeComponent>(BorgSwitchableTypeUiKey.SelectBorgType,
            sub =>
            {
                sub.Event<BorgSelectTypeMessage>(SelectTypeMessageHandler);
            });
    }

    //
    // UI-adjacent code
    //

    private void OnMapInit(Entity<BorgSwitchableTypeComponent> ent, ref MapInitEvent args)
    {
        _actionsSystem.AddAction(ent, ref ent.Comp.SelectTypeAction, ActionId);
        Dirty(ent);

        // Starlight-start: Borg paints
        if (ent.Comp.SelectedBorgType != null
            && Prototypes.TryIndex<BorgTypePrototype>(ent.Comp.SelectedBorgType, out var borgTypePrototype))
        {
            if (borgTypePrototype.BasicPaint != null)
                SelectBorgModule(ent, ent.Comp.SelectedBorgType.Value, borgTypePrototype.BasicPaint.Value);
            else if (borgTypePrototype.Paints.Count > 0)
                SelectBorgModule(ent, ent.Comp.SelectedBorgType.Value, borgTypePrototype.Paints.First());
        }
        // Starlight-end
    }

    private void OnShutdown(Entity<BorgSwitchableTypeComponent> ent, ref ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(ent.Owner, ent.Comp.SelectTypeAction);
    }

    private void OnSelectBorgTypeAction(Entity<BorgSwitchableTypeComponent> ent, ref BorgToggleSelectTypeEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;

        _userInterface.TryToggleUi((ent.Owner, null), BorgSwitchableTypeUiKey.SelectBorgType, actor.PlayerSession);
    }

    private void SelectTypeMessageHandler(Entity<BorgSwitchableTypeComponent> ent, ref BorgSelectTypeMessage args)
    {
        if (ent.Comp.SelectedBorgType != null)
            return;

        if (!Prototypes.HasIndex(args.Prototype))
            return;

        // Starlight-start: Handle paint cost
        if (!Prototypes.TryIndex(args.Paint, out var paint))
            return;

        if (paint.Price is not null and > 0)
        {
            if (_playerRoles.GetPlayerData(ent.Owner) is not PlayerData playerData
                || playerData.Balance < paint.Price)
                return;

            playerData.Balance -= paint.Price.Value;
        }
        // Starlight-end

        SelectBorgModule(ent, args.Prototype, args.Paint); // Starlight-edit
    }

    //
    // Implementation
    //

    protected virtual void SelectBorgModule(
        Entity<BorgSwitchableTypeComponent> ent,
        ProtoId<BorgTypePrototype> borgType, // Starlight-edit
        ProtoId<BorgPaintPrototype> borgPaint) // Starlight-edit
    {
        ent.Comp.SelectedBorgType = borgType;
        ent.Comp.SelectedBorgPaint = borgPaint; // Starlight-edit

        _actionsSystem.RemoveAction(ent.Owner, ent.Comp.SelectTypeAction);
        ent.Comp.SelectTypeAction = null;
        Dirty(ent);

        _userInterface.CloseUi((ent.Owner, null), BorgSwitchableTypeUiKey.SelectBorgType);

        UpdateEntityAppearance(ent);
    }

    protected void UpdateEntityAppearance(Entity<BorgSwitchableTypeComponent> entity)
    {
        if (!Prototypes.TryIndex(entity.Comp.SelectedBorgType, out var proto)
            || !Prototypes.TryIndex(entity.Comp.SelectedBorgPaint, out var paint)) // Starlight-edit
            return;

        UpdateEntityAppearance(entity, proto, paint); // Starlight-edit
    }

    protected virtual void UpdateEntityAppearance(
        Entity<BorgSwitchableTypeComponent> entity,
        BorgTypePrototype prototype, // Starlight-edit
        BorgPaintPrototype paint) // Starlight-edit
    {
        if (TryComp(entity, out InteractionPopupComponent? popup))
        {
            _interactionPopup.SetInteractSuccessString((entity.Owner, popup), prototype.PetSuccessString);
            _interactionPopup.SetInteractFailureString((entity.Owner, popup), prototype.PetFailureString);
        }

        if (TryComp(entity, out FootstepModifierComponent? footstepModifier))
        {
            footstepModifier.FootstepSoundCollection = prototype.FootstepCollection;
        }

        // Starlight-start: Movement sprite state

        if (paint.SpriteBodyMovementState is { } movementState)
        {
            var spriteMovement = EnsureComp<SpriteMovementComponent>(entity);
            spriteMovement.NoMovementLayers.Clear();
            spriteMovement.NoMovementLayers["movement"] = new PrototypeLayerData
            {
                State = paint.SpriteBodyState,
            };
            spriteMovement.MovementLayers.Clear();
            spriteMovement.MovementLayers["movement"] = new PrototypeLayerData
            {
                State = movementState,
            };
        }
        else
        {
            RemComp<SpriteMovementComponent>(entity);
        }

        // Starlight-end
    }
}
