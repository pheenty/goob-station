// SPDX-FileCopyrightText: 2022 metalgearsloth <metalgearsloth@gmail.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Tayrtahn <tayrtahn@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Ilya246 <57039557+Ilya246@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Ilya246 <ilyukarno@gmail.com>
// SPDX-FileCopyrightText: 2025 J <billsmith116@gmail.com>
// SPDX-FileCopyrightText: 2025 SX-7 <sn1.test.preria.2002@gmail.com>
//
// SPDX-License-Identifier: MIT

using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Moves an NPC to the specified target key. Hands the actual steering off to NPCSystem.Steering
/// </summary>
public sealed partial class MoveToOperator : HTNOperator, IHtnConditionalShutdown
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private NPCSteeringSystem _steering = default!;
    private PathfindingSystem _pathfind = default!;
    private SharedTransformSystem _transform = default!;

    /// <summary>
    /// When to shut the task down.
    /// </summary>
    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    /// <summary>
    /// Should we assume the MovementTarget is reachable during planning or should we pathfind to it?
    /// </summary>
    [DataField("pathfindInPlanning")]
    public bool PathfindInPlanning = true;

    /// <summary>
    /// When we're finished moving to the target should we remove its key?
    /// </summary>
    [DataField("removeKeyOnFinish")]
    public bool RemoveKeyOnFinish = true;

    /// <summary>
    /// Target Coordinates to move to. This gets removed after execution.
    /// </summary>
    [DataField("targetKey")]
    public string TargetKey = "TargetCoordinates";

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField("pathfindKey")]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    /// <summary>
    /// How close we need to get before considering movement finished.
    /// </summary>
    [DataField("rangeKey")]
    public string RangeKey = "MovementRange";

    /// <summary>
    /// Do we only need to move into line of sight.
    /// </summary>
    [DataField("stopOnLineOfSight")]
    public bool StopOnLineOfSight;

    // Goobstation - if you see this in a merge conflict: should be safe to just revert our version, however it has changes to some goob HTNs to make them not brake (via setting this datafield to null), you might want to reinstate those
    /// <summary>
    /// Velocity below which we count as successfully braked.
    /// Don't try to brake if null (upstream behavior).
    /// </summary>
    [DataField]
    public float? BrakeMaxVelocity = 0.03f;

    // Goobstation
    /// <summary>
    /// If either we or the target are offgrid, gets assigned to make us just move directly to target.
    /// </summary>
    [DataField]
    public string DirectMoveTargetKey = "DirectMoveTarget";

    private const string MovementCancelToken = "MovementCancelToken";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _pathfind = sysManager.GetEntitySystem<PathfindingSystem>();
        _steering = sysManager.GetEntitySystem<NPCSteeringSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        if (!blackboard.TryGetValue<EntityCoordinates>(TargetKey, out var targetCoordinates, _entManager))
        {
            return (false, null);
        }

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<TransformComponent>(owner, out var xform) ||
            !_entManager.TryGetComponent<PhysicsComponent>(owner, out var body))
            return (false, null);

        // Goobstation - check if we or target are offgrid or on different grids
        var doDirectMove = !_entManager.TryGetComponent<MapGridComponent>(xform.GridUid, out var ownerGrid) ||
                      !_entManager.TryGetComponent<MapGridComponent>(_transform.GetGrid(targetCoordinates), out var targetGrid) ||
                      ownerGrid != targetGrid;

        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);

        if (xform.Coordinates.TryDistance(_entManager, targetCoordinates, out var distance) && distance <= range)
        {
            // In range
            return (true, new Dictionary<string, object>()
            {
                {NPCBlackboard.OwnerCoordinates, blackboard.GetValueOrDefault<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, _entManager)}
            });
        }

        if (!PathfindInPlanning)
        {
            return (true, new Dictionary<string, object>()
            {
                {NPCBlackboard.OwnerCoordinates, targetCoordinates}
            });
        }

        if (!doDirectMove) // Goobstation
        {
            var path = await _pathfind.GetPath(
                blackboard.GetValue<EntityUid>(NPCBlackboard.Owner),
                xform.Coordinates,
                    targetCoordinates,
                range,
                cancelToken,
                _pathfind.GetFlags(blackboard));

            if (path.Result != PathResult.Path)
            {
                return (false, null);
            }

            return (true, new Dictionary<string, object>()
            {
                {NPCBlackboard.OwnerCoordinates, targetCoordinates},
                {PathfindKey, path}
            });
        }
        // Goobstation - else try move directly to target
        else
        {
            return (true, new Dictionary<string, object>()
            {
                {NPCBlackboard.OwnerCoordinates, targetCoordinates},
                {DirectMoveTargetKey, true}
            });
        }
    }

    // Given steering is complicated we'll hand it off to a dedicated system rather than this singleton operator.

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        // Need to remove the planning value for execution.
        blackboard.Remove<EntityCoordinates>(NPCBlackboard.OwnerCoordinates);
        var targetCoordinates = blackboard.GetValue<EntityCoordinates>(TargetKey);
        var uid = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        // Re-use the path we may have if applicable.
        var comp = _steering.Register(uid, targetCoordinates);
        comp.ArriveOnLineOfSight = StopOnLineOfSight;

        if (blackboard.TryGetValue<float>(RangeKey, out var range, _entManager))
        {
            comp.Range = range;
        }

        // Goobstation - see if we want to just move directly first
        if (blackboard.TryGetValue<bool>(DirectMoveTargetKey, out var doDirectMove, _entManager) && doDirectMove)
        {
            comp.Coordinates = targetCoordinates;
            comp.DirectMove = true;
        }
        else if (blackboard.TryGetValue<PathResultEvent>(PathfindKey, out var result, _entManager))
        {
            // Goobstation
            comp.DirectMove = false;

            if (blackboard.TryGetValue<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, out var coordinates, _entManager))
            {
                var mapCoords = _transform.ToMapCoordinates(coordinates);
                _steering.PrunePath(uid, mapCoords, _transform.ToMapCoordinates(targetCoordinates).Position - mapCoords.Position, result.Path);
            }

            comp.CurrentPath = new Queue<PathPoly>(result.Path);
        }
        comp.InRangeMaxSpeed = BrakeMaxVelocity; // Goobstation
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<NPCSteeringComponent>(owner, out var steering))
            return HTNOperatorStatus.Failed;

        // Just keep moving in the background and let the other tasks handle it.
        if (ShutdownState == HTNPlanState.PlanFinished && steering.Status == SteeringStatus.Moving)
        {
            return HTNOperatorStatus.Finished;
        }

        return steering.Status switch
        {
            SteeringStatus.InRange => HTNOperatorStatus.Finished,
            SteeringStatus.NoPath => HTNOperatorStatus.Failed,
            SteeringStatus.Moving => HTNOperatorStatus.Continuing,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public void ConditionalShutdown(NPCBlackboard blackboard)
    {
        // Cleanup the blackboard and remove steering.
        if (blackboard.TryGetValue<CancellationTokenSource>(MovementCancelToken, out var cancelToken, _entManager))
        {
            cancelToken.Cancel();
            blackboard.Remove<CancellationTokenSource>(MovementCancelToken);
        }

        // OwnerCoordinates is only used in planning so dump it.
        blackboard.Remove<PathResultEvent>(PathfindKey);
        // Goobstation - also clear direct move
        blackboard.Remove<bool>(DirectMoveTargetKey);

        if (RemoveKeyOnFinish)
        {
            blackboard.Remove<EntityCoordinates>(TargetKey);
        }

        _steering.Unregister(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner));
    }
}
