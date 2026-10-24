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

        int playerLevel = 1;
        if (SystemAPI.HasComponent<PlayerLevelData>(playerEntity))
        {
            playerLevel = SystemAPI.GetComponent<PlayerLevelData>(playerEntity).Level;
        }

        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (spawner, spawnEntries) in
                 SystemAPI.Query<RefRW<SpawnerData>, DynamicBuffer<EnemySpawnEntry>>())
        {
            if (spawnEntries.Length == 0) continue;

            // 1. Tăng tiến thời gian trận đấu (Elapsed Time)
            spawner.ValueRW.ElapsedGameTime += dt;

            spawner.ValueRW.Timer -= dt;
            if (spawner.ValueRO.Timer > 0f) continue;
            spawner.ValueRW.Timer = spawner.ValueRO.Interval;

            // Tính hệ số tăng tiến máu của quái (Enemy HP Scale):
            // - Mỗi 1 phút trôi qua: Máu quái tăng +30%
            // - Mỗi cấp độ của Player (từ Lv2 trở đi): Quái tăng thêm +5% máu để thích ứng sức mạnh
            float timeMinutes = spawner.ValueRO.ElapsedGameTime / 60f;
            float timeMultiplier = 1.0f + timeMinutes * 0.30f;
            float levelMultiplier = 1.0f + math.max(0, playerLevel - 1) * 0.05f;
            float hpMultiplier = timeMultiplier * levelMultiplier;

            // Tính tổng trọng số của các loại quái
            int totalWeight = 0;
            for (int i = 0; i < spawnEntries.Length; i++)
            {
                totalWeight += spawnEntries[i].Weight;
            }
            if (totalWeight <= 0) continue;

            // Số quái sinh mỗi đợt tăng nhẹ theo thời gian (cứ 1.5 phút tăng thêm 1 quái, tối đa 8)
            int extraSpawns = (int)(timeMinutes / 1.5f);
            int countToSpawn = math.clamp(spawner.ValueRO.CountPerSpawn + extraSpawns, 1, 8);

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

                // Scale máu của quái vật mới sinh theo nhịp độ thời gian trận đấu
                if (state.EntityManager.HasComponent<HealthData>(chosenPrefab))
                {
                    var baseHealth = state.EntityManager.GetComponentData<HealthData>(chosenPrefab);
                    float scaledHp = math.round(baseHealth.MaxHP * hpMultiplier);
                    ecb.SetComponent(newEnemy, new HealthData
                    {
                        CurrentHP = scaledHp,
                        MaxHP = scaledHp
                    });
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}