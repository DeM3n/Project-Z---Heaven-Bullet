using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class SpawnerAuthoring : MonoBehaviour
{
    [Serializable]
    public struct EnemySpawnConfig
    {
        public string Name;
        public GameObject Prefab;
        [Range(1, 100)] public int Weight;
    }

    [Header("Danh sách các loại Enemy trong Spawner")]
    public List<EnemySpawnConfig> EnemyPool = new List<EnemySpawnConfig>();

    [Header("Cấu hình nhịp sinh quái")]
    public float SpawnInterval = 1.5f;
    public float SpawnRadius = 10f;
    public int CountPerSpawn = 2; // Số quái sinh ra mỗi nhịp

    class Baker : Baker<SpawnerAuthoring>
    {
        public override void Bake(SpawnerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SpawnerData
            {
                Interval = authoring.SpawnInterval,
                Timer = 0f,
                SpawnRadius = authoring.SpawnRadius,
                CountPerSpawn = math.max(1, authoring.CountPerSpawn),
                Rng = new Unity.Mathematics.Random(12345),
                ElapsedGameTime = 0f
            });

            var buffer = AddBuffer<EnemySpawnEntry>(entity);
            if (authoring.EnemyPool != null && authoring.EnemyPool.Count > 0)
            {
                foreach (var item in authoring.EnemyPool)
                {
                    if (item.Prefab == null) continue;
                    buffer.Add(new EnemySpawnEntry
                    {
                        Prefab = GetEntity(item.Prefab, TransformUsageFlags.Dynamic),
                        Weight = math.max(1, item.Weight)
                    });
                }
            }
        }
    }
}