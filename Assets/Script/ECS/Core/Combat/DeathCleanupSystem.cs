using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateAfter(typeof(ProjectileCollisionSystem))]
public partial struct DeathCleanupSystem : ISystem
{
    private Unity.Mathematics.Random _random;

    public void OnCreate(ref SystemState state)
    {
        uint seed = (uint)System.DateTime.UtcNow.Ticks;
        if (seed == 0) seed = 12345;
        _random = new Unity.Mathematics.Random(seed);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (hp, transform, entity) in
                 SystemAPI.Query<RefRO<HealthData>, RefRO<LocalTransform>>()
                     .WithAll<EnemyTag>()
                     .WithEntityAccess())
        {
            if (hp.ValueRO.CurrentHP <= 0f)
            {
                // 1. NẾU LÀ TREASURE GOBLIN BỊ TIÊU DIỆT:
                if (SystemAPI.HasComponent<TreasureGoblinTag>(entity))
                {
                    // Chắc chắn rơi 1 Rương Cổ Vật (Treasure Chest)!
                    Entity chest = ecb.CreateEntity();
                    ecb.AddComponent(chest, LocalTransform.FromPosition(transform.ValueRO.Position));
                    ecb.AddComponent(chest, new PickupItemTag());
                    ecb.AddComponent(chest, new PickupData
                    {
                        Type = PickupType.TreasureChest,
                        Value = 0f,
                        Radius = 0.9f,
                        FlySpeed = 0f
                    });

                    // Rơi thêm 1 viên Nam Châm (Exp Magnet) ngay bên cạnh!
                    Entity magnet = ecb.CreateEntity();
                    ecb.AddComponent(magnet, LocalTransform.FromPosition(transform.ValueRO.Position + new Unity.Mathematics.float3(0.4f, 0.2f, 0f)));
                    ecb.AddComponent(magnet, new PickupItemTag());
                    ecb.AddComponent(magnet, new PickupData
                    {
                        Type = PickupType.ExpMagnet,
                        Value = 0f,
                        Radius = 0.8f,
                        FlySpeed = 0f
                    });
                }
                else
                {
                    // 2. NẾU LÀ QUÁI THƯỜNG:
                    // Rơi ngọc EXP theo tỉ lệ DropChance
                    if (SystemAPI.HasComponent<EnemyDeathDropData>(entity))
                    {
                        var drop = SystemAPI.GetComponent<EnemyDeathDropData>(entity);
                        float rollExp = _random.NextFloat(0f, 1f);
                        if (rollExp <= drop.DropChance && drop.ExpOrbPrefab != Entity.Null)
                        {
                            Entity orb = ecb.Instantiate(drop.ExpOrbPrefab);
                            ecb.SetComponent(orb, LocalTransform.FromPosition(transform.ValueRO.Position));
                            ecb.SetComponent(orb, new ExpOrbData { ExpValue = drop.ExpReward, FlySpeed = 0f });
                        }
                    }

                    // Tỉ lệ rơi Trái Tim Hồi Máu (Health Heart): 3.5%
                    float rollLoot = _random.NextFloat(0f, 1f);
                    if (rollLoot <= 0.035f)
                    {
                        Entity heart = ecb.CreateEntity();
                        ecb.AddComponent(heart, LocalTransform.FromPosition(transform.ValueRO.Position));
                        ecb.AddComponent(heart, new PickupItemTag());
                        ecb.AddComponent(heart, new PickupData
                        {
                            Type = PickupType.HealthHeart,
                            Value = 25f, // Hồi 25 HP
                            Radius = 0.8f,
                            FlySpeed = 0f
                        });
                    }
                    // Tỉ lệ rơi Nam Châm Hút EXP (Exp Magnet): 1.2%
                    else if (rollLoot <= 0.047f)
                    {
                        Entity magnet = ecb.CreateEntity();
                        ecb.AddComponent(magnet, LocalTransform.FromPosition(transform.ValueRO.Position));
                        ecb.AddComponent(magnet, new PickupItemTag());
                        ecb.AddComponent(magnet, new PickupData
                        {
                            Type = PickupType.ExpMagnet,
                            Value = 0f,
                            Radius = 0.8f,
                            FlySpeed = 0f
                        });
                    }
                }

                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}