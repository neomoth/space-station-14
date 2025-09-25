using System.Linq;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Components; // For DockingComponent
using Content.Shared.NodeContainer;
using Content.Shared.Atmos;
using Robust.Shared.Map.Components; // For MapGridComponent
using Robust.Shared.GameObjects;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Content.Shared.Atmos.Components;
using Robust.Shared.Utility;
using Robust.Shared.Log;

namespace Content.Server.Atmos.EntitySystems
{
    public sealed class DockPipeSystem : EntitySystem
    {
        #region Dependencies

        [Dependency] public readonly SharedMapSystem _mapSystem = default!;
        private readonly ISawmill _sawmill = Logger.GetSawmill("dockpipe");

        #endregion

        #region Initialization

        public override void Initialize()
        {
            base.Initialize();
            _sawmill.Debug("Initializing DockPipeSystem");
            SubscribeLocalEvent<DockEvent>(OnDocked);
            SubscribeLocalEvent<UndockEvent>(OnUndocked);
        }

        #endregion

        #region Docking Logic

        private void OnDocked(DockEvent ev)
        {
            _sawmill.Debug($"Dock event: {ev.DockA.Owner} <-> {ev.DockB.Owner}");
            var dockA = ev.DockA.Owner;
            var dockB = ev.DockB.Owner;

            var pipesA = GetTilePipesWithRotation(dockA, out var rotationA);
            var pipesB = GetTilePipesWithRotation(dockB, out var rotationB);

            _sawmill.Debug($"Found {pipesA.Count} pipes on dockA tile, {pipesB.Count} pipes on dockB tile");
            foreach (var (pipeA, visualLayerA) in pipesA)
            {
                foreach (var (pipeB, visualLayerB) in pipesB)
                {
                    if (CanConnect(pipeA, pipeB) && visualLayerA == visualLayerB)
                    {
                        pipeA.AddAlwaysReachable(pipeB);
                        pipeB.AddAlwaysReachable(pipeA);
                    }
                }
            }

            // Ensure future pipes get dock connections when anchored
            foreach (var dock in new[] { dockA, dockB })
            {
                var pipes = GetTilePipes(dock);
                foreach (var pipe in pipes)
                {
                    // This will ensure CheckForDockConnections is called on anchor
                    // (if not already handled by OnAnchorStateChanged)
                    CheckForDockConnections(pipe.Owner, pipe);
                }
            }
        }

        private void OnUndocked(UndockEvent ev)
        {
            _sawmill.Debug($"Undock event: {ev.DockA.Owner} <-> {ev.DockB.Owner}");
            var dockA = ev.DockA.Owner;
            var dockB = ev.DockB.Owner;

            var pipesA = GetTilePipesWithRotation(dockA, out var rotationA);
            var pipesB = GetTilePipesWithRotation(dockB, out var rotationB);

            _sawmill.Debug($"Found {pipesA.Count} pipes on dockA tile, {pipesB.Count} pipes on dockB tile");
            foreach (var (pipeA, visualLayerA) in pipesA)
            {
                foreach (var (pipeB, visualLayerB) in pipesB)
                {
                    // Manifold-specific code commented out; use normal pipe connection logic for all pipes.
                    /*
                    if (pipeA.IsManifold() && pipeB.IsManifold())
                    {
                        foreach (var connectorA in pipeA.GetManifoldConnectors())
                        foreach (var connectorB in pipeB.GetManifoldConnectors())
                        {
                            if (visualLayerA == visualLayerB)
                            {
                                connectorA.RemoveAlwaysReachable(connectorB);
                                connectorB.RemoveAlwaysReachable(connectorA);
                            }
                        }
                    }
                    else */
                    if (visualLayerA == visualLayerB)
                    {
                        pipeA.RemoveAlwaysReachable(pipeB);
                        pipeB.RemoveAlwaysReachable(pipeA);
                    }
                }
            }
        }

        #endregion

        #region Pipe Query Helpers

