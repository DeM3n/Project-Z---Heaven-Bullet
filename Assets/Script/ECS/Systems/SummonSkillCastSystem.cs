using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateBefore(typeof(ApplyMovementSystem))]
[BurstCompile]
public partial struct SummonSkillCastSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (runtime, owner, summon) in
                 SystemAPI.Query<RefRW<SkillRuntimeData>, RefRO<SkillOwnerData>, RefRO<SummonSkillData>>()
                     .WithAll<SummonSkillTag>())
        {
            runtime.ValueRW.Timer -= dt;
            if (runtime.ValueRO.Timer > 0f) continue;
            runtime.ValueRW.Timer = runtime.ValueRO.Cooldown;

            if (!state.EntityManager.Exists(owner.ValueRO.Owner)) continue;
            LocalTransform ownerTransform = state.EntityManager.GetComponentData<LocalTransform>(owner.ValueRO.Owner);

            Entity summonEntity = ecb.Instantiate(summon.ValueRO.SummonPrefab);
            ecb.SetComponent(summonEntity, LocalTransform.FromPosition(ownerTransform.Position));
            ecb.SetComponent(summonEntity, new ProjectileLifetimeData { TimeRemaining = summon.ValueRO.SummonLifetime });
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}