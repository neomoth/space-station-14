using Content.Server.NodeContainer.Nodes;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Components;
using Content.Shared.NodeContainer;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Robust.Shared.Map.Components;
using System.Linq;
using Robust.Shared.GameObjects;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
/// System for managing automatic gas pipe connections between docked grids.
/// When two ships dock, pipes on the same tiles as the docking airlocks are automatically connected.
/// Handles connection, disconnection, and cleanup of cross-grid pipe links.
/// </summary>
public sealed class DockPipeSystem : EntitySystem
{
    #region Dependencies

    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    #endregion

    #region Fields

    /// <summary>
    /// Stores all current docked pipe connections, keyed by the pair of docked airlocks.
    /// Each entry contains a list of connected pipe node pairs.
    /// </summary>
    private readonly Dictionary<(EntityUid, EntityUid), List<(PipeNode, PipeNode)>> _dockConnections = new();

    #endregion

    #region Initialization

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DockEvent>(OnGridDocked);
        SubscribeLocalEvent<UndockEvent>(OnGridUndocked);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles entity termination to clean up any dock connections referencing pipe nodes on the deleted entity.
    /// </summary>
    private void OnEntityTerminating(EntityTerminatingEvent args)
    {
        if (TryComp<NodeContainerComponent>(args.Entity, out var nodeContainer))
        {
            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is PipeNode)
                {
                    CleanupConnectionsForEntity(args.Entity);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Removes any pipe connections that reference the specified entity.
    /// Cleans up the dock connection dictionary accordingly.
    /// </summary>
    private void CleanupConnectionsForEntity(EntityUid deletedEntity)
    {
        var connectionsToRemove = new List<(EntityUid, EntityUid)>();
        var connectionsToUpdate = new List<(EntityUid, EntityUid, List<(PipeNode, PipeNode)>)>();

        foreach (var ((dockA, dockB), connections) in _dockConnections)
        {
            var updatedConnections = new List<(PipeNode, PipeNode)>();
            var needsUpdate = false;

            foreach (var (pipeA, pipeB) in connections)
            {
                // Check if either pipe belongs to the deleted entity
                if (pipeA.Owner == deletedEntity || pipeB.Owner == deletedEntity)
                {
                    needsUpdate = true;
                    continue; // Skip this connection
                }

                // Also check if the entity still exists
                if (!EntityManager.EntityExists(pipeA.Owner) || !EntityManager.EntityExists(pipeB.Owner))
                {
                    needsUpdate = true;
                    continue; // Skip this connection
                }

                updatedConnections.Add((pipeA, pipeB));
            }

            if (needsUpdate)
            {
                if (updatedConnections.Count == 0)
                {
                    connectionsToRemove.Add((dockA, dockB));
                }
                else
                {
                    connectionsToUpdate.Add((dockA, dockB, updatedConnections));
                }
            }
        }

        // Apply the updates
        foreach (var (dockA, dockB) in connectionsToRemove)
        {
            _dockConnections.Remove((dockA, dockB));
        }

        foreach (var (dockA, dockB, updatedConnections) in connectionsToUpdate)
        {
            _dockConnections[(dockA, dockB)] = updatedConnections;
        }
    }

    /// <summary>
    /// Handles the docking event between two airlocks, connecting pipes across all layers.
    /// </summary>
    private void OnGridDocked(DockEvent ev)
    {
        // Get the EntityUids from the DockingComponent objects
        var dockA = ev.DockA.Owner;
        var dockB = ev.DockB.Owner;
        
        // Verify these airlocks are actually docked to each other
        if (ev.DockA.DockedWith != dockB || ev.DockB.DockedWith != dockA)
            return;

        // Get the docking direction between the two specific airlocks
        var dockDirection = GetDockingDirection(dockA, dockB);
        if (dockDirection == null)
            return;

        // Connect pipes across all layers
        var connectionPairs = ConnectAllLayers(dockA, dockB, dockDirection.Value);

        // Store the connections for later disconnection, keyed by the specific dock pair
        if (connectionPairs.Count > 0)
        {
            _dockConnections[(dockA, dockB)] = connectionPairs;
        }
    }

    /// <summary>
    /// Handles the undocking event, disconnecting all pipes between the two previously docked airlocks.
    /// </summary>
    private void OnGridUndocked(UndockEvent ev)
    {
        // Get the EntityUids from the DockingComponent objects
        var dockA = ev.DockA.Owner;
        var dockB = ev.DockB.Owner;
        
        // Find and disconnect pipes for the specific dock pair
        var key = (dockA, dockB);
        var reverseKey = (dockB, dockA);

        if (_dockConnections.TryGetValue(key, out var connections))
        {
            DisconnectPipes(connections);
            _dockConnections.Remove(key);
        }

        if (_dockConnections.TryGetValue(reverseKey, out var reverseConnections))
        {
            DisconnectPipes(reverseConnections);
            _dockConnections.Remove(reverseKey);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Checks for possible dock connections for a newly placed or constructed pipe.
    /// Should be called by other systems when a pipe is created or anchored.
    /// </summary>
    public void CheckForDockConnections(EntityUid pipeEntity, PipeNode pipeNode)
    {
        var pipeXform = Transform(pipeEntity);
        if (!pipeXform.Anchored || pipeXform.GridUid == null)
            return;

        if (!TryComp<MapGridComponent>(pipeXform.GridUid.Value, out var grid))
            return;

        var pipeTilePos = _mapSystem.TileIndicesFor(pipeXform.GridUid.Value, grid, pipeXform.Coordinates);

        // Only check dock connections where this pipe is placed on the exact same tile as a dock
        foreach (var ((dockA, dockB), connections) in _dockConnections)
        {
            EntityUid? thisDock = null;
            EntityUid? otherDock = null;

            // Check if this pipe is on the same tile as dockA
            var dockAXform = Transform(dockA);
            if (dockAXform.GridUid == pipeXform.GridUid)
            {
                var dockATilePos = _mapSystem.TileIndicesFor(dockAXform.GridUid!.Value, grid, dockAXform.Coordinates);
                if (pipeTilePos == dockATilePos)
                {
                    thisDock = dockA;
                    otherDock = dockB;
                }
            }

            // Check if this pipe is on the same tile as dockB (if not already found)
            if (thisDock == null)
            {
                var dockBXform = Transform(dockB);
                if (dockBXform.GridUid == pipeXform.GridUid)
                {
                    var dockBTilePos = _mapSystem.TileIndicesFor(dockBXform.GridUid!.Value, grid, dockBXform.Coordinates);
                    if (pipeTilePos == dockBTilePos)
                    {
                        thisDock = dockB;
                        otherDock = dockA;
                    }
                }
            }

            // If the pipe isn't on a dock tile, skip this dock pair
            if (thisDock == null || otherDock == null)
                continue;

            // Get the docking direction and check for compatible pipes on the other side
            var dockDirection = GetDockingDirection(thisDock.Value, otherDock.Value);
            if (dockDirection == null)
                continue;

            var otherSidePipes = FindCompatiblePipesOnTile(otherDock.Value, dockDirection.Value.GetOpposite());

            // Try to connect this new pipe to pipes on the other side
            foreach (var otherPipe in otherSidePipes)
            {
                if (CanPipesConnect(pipeNode, otherPipe))
                {
                    ConnectPipes(pipeNode, otherPipe);
                    
                    // Add to the existing connections list for this dock pair
                    _dockConnections[(dockA, dockB)].Add((pipeNode, otherPipe));
                }
            }
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Determines the cardinal direction from dockA to dockB.
    /// Returns null if either entity does not exist.
    /// </summary>
    private Direction? GetDockingDirection(EntityUid dockA, EntityUid dockB)
    {
        // Validate entities exist before accessing their transforms
        if (!EntityManager.EntityExists(dockA) || !EntityManager.EntityExists(dockB))
            return null;

        var xformA = Transform(dockA);
        var xformB = Transform(dockB);

        // Get world positions using the transform system
        var positionA = _transformSystem.GetWorldPosition(xformA);
        var positionB = _transformSystem.GetWorldPosition(xformB);
        var directionVector = positionB - positionA;
        
        // Convert to cardinal direction with better tolerance
        var absX = Math.Abs(directionVector.X);
        var absY = Math.Abs(directionVector.Y);
        
        if (absX > absY)
        {
            return directionVector.X > 0 ? Direction.East : Direction.West;
        }
        else
        {
            return directionVector.Y > 0 ? Direction.North : Direction.South;
        }
    }

    /// <summary>
    /// Finds all compatible pipe nodes on the specified tile and direction.
    /// </summary>
    private List<PipeNode> FindCompatiblePipesOnTile(EntityUid dockEntity, Direction dockDirection)
    {
        var pipes = new List<PipeNode>();
        
        var dockXform = Transform(dockEntity);
        if (!dockXform.Anchored || dockXform.GridUid == null)
            return pipes;

        if (!TryComp<MapGridComponent>(dockXform.GridUid.Value, out var grid))
            return pipes;

        var tilePos = _mapSystem.TileIndicesFor(dockXform.GridUid.Value, grid, dockXform.Coordinates);
        var requiredPipeDirection = dockDirection.ToPipeDirection();

        // Find all entities on this tile
        foreach (var entity in _mapSystem.GetAnchoredEntities(dockXform.GridUid.Value, grid, tilePos))
        {
            if (entity == dockEntity) // Skip the dock itself
                continue;

            if (!TryComp<NodeContainerComponent>(entity, out var nodeContainer))
                continue;

            // Collect pipe nodes that can connect in the docking direction
            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is PipeNode pipeNode && 
                    CanPipeConnectInDirection(pipeNode, requiredPipeDirection))
                {
                    pipes.Add(pipeNode);
                }
            }
        }

        return pipes;
    }

    /// <summary>
    /// Checks if a pipe node can connect in the specified direction.
    /// </summary>
    private bool CanPipeConnectInDirection(PipeNode pipe, PipeDirection direction)
    {
        // Check if the pipe can connect in the specified direction
        return pipe.CurrentPipeDirection.HasDirection(direction);
    }

    /// <summary>
    /// Connects pipes across all layers between two docked airlocks, considering both actual and visual alignment.
    /// </summary>
    private List<(PipeNode, PipeNode)> ConnectAllLayers(EntityUid dockA, EntityUid dockB, Direction dockDirection)
    {
        var connectionPairs = new List<(PipeNode, PipeNode)>();
        
        // Get all pipes on both sides with their visual positions
        var pipesA = GetPipesWithVisualLayers(dockA, dockDirection);
        var pipesB = GetPipesWithVisualLayers(dockB, dockDirection.GetOpposite());

        // First, try exact layer matching
        foreach (var (pipeA, layerA) in pipesA)
        {
            foreach (var (pipeB, layerB) in pipesB)
            {
                // Connect if they're on the same actual layer and compatible
                if (pipeA.CurrentPipeLayer == pipeB.CurrentPipeLayer && CanPipesConnect(pipeA, pipeB))
                {
                    ConnectPipes(pipeA, pipeB);
                    connectionPairs.Add((pipeA, pipeB));
                }
            }
        }

        // If no exact matches, try visual layer matching
        if (connectionPairs.Count == 0)
        {
            foreach (var (pipeA, visualLayerA) in pipesA)
            {
                foreach (var (pipeB, visualLayerB) in pipesB)
                {
                    // Connect if they're on the same visual layer and compatible
                    if (visualLayerA == visualLayerB && CanPipesConnect(pipeA, pipeB) &&
                        !connectionPairs.Any(pair => pair.Item1 == pipeA || pair.Item2 == pipeB))
                    {
                        ConnectPipes(pipeA, pipeB);
                        connectionPairs.Add((pipeA, pipeB));
                    }
                }
            }
        }
        
        return connectionPairs;
    }

    /// <summary>
    /// Gets all pipe nodes on a dock tile, along with their visual layer index after grid rotation.
    /// </summary>
    private List<(PipeNode pipe, int visualLayer)> GetPipesWithVisualLayers(EntityUid dockEntity, Direction dockDirection)
    {
        var result = new List<(PipeNode pipe, int visualLayer)>();
        
        var dockXform = Transform(dockEntity);
        if (!dockXform.Anchored || dockXform.GridUid == null)
            return result;

        if (!TryComp<MapGridComponent>(dockXform.GridUid.Value, out var grid))
            return result;

        var tilePos = _mapSystem.TileIndicesFor(dockXform.GridUid.Value, grid, dockXform.Coordinates);
        var requiredPipeDirection = dockDirection.ToPipeDirection();

        // Get the grid's rotation to calculate visual layer mapping
        var gridRotation = GetGridRotation(dockXform.GridUid.Value);

        // Find all entities on this tile
        foreach (var entity in _mapSystem.GetAnchoredEntities(dockXform.GridUid.Value, grid, tilePos))
        {
            if (entity == dockEntity) // Skip the dock itself
                continue;

            if (!TryComp<NodeContainerComponent>(entity, out var nodeContainer))
                continue;

            // Collect pipe nodes that can connect in the docking direction
            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is PipeNode pipeNode && 
                    CanPipeConnectInDirection(pipeNode, requiredPipeDirection))
                {
                    // Calculate the visual layer this pipe appears on when viewed from the dock direction
                    var visualLayer = CalculateVisualLayer(pipeNode.CurrentPipeLayer, gridRotation);
                    result.Add((pipeNode, visualLayer));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the grid's rotation in 90-degree increments.
    /// </summary>
    private int GetGridRotation(EntityUid gridUid)
    {
        var gridXform = Transform(gridUid);
        var angle = gridXform.LocalRotation.Theta;
        
        // Convert to 90-degree steps (0, 1, 2, 3 for 0°, 90°, 180°, 270°)
        return (int)Math.Round(angle / (Math.PI / 2)) % 4;
    }

    /// <summary>
    /// Calculates the visual layer index for a pipe after grid rotation.
    /// </summary>
    private int CalculateVisualLayer(AtmosPipeLayer pipeLayer, int gridRotation)
    {
        var baseLayer = (int)pipeLayer;
        
        // For each 90-degree rotation, the visual layers shift
        // This creates a mapping where pipes that would visually overlap connect to each other
        return (baseLayer + gridRotation) % 3; // 3 total layers (Primary=0, Secondary=1, Tertiary=2)
    }

    /// <summary>
    /// Checks if two pipes can be connected for cross-grid connections.
    /// Ignores layer differences; only checks node group and deletion status.
    /// </summary>
    private bool CanPipesConnect(PipeNode pipeA, PipeNode pipeB)
    {
        // For cross-grid connections, we only check node group and deletion status
        // Layer compatibility is handled by the visual layer calculation
        return pipeA.NodeGroupID == pipeB.NodeGroupID && 
               !pipeA.Deleting && !pipeB.Deleting;
    }

    /// <summary>
    /// Finds all pipe nodes on a specific layer at the dock location.
    /// </summary>
    private List<PipeNode> FindPipesOnLayer(EntityUid dockEntity, Direction dockDirection, AtmosPipeLayer layer)
    {
        var pipes = new List<PipeNode>();
        
        var dockXform = Transform(dockEntity);
        if (!dockXform.Anchored || dockXform.GridUid == null)
            return pipes;

        if (!TryComp<MapGridComponent>(dockXform.GridUid.Value, out var grid))
            return pipes;

        var tilePos = _mapSystem.TileIndicesFor(dockXform.GridUid.Value, grid, dockXform.Coordinates);
        var requiredPipeDirection = dockDirection.ToPipeDirection();

        // Find all entities on this tile
        foreach (var entity in _mapSystem.GetAnchoredEntities(dockXform.GridUid.Value, grid, tilePos))
        {
            if (entity == dockEntity) // Skip the dock itself
                continue;

            if (!TryComp<NodeContainerComponent>(entity, out var nodeContainer))
                continue;

            // Collect pipe nodes that can connect in the docking direction and are on the specified layer
            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is PipeNode pipeNode && 
                    pipeNode.CurrentPipeLayer == layer &&
                    CanPipeConnectInDirection(pipeNode, requiredPipeDirection))
                {
                    pipes.Add(pipeNode);
                }
            }
        }

        return pipes;
    }

    /// <summary>
    /// Connects two pipe nodes, adding a bidirectional always-reachable link and forcing network reconstruction.
    /// </summary>
    private void ConnectPipes(PipeNode pipeA, PipeNode pipeB)
    {
        // Validate that both entities still exist
        if (!EntityManager.EntityExists(pipeA.Owner) || !EntityManager.EntityExists(pipeB.Owner))
            return;

        // Add bidirectional always-reachable connection
        pipeA.AddAlwaysReachable(pipeB);
        pipeB.AddAlwaysReachable(pipeA);
        
        // Force network reconstruction to ensure gas can flow
        // This is critical for gas flow across grids
        if (pipeA.NodeGroup != null)
        {
            var nodeGroupSystem = EntityManager.System<NodeGroupSystem>();
            nodeGroupSystem.QueueRemakeGroup((BaseNodeGroup)pipeA.NodeGroup);
        }
        
        if (pipeB.NodeGroup != null && pipeB.NodeGroup != pipeA.NodeGroup)
        {
            var nodeGroupSystem = EntityManager.System<NodeGroupSystem>();
            nodeGroupSystem.QueueRemakeGroup((BaseNodeGroup)pipeB.NodeGroup);
        }
    }

    /// <summary>
    /// Disconnects all pipe node pairs in the provided list and queues network reconstruction.
    /// </summary>
    private void DisconnectPipes(List<(PipeNode, PipeNode)> connections)
    {
        var nodeGroupSystem = EntityManager.System<NodeGroupSystem>();
        var groupsToRemake = new HashSet<BaseNodeGroup>();
        
        foreach (var (pipeA, pipeB) in connections)
        {
            // Validate that both entities still exist before trying to disconnect
            if (!EntityManager.EntityExists(pipeA.Owner) || !EntityManager.EntityExists(pipeB.Owner))
                continue;

            if (!pipeA.Deleting && !pipeB.Deleting)
            {
                pipeA.RemoveAlwaysReachable(pipeB);
                pipeB.RemoveAlwaysReachable(pipeA);
                
                // Queue network reconstruction for both groups
                if (pipeA.NodeGroup != null)
                    groupsToRemake.Add((BaseNodeGroup)pipeA.NodeGroup);
                if (pipeB.NodeGroup != null)
                    groupsToRemake.Add((BaseNodeGroup)pipeB.NodeGroup);
            }
        }
        
        // Remake all affected node groups
        foreach (var group in groupsToRemake)
        {
            nodeGroupSystem.QueueRemakeGroup(group);
        }
    }

    /// <summary>
    /// Manually refresh all dock connections. Useful for debugging or after system restarts.
    /// </summary>
    public void RefreshAllDockConnections()
    {
        var allDockPairs = new List<(EntityUid, EntityUid)>(_dockConnections.Keys);
        
        foreach (var (dockA, dockB) in allDockPairs)
        {
            // Validate that dock entities still exist
            if (!EntityManager.EntityExists(dockA) || !EntityManager.EntityExists(dockB))
            {
                _dockConnections.Remove((dockA, dockB));
                continue;
            }

            // Disconnect existing connections
            if (_dockConnections.TryGetValue((dockA, dockB), out var connections))
            {
                DisconnectPipes(connections);
                _dockConnections.Remove((dockA, dockB));
            }

            // Check if still docked and reconnect
            if (TryComp<DockingComponent>(dockA, out var dockingA) && 
                TryComp<DockingComponent>(dockB, out var dockingB) &&
                dockingA.DockedWith == dockB && dockingB.DockedWith == dockA)
            {
                // Recreate the connection
                var dockDirection = GetDockingDirection(dockA, dockB);
                
                if (dockDirection != null)
                {
                    var connectionPairs = ConnectAllLayers(dockA, dockB, dockDirection.Value);

                    if (connectionPairs.Count > 0)
                    {
                        _dockConnections[(dockA, dockB)] = connectionPairs;
                    }
                }
            }
        }
    }

    #endregion
}

/// <summary>
/// Extension methods for converting cardinal directions to pipe directions.
/// </summary>
public static class DirectionExtensions
{
    /// <summary>
    /// Converts a cardinal direction to the corresponding pipe direction.
    /// </summary>
    public static PipeDirection ToPipeDirection(this Direction direction)
    {
        return direction switch
        {
            Direction.North => PipeDirection.North,
            Direction.South => PipeDirection.South,
            Direction.East => PipeDirection.East,
            Direction.West => PipeDirection.West,
            _ => PipeDirection.None
        };
    }
}