        // Returns (PipeNode, visualLayer) for all pipes on the dock's tile, and outputs grid rotation
        private List<(PipeNode pipe, int visualLayer)> GetTilePipesWithRotation(EntityUid dock, out int gridRotation)
        {
            _sawmill.Debug($"GetTilePipesWithRotation for dock {dock}");
            var result = new List<(PipeNode, int)>();
            gridRotation = 0;
            if (!TryComp<TransformComponent>(dock, out var xform) || xform.GridUid == null)
            {
                _sawmill.Debug($"Dock {dock} has no grid");
                return result;
            }
            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            {
                _sawmill.Debug($"Dock {dock} grid {xform.GridUid.Value} has no MapGridComponent");
                return result;
            }

            gridRotation = GetGridRotation(xform.GridUid.Value);

            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile))
            {
                if (ent == dock)
                    continue;
                if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                    continue;
                foreach (var node in nodeContainer.Nodes.Values)
                {
                    if (node is PipeNode pipe)
                    {
                        var visualLayer = CalculateVisualLayer(pipe.CurrentPipeLayer, gridRotation);
                        _sawmill.Debug($"Found pipe {pipe.Owner} on dock tile, visualLayer={visualLayer}");
                        result.Add((pipe, visualLayer));
                    }
                }
            }
            return result;
        }

        // Returns grid rotation in 90-degree increments (0, 1, 2, 3)
        private int GetGridRotation(EntityUid gridUid)
        {
            var gridXform = EntityManager.GetComponent<TransformComponent>(gridUid);
            var angle = gridXform.LocalRotation.Theta;
            var rot = ((int)Math.Round(angle / (Math.PI / 2)) % 4 + 4) % 4;
            _sawmill.Debug($"Grid {gridUid} rotation: {rot} (angle {angle})");
            return rot;
        }

        // Calculates the visual layer index for a pipe after grid rotation
        private int CalculateVisualLayer(AtmosPipeLayer pipeLayer, int gridRotation)
        {
            var baseLayer = (int)pipeLayer;
            var visualLayer = (baseLayer + gridRotation) % 3;
            _sawmill.Debug($"CalculateVisualLayer: baseLayer={baseLayer}, gridRotation={gridRotation}, visualLayer={visualLayer}");
            return visualLayer;
        }

        public List<PipeNode> GetTilePipes(EntityUid dock)
        {
            _sawmill.Debug($"GetTilePipes for dock {dock}");
            var result = new List<PipeNode>();
            if (!TryComp<TransformComponent>(dock, out var xform) || xform.GridUid == null)
            {
                _sawmill.Debug($"Dock {dock} has no grid");
                return result;
            }
            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            {
                _sawmill.Debug($"Dock {dock} grid {xform.GridUid.Value} has no MapGridComponent");
                return result;
            }

            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile))
            {
                if (ent == dock)
                    continue;
                if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                    continue;
                foreach (var node in nodeContainer.Nodes.Values)
                {
                    if (node is PipeNode pipe)
                    {
                        _sawmill.Debug($"Found pipe {pipe.Owner} on dock tile");
                        result.Add(pipe);
                    }
                }
            }
            return result;
        }

        public bool CanConnect(PipeNode a, PipeNode b)
        {
            var canConnect = a.NodeGroupID == b.NodeGroupID && !a.Deleting && !b.Deleting;
            _sawmill.Debug($"CanConnect {a.Owner} <-> {b.Owner}: {canConnect}");
            return canConnect;
        }

        #endregion

        #region Anchor Handling

        /// <summary>
        /// Call this after anchoring a pipe entity to ensure dock connections are made if it's on a dock tile.
        /// This avoids duplicate AnchorStateChangedEvent subscriptions.
        /// </summary>
        public void TryConnectDockedPipe(EntityUid pipeEntity)
        {
            if (!EntityManager.TryGetComponent<NodeContainerComponent>(pipeEntity, out var nodeContainer))
                return;

            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is PipeNode pipe)
                {
                    CheckForDockConnections(pipeEntity, pipe);
                }
            }
        }

        #endregion

        #region Debug Commands

        public string GetDebugInfo()
        {
            _sawmill.Debug("GetDebugInfo called");
            var lines = new List<string>();
            lines.Add("Docked pipe connections:");
            // Scan all docked airlocks and show connections
            foreach (var (dockA, dockB) in GetAllDockedPairs())
            {
                // Only check pipes on the dock tiles, not the entire grid
                var pipesA = GetTilePipes(dockA);
                var pipesB = GetTilePipes(dockB);
                int count = 0;
                if (pipesA != null && pipesB != null)
                {
                    foreach (var pipeA in pipesA)
                    foreach (var pipeB in pipesB)
                    {
                        var reachable = pipeA.GetAlwaysReachable();
                        if (CanConnect(pipeA, pipeB) && reachable != null && reachable.Contains(pipeB))
                            count++;
                    }
                }
                lines.Add($"Dock {dockA} <-> {dockB}: {count} connections");
            }
            return string.Join('\n', lines);
        }

        public string GetPipeDebugInfo(EntityUid entity)
        {
            _sawmill.Debug($"GetPipeDebugInfo for {entity}");
            if (!EntityManager.TryGetComponent<NodeContainerComponent>(entity, out var nodeContainer))
                return "No NodeContainerComponent.";
            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is PipeNode pipe)
                {
                    var reachable = pipe.GetAlwaysReachable();
                    if (reachable == null || reachable.Count == 0)
                        return $"Pipe {entity} has no dock connections.";
                    var targets = reachable != null
                        ? string.Join(", ", reachable.Select(x => x.Owner.ToString()))
                        : "";
                    return $"Pipe {entity} dock connections: {targets}";
                }
            }
            return "No PipeNode found.";
        }

        public string GetTileDebugInfo(EntityUid gridId, Vector2i tile)
        {
            _sawmill.Debug($"GetTileDebugInfo for grid {gridId} tile {tile}");
            if (!EntityManager.TryGetComponent<MapGridComponent>(gridId, out var grid))
                return $"Grid {gridId} not found.";
            var ents = _mapSystem.GetAnchoredEntities(gridId, grid, tile).ToList();
            if (ents.Count == 0)
                return $"No anchored entities at {gridId} {tile}.";
            var lines = new List<string> { $"Entities at {gridId} {tile}:" };
            foreach (var ent in ents)
                lines.Add($"  {ent}");
            return string.Join('\n', lines);
        }

        public string TestPipeConnection(EntityUid pipeAUid, EntityUid pipeBUid)
        {
            _sawmill.Debug($"TestPipeConnection {pipeAUid} <-> {pipeBUid}");
            if (!EntityManager.TryGetComponent<NodeContainerComponent>(pipeAUid, out var nodeA) ||
                !EntityManager.TryGetComponent<NodeContainerComponent>(pipeBUid, out var nodeB))
                return "One or both entities are not pipes.";
            PipeNode? pipeA = null;
            PipeNode? pipeB = null;
            foreach (var node in nodeA.Nodes.Values)
                if (node is PipeNode p) { pipeA = p; break; }
            foreach (var node in nodeB.Nodes.Values)
                if (node is PipeNode p) { pipeB = p; break; }
            if (pipeA == null || pipeB == null)
                return "One or both entities are not pipes.";
            var reachable = pipeA.GetAlwaysReachable();
            if (reachable != null && reachable.Contains(pipeB))
                return $"Pipes {pipeAUid} and {pipeBUid} are dock-connected.";
            return $"Pipes {pipeAUid} and {pipeBUid} are not dock-connected.";
        }

        public string ScanAllDockedAirlocks()
        {
            _sawmill.Debug("ScanAllDockedAirlocks called");
            var lines = new List<string>();
            foreach (var (dockA, dockB) in GetAllDockedPairs())
                lines.Add($"Dock pair: {dockA} <-> {dockB}");
            return lines.Count == 0 ? "No docked airlocks." : string.Join('\n', lines);
        }

        public void RefreshAllDockConnections()
        {
            _sawmill.Debug("RefreshAllDockConnections called");
            // Remove all dock connections from all pipes, then re-connect all docked pairs
            foreach (var (dockA, dockB) in GetAllDockedPairs())
            {
                // Only check pipes on the dock tiles, not the entire grid
                var pipesA = GetTilePipes(dockA);
                var pipesB = GetTilePipes(dockB);
                foreach (var pipeA in pipesA)
                foreach (var pipeB in pipesB)
                {
                    var reachableA = pipeA.GetAlwaysReachable();
                    var reachableB = pipeB.GetAlwaysReachable();
                    if (reachableA != null && reachableA.Contains(pipeB))
                        pipeA.RemoveAlwaysReachable(pipeB);
                    if (reachableB != null && reachableB.Contains(pipeA))
                        pipeB.RemoveAlwaysReachable(pipeA);
                }
                // Reconnect
                foreach (var pipeA in pipesA)
                foreach (var pipeB in pipesB)
                    if (CanConnect(pipeA, pipeB))
                    {
                        pipeA.AddAlwaysReachable(pipeB);
                        pipeB.AddAlwaysReachable(pipeA);
                    }
            }
        }

        public void CheckForDockConnections(EntityUid pipeEntity, PipeNode pipeNode)
        {
            _sawmill.Debug($"CheckForDockConnections for {pipeEntity}");
            if (!EntityManager.TryGetComponent<TransformComponent>(pipeEntity, out var xform) || xform.GridUid == null)
                return;
            if (!EntityManager.TryGetComponent<MapGridComponent>(xform.GridUid.Value, out var grid))
                return;
            var gridRotation = GetGridRotation(xform.GridUid.Value);
            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile))
            {
                if (ent == pipeEntity)
                    continue;
                if (!EntityManager.TryGetComponent<DockingComponent>(ent, out var docking))
                    continue;
                if (docking.DockedWith is not { } otherDock)
                    continue;
                _sawmill.Debug($"Pipe {pipeEntity} is on dock tile with docked dock {otherDock}");
                var pipesOther = GetTilePipesWithRotation(otherDock, out var otherRotation);
                var visualLayerA = CalculateVisualLayer(pipeNode.CurrentPipeLayer, gridRotation);
                foreach (var (pipeB, visualLayerB) in pipesOther)
                {
                    if (CanConnect(pipeNode, pipeB) && visualLayerA == visualLayerB)
                    {
                        var reachableA = pipeNode.GetAlwaysReachable();
                        var reachableB = pipeB.GetAlwaysReachable();
                        if (reachableA == null || !reachableA.Contains(pipeB))
                            pipeNode.AddAlwaysReachable(pipeB);
                        if (reachableB == null || !reachableB.Contains(pipeNode))
                            pipeB.AddAlwaysReachable(pipeNode);
                    }
                }
            }
        }

        // Helper to enumerate all docked pairs
        public IEnumerable<(EntityUid, EntityUid)> GetAllDockedPairs()
        {
            _sawmill.Debug("GetAllDockedPairs called");
            foreach (var dockEntity in EntityManager.EntityQuery<DockingComponent>())
            {
                var dockA = dockEntity.Owner;
                var dockingA = dockEntity;
                if (dockingA.DockedWith is not { } dockB)
                    continue;
                // Only yield each pair once
                if (dockA.CompareTo(dockB) < 0)
                    yield return (dockA, dockB);
            }
        }

        #endregion
    }
}