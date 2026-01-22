// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using System.Numerics;
using Content.Server.Electrocution;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Damage;
using Content.Shared.Jittering;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;

namespace Content.Server.SS220.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class GnawCableOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<EntityUid>("Target", out var target, _entManager) || _entManager.Deleted(target))
            return HTNOperatorStatus.Failed;

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (_entManager.Deleted(owner))
            return HTNOperatorStatus.Failed;

        if (_entManager.TryGetComponent<TransformComponent>(owner, out var transform))
        {
            if (transform.LocalPosition == Vector2.Zero)
                return HTNOperatorStatus.Failed;
        }

        if (_entManager.TrySystem<TurfSystem>(out var turfSys))
        {
            if (!turfSys.TryGetTileRef(_entManager.GetComponent<TransformComponent>(target).Coordinates, out var tileRef) || tileRef == null)
                return HTNOperatorStatus.Failed;
            var tileDef = turfSys.GetContentTileDefinition(tileRef.Value);
            if (!tileDef.IsSubFloor)
                return HTNOperatorStatus.Failed;
        }

        if (_entManager.TrySystem<ElectrocutionSystem>(out var electro) &&
            _entManager.TrySystem<DamageableSystem>(out var damageableSys))
        {
            const int shockDamage = 35;
            const float shockTimeSec = 4f;
            const float siemens = 1.2f;

            electro.TryDoElectrocution(
                owner,
                target,
                shockDamage,
                TimeSpan.FromSeconds(shockTimeSec),
                refresh: true,
                siemensCoefficient: siemens,
                ignoreInsulation: true
            );

            _entManager.SystemOrNull<SharedJitteringSystem>()?.DoJitter(
                owner,
                TimeSpan.FromSeconds(shockTimeSec * 0.75f),
                refresh: true,
                amplitude: 80f,
                frequency: 8f
            );

            if (_entManager.TryGetComponent<TransformComponent>(owner, out var xform))
            {
                _entManager.SpawnEntity("EffectSparks", xform.Coordinates);
            }

            _entManager.DeleteEntity(target);
        }

        if (!_entManager.TryGetComponent<HTNComponent>(owner, out var htn) || htn.Plan == null)
            return HTNOperatorStatus.Continuing;

        if (_entManager.TrySystem<MobStateSystem>(out var mobState) && mobState.IsDead(owner))
            return HTNOperatorStatus.Continuing;

        return HTNOperatorStatus.Finished;
    }
}
