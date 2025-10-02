// ReSharper disable InvalidXmlDocComment <-- hatred.

// TODO: fix the second command variant not working, fix chat prefixes not working

using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Content.Shared.Speech;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Administration.Commands;

/// <summary>
/// Grant/Revoke the ability to speak in IC chat (local, radio, whisper, etc) to the specified entity (or yourself if no entity is specified)
/// <br/>
/// <b>Syntax:</b><br/>- ghostic &lt;grant/revoke&gt; [entity ID] [verb id] [sound id]<br/>- ghostic &lt;grant/revoke&gt; [verb id] [sound id]
/// <param name="grant/revoke"> whether to grant or revoke speaking capability</param>
/// <param name="entity ID">the ID of the entity <i>(optional)</i></param>
/// <param name="verb ID">the SpeechVerb prototype ID to use <i>(optional)</i></param>
/// <param name="sound ID">the SpeechSounds prototype ID to use <i>(optional)</i></param>
/// </summary>
[AdminCommand(AdminFlags.VarEdit)]
public sealed class GhostIC : LocalizedEntityCommands
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _protoMgr = default!;
    
    public override string Command => "ghostic";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        EntityUid? target;
        GhostComponent? ghost;
        ProtoId<SpeechVerbPrototype> verbProtoId = "Default";
        ProtoId<SpeechSoundsPrototype>? soundProtoId = null;
        var idx = 0;
        if (args.Length < 1)
        {
            shell.WriteError("Invalid number of arguments.");
            return;
        }

        if (args.Length < 2)
        {
            if (shell.Player is not { } player)
            {
                shell.WriteError(Loc.GetString("You need to be a player to use this on yourself."));
                return;
            }
            if (player.AttachedEntity is not { Valid: true } entity || !_entityManager.TryGetComponent<GhostComponent>(entity, out var comp))
            {
                shell.WriteError(Loc.GetString("You must be a ghost to use this on yourself."));
                return;
            }
            target = player.AttachedEntity;
            ghost = comp;
        }
        else
        {
            if (int.TryParse(args[1], out var id))
            {
                target = new EntityUid(id);
                if (target is not { Valid: true } ||
                    !_entityManager.TryGetComponent<GhostComponent>(target, out var comp))
                {
                    shell.WriteError("Target either doesn't exist or is not a ghost.");
                    return;
                }

                ghost = comp;
            }
            else
            {
                if (shell.Player is not { } player)
                {
                    shell.WriteError(Loc.GetString("You need to be a player to use this on yourself."));
                    return;
                }

                if (player.AttachedEntity is not { Valid: true } entity ||
                    !_entityManager.TryGetComponent<GhostComponent>(entity, out var comp))
                {
                    shell.WriteError(Loc.GetString("You must be a ghost to use this on yourself."));
                    return;
                }

                target = player.AttachedEntity;
                ghost = comp;
                idx -= 1;
            }
        }
        var verb = args.ElementAtOrDefault(2 - idx);
        if(verb is not null)
        {
            var protoId = new ProtoId<SpeechVerbPrototype>(verb);
            if (_protoMgr.HasIndex(protoId)) verbProtoId = protoId;
            else shell.WriteError("ProtoID not found, defaulting to \"Default\"");
            shell.WriteLine($"val: {verbProtoId}");
        }

        var sound = args.ElementAtOrDefault(3 - idx);
        if (sound is not null)
        {
            var protoId = new ProtoId<SpeechSoundsPrototype>(sound);
            if (_protoMgr.HasIndex(protoId)) soundProtoId = protoId;
            else shell.WriteError("ProtoID not found, defaulting to null");
            shell.WriteLine($"val: {soundProtoId}");
        }

        var access = args[0].ToLower();
        switch (access)
        {
            case "grant" or "true" or "1":
                Grant(target.Value, ghost, verbProtoId, soundProtoId);
                break;
            case "revoke" or "false" or "0":
                Revoke(target.Value, ghost);
                break;
        }
    }

    private void Grant(EntityUid entity, GhostComponent ghost, ProtoId<SpeechVerbPrototype> verb, ProtoId<SpeechSoundsPrototype>? sound)
    {
        ghost.LocalChatEnabled = true;
        var speech = _entityManager.EnsureComponent<SpeechComponent>(entity);
        speech.SpeechVerb = verb;
        speech.SpeechSounds = sound;
    }

    private void Revoke(EntityUid entity, GhostComponent ghost) => ghost.LocalChatEnabled = false;
}