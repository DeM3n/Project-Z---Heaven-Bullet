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

        foreach (var (spawner, spawnEntries) in
                 SystemAPI.Query<RefRW<SpawnerData>, DynamicBuffer<EnemySpawnEntry>>())
        {
            if (spawnEntries.Length == 0) continue;

            spawner.ValueRW.Timer -= dt;
            if (spawner.ValueRO.Timer > 0f) continue;
            spawner.ValueRW.Timer = spawner.ValueRO.Interval;

            // Tính tổng trọng số của các loại quái
            int totalWeight = 0;
            for (int i = 0; i < spawnEntries.Length; i++)
            {
                totalWeight += spawnEntries[i].Weight;
            }
            if (totalWeight <= 0) continue;

            int countToSpawn = math.max(1, spawner.ValueRO.CountPerSpawn);
            for (int c = 0; c < countToSpawn; c++)
            {
                // Chọn ngẫu nhiên loại quái theo trọng số (Weighted Random)
                int randVal = spawner.ValueRW.Rng.NextInt(0, totalWeight);
                Entity chosenPrefab = Entity.Null;
                int currentWeight = 0;

                for (int i = 0; i < spawnEntries.Length; i++)
                {
                    currentWeight += spawnEntries[i].Weight;
                    if (randVal < currentWeight)
                    {
                        chosenPrefab = spawnEntries[i].Prefab;
                        break;
                    }
                }

                if (chosenPrefab == Entity.Null)
                    chosenPrefab = spawnEntries[0].Prefab;

                // Sinh quái ngẫu nhiên trên vòng tròn bán kính SpawnRadius quanh Player
                float angle = spawner.ValueRW.Rng.NextFloat(0f, math.PI * 2f);
                float2 offset = new float2(math.cos(angle), math.sin(angle)) * spawner.ValueRO.SpawnRadius;
                float3 spawnPos = playerPos + new float3(offset.x, offset.y, 0);

                Entity newEnemy = ecb.Instantiate(chosenPrefab);
                ecb.SetComponent(newEnemy, LocalTransform.FromPosition(spawnPos));
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}