using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Administration;
using Content.Shared.NodeContainer;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server.Atmos.Commands;

/// <summary>
/// Console command for debugging and managing docked pipe connections.
/// Provides subcommands for inspecting, testing, and manipulating dock pipe links.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class DockPipeCommand : IConsoleCommand
{
    public string Command => "dockpipe";
    public string Description => "Debug dock pipe system connections";
    public string Help => @"
dockpipe info - Show all current dock connections
dockpipe pipe <entityId> - Show debug info for a specific pipe
dockpipe tile <gridId> <x> <y> - Show debug info for entities on a tile
dockpipe test <pipeA> <pipeB> - Test connection between two pipes
dockpipe scan - Scan all docked airlocks for potential connections
dockpipe refresh - Refresh all dock connections
dockpipe connect <pipeA> <pipeB> - Force connect two pipes
dockpipe cleanup - Clean up invalid connections
dockpipe check <entityId> - Check dock connections for a specific entity
";

    /// <summary>
    /// Executes the dockpipe command with the provided arguments.
    /// Handles subcommands for info, pipe, tile, test, scan, refresh, connect, cleanup, and check.
    /// </summary>
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        var entityManager = IoCManager.Resolve<IEntityManager>();
        var dockPipeSystem = entityManager.System<DockPipeSystem>();

        switch (args[0].ToLower())
        {
            case "info":
                shell.WriteLine(dockPipeSystem.GetDebugInfo());
                break;

            case "pipe":
                if (args.Length < 2)
                {
                    shell.WriteLine("Usage: dockpipe pipe <entityId>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var netEnt) || !entityManager.TryGetEntity(netEnt, out var entityId))
                {
                    shell.WriteLine("Invalid entity ID");
                    return;
                }
                shell.WriteLine(dockPipeSystem.GetPipeDebugInfo(entityId.Value));
                break;

            case "tile":
                if (args.Length < 4)
                {
                    shell.WriteLine("Usage: dockpipe tile <gridId> <x> <y>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var gridNet) || !entityManager.TryGetEntity(gridNet, out var gridId))
                {
                    shell.WriteLine("Invalid grid ID");
                    return;
                }
                if (!int.TryParse(args[2], out var x) || !int.TryParse(args[3], out var y))
                {
                    shell.WriteLine("Invalid coordinates");
                    return;
                }
                shell.WriteLine(dockPipeSystem.GetTileDebugInfo(gridId.Value, new Vector2i(x, y)));
                break;

            case "test":
                if (args.Length < 3)
                {
                    shell.WriteLine("Usage: dockpipe test <pipeA> <pipeB>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var pipeANet) || !entityManager.TryGetEntity(pipeANet, out var pipeA))
                {
                    shell.WriteLine("Invalid pipe A entity ID");
                    return;
                }
                if (!NetEntity.TryParse(args[2], out var pipeBNet) || !entityManager.TryGetEntity(pipeBNet, out var pipeB))
                {
                    shell.WriteLine("Invalid pipe B entity ID");
                    return;
                }
                shell.WriteLine(dockPipeSystem.TestPipeConnection(pipeA.Value, pipeB.Value));
                break;

            case "scan":
                shell.WriteLine(dockPipeSystem.ScanAllDockedAirlocks());
                break;

            case "refresh":
                dockPipeSystem.RefreshAllDockConnections();
                shell.WriteLine("Refreshed all dock connections");
                shell.WriteLine(dockPipeSystem.GetDebugInfo());
                break;

            case "connect":
                if (args.Length < 3)
                {
                    shell.WriteLine("Usage: dockpipe connect <pipeA> <pipeB>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var connectANet) || !entityManager.TryGetEntity(connectANet, out var connectA))
                {
                    shell.WriteLine("Invalid pipe A entity ID");
                    return;
                }
                if (!NetEntity.TryParse(args[2], out var connectBNet) || !entityManager.TryGetEntity(connectBNet, out var connectB))
                {
                    shell.WriteLine("Invalid pipe B entity ID");
                    return;
                }
                
                // Force a manual connection
                if (entityManager.TryGetComponent<NodeContainerComponent>(connectA, out var nodeA) &&
                    entityManager.TryGetComponent<NodeContainerComponent>(connectB, out var nodeB))
                {
                    PipeNode? pipeNodeA = null, pipeNodeB = null;
                    
                    foreach (var node in nodeA.Nodes.Values)
                        if (node is PipeNode pipe) { pipeNodeA = pipe; break; }
                    
                    foreach (var node in nodeB.Nodes.Values)
                        if (node is PipeNode pipe) { pipeNodeB = pipe; break; }
                    
                    if (pipeNodeA != null && pipeNodeB != null)
                    {
                        pipeNodeA.AddAlwaysReachable(pipeNodeB);
                        pipeNodeB.AddAlwaysReachable(pipeNodeA);
                        shell.WriteLine($"Manually connected {connectA} and {connectB}");
                    }
                    else
                    {
                        shell.WriteLine("One or both entities are not pipes");
                    }
                }
                else
                {
                    shell.WriteLine("Entities do not have NodeContainerComponent");
                }
                break;

            case "cleanup":
                // Force cleanup of all connections
                dockPipeSystem.RefreshAllDockConnections();
                shell.WriteLine("Cleaned up all dock connections");
                break;

            case "check":
                if (args.Length < 2)
                {
                    shell.WriteLine("Usage: dockpipe check <entityId>");
                    return;
                }
                if (!NetEntity.TryParse(args[1], out var netEntity) || 
                    !entityManager.TryGetEntity(netEntity, out var entity))
                {
                    shell.WriteLine("Invalid entity ID.");
                    return;
                }

                if (!entityManager.TryGetComponent<NodeContainerComponent>(entity.Value, out var nodeContainer))
                {
                    shell.WriteLine("Entity doesn't have NodeContainerComponent.");
                    return;
                }

                foreach (var node in nodeContainer.Nodes.Values)
                {
                    if (node is PipeNode pipeNode)
                    {
                        dockPipeSystem.CheckForDockConnections(entity.Value, pipeNode);
                        shell.WriteLine($"Checked dock connections for pipe node: {node.GetType().Name}");
                        return;
                    }
                }

                shell.WriteLine("Entity doesn't contain any pipe nodes.");
                break;

            default:
                shell.WriteLine($"Unknown subcommand: {args[0]}");
                shell.WriteLine(Help);
                break;
        }
    }
}