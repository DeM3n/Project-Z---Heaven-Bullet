using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class SpawnerAuthoring : MonoBehaviour
{
    public GameObject EnemyPrefab;
    public float SpawnInterval = 1f;
    public float SpawnRadius = 10f;

    class Baker : Baker<SpawnerAuthoring>
    {
        public override void Bake(SpawnerAuthoring authoring)
        {
        
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SpawnerData
            {
                EnemyPrefab = GetEntity(authoring.EnemyPrefab, TransformUsageFlags.Dynamic),
                Interval = authoring.SpawnInterval,
                Timer = 0f,
                SpawnRadius = authoring.SpawnRadius,
                Rng = new Unity.Mathematics.Random(12345) // seed cố định tạm thời — GDD 11.6 sẽ thay bằng seed per-run sau
            });
        }
    }
}