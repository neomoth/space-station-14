using System.Linq;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Components;
using Content.Shared.NodeContainer;
using Content.Shared.Atmos;
using Robust.Shared.Map.Components;
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

        private readonly HashSet<EntityUid> _dockConnectionsChecked = new();

        #endregion

        #region Initialization

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DockEvent>(OnDocked);
            SubscribeLocalEvent<UndockEvent>(OnUndocked);
        }

        #endregion

        #region Docking Logic

        private void OnDocked(DockEvent ev)
        {
            var dockA = ev.DockA.Owner;
            var dockB = ev.DockB.Owner;

            var pipesA = GetTilePipes(dockA);
            var pipesB = GetTilePipes(dockB);

            // Connect pipes by matching CurrentPipeLayer
            foreach (var pipeA in pipesA)
            {
                foreach (var pipeB in pipesB)
                {
                    if (CanConnect(pipeA, pipeB) && pipeA.CurrentPipeLayer == pipeB.CurrentPipeLayer)
                    {
                        pipeA.AddAlwaysReachable(pipeB);
                        pipeB.AddAlwaysReachable(pipeA);
                    }
                }
            }

            foreach (var dock in new[] { dockA, dockB })
            {
                foreach (var pipe in GetTilePipes(dock))
                    CheckForDockConnections(pipe.Owner, pipe);
            }
        }

        private void OnUndocked(UndockEvent ev)
        {
            var dockA = ev.DockA.Owner;
            var dockB = ev.DockB.Owner;

            var pipesA = GetTilePipes(dockA);
            var pipesB = GetTilePipes(dockB);

            // Disconnect pipes by matching CurrentPipeLayer
            foreach (var pipeA in pipesA)
            {
                foreach (var pipeB in pipesB)
                {
                    if (pipeA.CurrentPipeLayer == pipeB.CurrentPipeLayer)
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
            gridRotation = 0;
            if (!TryComp<TransformComponent>(dock, out var xform) || xform.GridUid == null)
                return new();

            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                return new();

            gridRotation = GetGridRotation(xform.GridUid.Value);

            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            var result = new List<(PipeNode, int)>();
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile))
            {
                if (ent == dock)
                    continue;
                if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                    continue;
                foreach (var node in nodeContainer.Nodes.Values)
                {
                    if (node is PipeNode pipe)
                        result.Add((pipe, CalculateVisualLayer(pipe.CurrentPipeLayer, gridRotation)));
                }
            }
            return result;
        }

        // Returns grid rotation in 90-degree increments (0, 1, 2, 3)
        private int GetGridRotation(EntityUid gridUid)
        {
            var gridXform = EntityManager.GetComponent<TransformComponent>(gridUid);
            var angle = gridXform.LocalRotation.Theta;
            return ((int)Math.Round(angle / (Math.PI / 2)) % 4 + 4) % 4;
        }

        // Calculates the visual layer index for a pipe after grid rotation
        private int CalculateVisualLayer(AtmosPipeLayer pipeLayer, int gridRotation)
        {
            return ((int)pipeLayer + gridRotation) % 3;
        }

        public List<PipeNode> GetTilePipes(EntityUid dock)
        {
            if (!TryComp<TransformComponent>(dock, out var xform) || xform.GridUid == null)
                return new();

            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                return new();

            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            var result = new List<PipeNode>();
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile))
            {
                if (ent == dock)
                    continue;
                if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
                    continue;
                foreach (var node in nodeContainer.Nodes.Values)
                {
                    if (node is PipeNode pipe)
                        result.Add(pipe);
                }
            }
            return result;
        }

        public bool CanConnect(PipeNode a, PipeNode b)
        {
            return a.NodeGroupID == b.NodeGroupID && !a.Deleting && !b.Deleting;
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
                    CheckForDockConnections(pipeEntity, pipe);
            }
        }

        #endregion

        #region Debug Commands

        public string GetDebugInfo()
        {
            var lines = new List<string> { "Docked pipe connections:" };
            foreach (var (dockA, dockB) in GetAllDockedPairs())
            {
                var pipesA = GetTilePipes(dockA);
                var pipesB = GetTilePipes(dockB);
                int count = 0;
                foreach (var pipeA in pipesA)
                foreach (var pipeB in pipesB)
                {
                    var reachable = pipeA.GetAlwaysReachable();
                    if (CanConnect(pipeA, pipeB) && reachable != null && reachable.Contains(pipeB))
                        count++;
                }
                lines.Add($"Dock {dockA} <-> {dockB}: {count} connections");
            }
            return string.Join('\n', lines);
        }

        public string GetPipeDebugInfo(EntityUid entity)
        {
            if (!EntityManager.TryGetComponent<NodeContainerComponent>(entity, out var nodeContainer))
                return "No NodeContainerComponent.";
            foreach (var node in nodeContainer.Nodes.Values)
            {
                if (node is PipeNode pipe)
                {
                    var reachable = pipe.GetAlwaysReachable();
                    if (reachable == null || reachable.Count == 0)
                        return $"Pipe {entity} has no dock connections.";
                    var targets = string.Join(", ", reachable.Select(x => x.Owner.ToString()));
                    return $"Pipe {entity} dock connections: {targets}";
                }
            }
            return "No PipeNode found.";
        }

        public string GetTileDebugInfo(EntityUid gridId, Vector2i tile)
        {
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
            if (!EntityManager.TryGetComponent<NodeContainerComponent>(pipeAUid, out var nodeA) ||
                !EntityManager.TryGetComponent<NodeContainerComponent>(pipeBUid, out var nodeB))
                return "One or both entities are not pipes.";
            PipeNode? pipeA = nodeA.Nodes.Values.OfType<PipeNode>().FirstOrDefault();
            PipeNode? pipeB = nodeB.Nodes.Values.OfType<PipeNode>().FirstOrDefault();
            if (pipeA == null || pipeB == null)
                return "One or both entities are not pipes.";
            var reachable = pipeA.GetAlwaysReachable();
            if (reachable != null && reachable.Contains(pipeB))
                return $"Pipes {pipeAUid} and {pipeBUid} are dock-connected.";
            return $"Pipes {pipeAUid} and {pipeBUid} are not dock-connected.";
        }

        public string ScanAllDockedAirlocks()
        {
            var lines = new List<string>();
            foreach (var (dockA, dockB) in GetAllDockedPairs())
                lines.Add($"Dock pair: {dockA} <-> {dockB}");
            return lines.Count == 0 ? "No docked airlocks." : string.Join('\n', lines);
        }

        public void RefreshAllDockConnections()
        {
            ClearDockConnectionsChecked();
            foreach (var (dockA, dockB) in GetAllDockedPairs())
            {
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
            if (_dockConnectionsChecked.Contains(pipeEntity))
                return;
            _dockConnectionsChecked.Add(pipeEntity);

            if (!EntityManager.TryGetComponent<TransformComponent>(pipeEntity, out var xform) || xform.GridUid == null)
                return;
            if (!EntityManager.TryGetComponent<MapGridComponent>(xform.GridUid.Value, out var grid))
                return;
            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile))
            {
                if (ent == pipeEntity)
                    continue;
                if (!EntityManager.TryGetComponent<DockingComponent>(ent, out var docking))
                    continue;
                if (docking.DockedWith is not { } otherDock)
                    continue;
                var pipesOther = GetTilePipes(otherDock);
                foreach (var pipeB in pipesOther)
                {
                    if (CanConnect(pipeNode, pipeB) && pipeNode.CurrentPipeLayer == pipeB.CurrentPipeLayer)
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

        #endregion

        #region Helper Methods

        // Call this to force a refresh (e.g. in RefreshAllDockConnections)
        private void ClearDockConnectionsChecked()
        {
            _dockConnectionsChecked.Clear();
        }

        // Helper to enumerate all docked pairs
        public IEnumerable<(EntityUid, EntityUid)> GetAllDockedPairs()
        {
            foreach (var dockEntity in EntityManager.EntityQuery<DockingComponent>())
            {
                var dockA = dockEntity.Owner;
                if (dockEntity.DockedWith is not { } dockB)
                    continue;
                if (dockA.CompareTo(dockB) < 0)
                    yield return (dockA, dockB);
            }
        }

        #endregion
    }
}