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

            // Use HashSet for anchored pipes for faster lookup
            var anchoredA = new HashSet<EntityUid>(pipesA.Select(p => p.Owner));
            var anchoredB = new HashSet<EntityUid>(pipesB.Select(p => p.Owner));

            foreach (var pipeA in pipesA)
            {
                if (!anchoredA.Contains(pipeA.Owner) || !EntityManager.GetComponent<TransformComponent>(pipeA.Owner).Anchored) continue;
                foreach (var pipeB in pipesB)
                {
                    if (!anchoredB.Contains(pipeB.Owner) || !EntityManager.GetComponent<TransformComponent>(pipeB.Owner).Anchored) continue;
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
                {
                    if (!EntityManager.GetComponent<TransformComponent>(pipe.Owner).Anchored) continue;
                    CheckForDockConnections(pipe.Owner, pipe);
                }
            }
        }

        private void OnUndocked(UndockEvent ev)
        {
            var dockA = ev.DockA.Owner;
            var dockB = ev.DockB.Owner;

            var pipesA = GetTilePipes(dockA);
            var pipesB = GetTilePipes(dockB);

            var anchoredA = new HashSet<EntityUid>(pipesA.Select(p => p.Owner));
            var anchoredB = new HashSet<EntityUid>(pipesB.Select(p => p.Owner));

            foreach (var pipeA in pipesA)
            {
                if (!anchoredA.Contains(pipeA.Owner) || !EntityManager.GetComponent<TransformComponent>(pipeA.Owner).Anchored) continue;
                foreach (var pipeB in pipesB)
                {
                    if (!anchoredB.Contains(pipeB.Owner) || !EntityManager.GetComponent<TransformComponent>(pipeB.Owner).Anchored) continue;
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
            // Only return pipes that are anchored and not deleted
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
                if (!TryComp<TransformComponent>(ent, out var entXform) || !entXform.Anchored)
                    continue;
                // Use OfType for direct filtering
                result.AddRange(nodeContainer.Nodes.Values.OfType<PipeNode>().Where(pipe => !pipe.Deleting));
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

            var nodesCopy = nodeContainer.Nodes.Values.OfType<PipeNode>().ToList();

            if (!EntityManager.TryGetComponent<TransformComponent>(pipeEntity, out var xform) || xform.GridUid == null || !xform.Anchored)
                return;
            if (!EntityManager.TryGetComponent<MapGridComponent>(xform.GridUid.Value, out var grid))
                return;
            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

            var anchoredEntities = _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile).ToList();
            var dockedEntities = anchoredEntities.Where(ent =>
                ent != pipeEntity &&
                EntityManager.TryGetComponent<DockingComponent>(ent, out var docking) &&
                docking.DockedWith is not null).ToList();

            foreach (var pipe in nodesCopy)
            {
                foreach (var ent in dockedEntities)
                {
                    var docking = EntityManager.GetComponent<DockingComponent>(ent);
                    var otherDock = docking.DockedWith!.Value;
                    var pipesOther = GetTilePipes(otherDock).Where(p => EntityManager.GetComponent<TransformComponent>(p.Owner).Anchored).ToList();
                    foreach (var pipeB in pipesOther)
                    {
                        if (CanConnect(pipe, pipeB) && pipe.CurrentPipeLayer == pipeB.CurrentPipeLayer)
                        {
                            var reachableA = pipe.GetAlwaysReachable();
                            var reachableB = pipeB.GetAlwaysReachable();
                            if (reachableA == null || !reachableA.Contains(pipeB))
                                pipe.AddAlwaysReachable(pipeB);
                            if (reachableB == null || !reachableB.Contains(pipe))
                                pipeB.AddAlwaysReachable(pipe);
                        }
                    }
                }
            }
        }

        #endregion

        #region Dock Checking

        public void CheckForDockConnections(EntityUid pipeEntity, PipeNode pipeNode)
        {
            if (!_dockConnectionsChecked.Add(pipeEntity))
                return;

            if (!EntityManager.TryGetComponent<TransformComponent>(pipeEntity, out var xform) || xform.GridUid == null || !xform.Anchored)
                return;
            if (!EntityManager.TryGetComponent<MapGridComponent>(xform.GridUid.Value, out var grid))
                return;
            var tile = _mapSystem.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

            var anchoredEntities = _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tile).ToList();
            var dockedEntities = anchoredEntities.Where(ent =>
                ent != pipeEntity &&
                EntityManager.TryGetComponent<DockingComponent>(ent, out var docking) &&
                docking.DockedWith is not null).ToList();

            foreach (var ent in dockedEntities)
            {
                var docking = EntityManager.GetComponent<DockingComponent>(ent);
                var otherDock = docking.DockedWith!.Value;
                var pipesOther = GetTilePipes(otherDock).Where(p => EntityManager.GetComponent<TransformComponent>(p.Owner).Anchored);
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

        #endregion

        #region Connection Refresh

        public void RefreshAllDockConnections()
        {
            ClearDockConnectionsChecked();
            foreach (var (dockA, dockB) in GetAllDockedPairs())
            {
                var pipesA = GetTilePipes(dockA);
                var pipesB = GetTilePipes(dockB);

                var anchoredA = new HashSet<EntityUid>(pipesA.Select(p => p.Owner));
                var anchoredB = new HashSet<EntityUid>(pipesB.Select(p => p.Owner));

                foreach (var pipeA in pipesA)
                foreach (var pipeB in pipesB)
                {
                    if (!anchoredA.Contains(pipeA.Owner) || !anchoredB.Contains(pipeB.Owner) ||
                        !EntityManager.GetComponent<TransformComponent>(pipeA.Owner).Anchored ||
                        !EntityManager.GetComponent<TransformComponent>(pipeB.Owner).Anchored) continue;
                    var reachableA = pipeA.GetAlwaysReachable();
                    var reachableB = pipeB.GetAlwaysReachable();
                    if (reachableA != null && reachableA.Contains(pipeB))
                        pipeA.RemoveAlwaysReachable(pipeB);
                    if (reachableB != null && reachableB.Contains(pipeA))
                        pipeB.RemoveAlwaysReachable(pipeA);
                }
                foreach (var pipeA in pipesA)
                foreach (var pipeB in pipesB)
                    if (anchoredA.Contains(pipeA.Owner) && anchoredB.Contains(pipeB.Owner) &&
                        EntityManager.GetComponent<TransformComponent>(pipeA.Owner).Anchored &&
                        EntityManager.GetComponent<TransformComponent>(pipeB.Owner).Anchored &&
                        CanConnect(pipeA, pipeB))
                    {
                        pipeA.AddAlwaysReachable(pipeB);
                        pipeB.AddAlwaysReachable(pipeA);
                    }
            }
        }

        private void ClearDockConnectionsChecked()
        {
            _dockConnectionsChecked.Clear();
        }

        #endregion

        #region Helper Methods

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