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

    [Header("Cấu hình rơi ngọc EXP tự nhiên trên bản đồ (Ambient EXP Orbs)")]
    public GameObject SmallExpOrbPrefab;
    public GameObject MediumExpOrbPrefab;
    [Range(1.0f, 10.0f)] public float AmbientSpawnInterval = 3.5f;
    [Range(5, 30)] public int MaxAmbientOrbs = 15;
    public float AmbientMinRadius = 4.5f;
    public float AmbientMaxRadius = 11.5f;

    class Baker : Baker<SpawnerAuthoring>
    {
        public override void Bake(SpawnerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SpawnerData
            {
                Interval = authoring.SpawnInterval,
                Timer = 1.0f,
                SpawnRadius = authoring.SpawnRadius,
                CountPerSpawn = math.max(1, authoring.CountPerSpawn),
                Rng = new Unity.Mathematics.Random(12345),
                ElapsedGameTime = 0f
            });

            AddComponent(entity, new WaveManagerData
            {
                State = WaveState.NormalTimeline,
                ElapsedGameTime = 0f,
                IsTimerPaused = false,
                ActiveMegaWaveMinute = 0,
                MegaWaveDuration = 60f,
                MegaWaveTimer = 0f,
                MegaWaveSubSpawnTimer = 0f,
                MegaWaveSurgeStage = 0,
                MegaWaveEnemiesKilled = 0,
                MegaWaveRemainingEnemies = 0,
                MegaWaveTotalEnemies = 0,
                Triggered3Min = 0,
                Triggered6Min = 0,
                Triggered9Min = 0
            });

            Entity smallOrbEntity = authoring.SmallExpOrbPrefab != null
                ? GetEntity(authoring.SmallExpOrbPrefab, TransformUsageFlags.Dynamic)
                : Entity.Null;
            Entity medOrbEntity = authoring.MediumExpOrbPrefab != null
                ? GetEntity(authoring.MediumExpOrbPrefab, TransformUsageFlags.Dynamic)
                : Entity.Null;

            AddComponent(entity, new AmbientExpSpawnerData
            {
                SmallOrbPrefab = smallOrbEntity,
                MediumOrbPrefab = (medOrbEntity != Entity.Null) ? medOrbEntity : smallOrbEntity,
                Interval = authoring.AmbientSpawnInterval > 0f ? authoring.AmbientSpawnInterval : 3.5f,
                Timer = 1.5f,
                MaxAmbientOrbs = authoring.MaxAmbientOrbs > 0 ? authoring.MaxAmbientOrbs : 15,
                MinRadius = authoring.AmbientMinRadius > 0f ? authoring.AmbientMinRadius : 4.5f,
                MaxRadius = authoring.AmbientMaxRadius > 0f ? authoring.AmbientMaxRadius : 11.5f,
                Rng = new Unity.Mathematics.Random(98765)
            });

            var buffer = AddBuffer<EnemySpawnEntry>(entity);
            if (authoring.EnemyPool != null && authoring.EnemyPool.Count > 0)
            {
                foreach (var item in authoring.EnemyPool)
                {
                    if (item.Prefab == null) continue;
                    var enemyAuth = item.Prefab.GetComponent<EnemyAuthoring>();
                    EnemyBehaviorType bType = enemyAuth != null ? enemyAuth.Behavior : EnemyBehaviorType.Chaser;
                    buffer.Add(new EnemySpawnEntry
                    {
                        Prefab = GetEntity(item.Prefab, TransformUsageFlags.Dynamic),
                        Behavior = bType,
                        Weight = math.max(1, item.Weight)
                    });
                }
            }
        }
    }
}