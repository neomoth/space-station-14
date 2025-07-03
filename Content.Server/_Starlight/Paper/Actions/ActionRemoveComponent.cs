using Robust.Shared.Utility;

namespace Content.Server._Starlight.Paper.Actions;

public sealed partial class ActionRemoveComponent : OnSignAction
{
    /// <summary>
    /// list of component names to be deleted.
    /// </summary>
    [DataField]
    public List<String> Components = [];
    
    private IComponentFactory _componentFactory = default!;
    private IEntityManager _entityManager = default!;
    
    public override bool Action(EntityUid paper, ActionsOnSignComponent component, EntityUid target)
    {
        foreach (var rmComp in Components)
        {
            var targetComp = _componentFactory.GetComponent(rmComp);
            var remComp = _entityManager.GetType().GetMethod(nameof(IEntityManager.RemoveComponent));
            if (remComp == null)
            {
                DebugTools.Assert("Failed to reflect RemoveComponent from IEntityManager instance");
                continue; // Failed to reflect... this is a MAJOR bug   
            }
            var generic = remComp.MakeGenericMethod(targetComp.GetType());
            generic.Invoke(_entityManager, [target]);
        }
        
        return false;
    }

    public override void ResolveIoC()
    {
        _entityManager = IoCManager.Resolve<IEntityManager>();
        _componentFactory = IoCManager.Resolve<IComponentFactory>();
    }
}