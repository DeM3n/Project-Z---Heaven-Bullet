using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct SpawnSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var spawner in SystemAPI.Query<RefRW<SpawnerData>>())
        {
            spawner.ValueRW.Timer -= dt;
            if (spawner.ValueRO.Timer > 0f) continue;
            spawner.ValueRW.Timer = spawner.ValueRO.Interval;

            float angle = spawner.ValueRW.Rng.NextFloat(0f, math.PI * 2f);
            float2 offset = new float2(math.cos(angle), math.sin(angle)) * spawner.ValueRO.SpawnRadius;
            float3 spawnPos = playerPos + new float3(offset.x, offset.y, 0);

            Entity newEnemy = ecb.Instantiate(spawner.ValueRO.EnemyPrefab);
            ecb.SetComponent(newEnemy, LocalTransform.FromPosition(spawnPos));
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}