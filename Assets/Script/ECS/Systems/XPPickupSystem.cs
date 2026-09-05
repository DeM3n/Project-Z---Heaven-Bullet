using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateAfter(typeof(DeathCleanupSystem))]
public partial struct XPPickupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;

        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
        float range = SystemAPI.GetComponent<PickupRangeData>(playerEntity).Range;
        float rangeSq = range * range;

        var playerLevel = SystemAPI.GetComponent<PlayerLevelData>(playerEntity);
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool changed = false;

        foreach (var (transform, orb, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<ExpOrbData>>()
                     .WithAll<ExpOrbTag>()
                     .WithEntityAccess())
        {
            if (math.distancesq(playerPos, transform.ValueRO.Position) > rangeSq) continue;

            playerLevel.CurrentExp += orb.ValueRO.ExpValue;
            changed = true;
            ecb.DestroyEntity(entity);
        }

        if (changed) SystemAPI.SetComponent(playerEntity, playerLevel);

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}