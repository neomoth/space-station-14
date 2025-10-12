using Content.Shared._Starlight.PhysicalSocialInteraction.Components;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.PhysicalSocialInteraction.Systems;

public abstract class SharedPhysicalSocialInteractionSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    public override void Initialize()
    {
        //subscribe to inspect events on the physical social interaction receiver component
        SubscribeLocalEvent<PhysicalSocialInteractionReceiverComponent, GetVerbsEvent<Verb>>(AddPhysicalSocialInteractionVerbs);
    }

    private void AddPhysicalSocialInteractionVerbs(EntityUid uid, PhysicalSocialInteractionReceiverComponent component, GetVerbsEvent<Verb> args)
    {
        //check if the user also has a interaction giver
        if (!HasComp<PhysicalSocialInteractionGiverComponent>(args.User))
            return;

        //create a verb subcategory
        var category = new VerbCategory("Physical Social Interaction", null);

        //enumerate all the physical social interaction prototypes
        foreach (var proto in _protoMan.EnumeratePrototypes<PhysicalSocialInteractionPrototype>())
        {
            //make a verb for each one
            Verb verb = new()
            {
                Text = proto.ID,
                Category = category,
                Act = () =>
                {
                    //for now, just print to console
                    //later, this will trigger an animation and a status effect
                    //or something else entirely, idk yet
                    //maybe even a sound effect
                    //who knows
                    //the possibilities are endless
                    //just like my love for starlight
                    //which is to say, endless
                    Console.WriteLine($"You {proto.ID} {ToPrettyString(uid)}");
                }
            };
            
            args.Verbs.Add(verb);
        }
    }
}
