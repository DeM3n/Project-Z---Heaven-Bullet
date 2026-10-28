using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống tự động sinh Treasure Goblin theo chu kỳ (mỗi 75 - 100 giây một lần).
/// Không cần cấu hình thủ công trong scene - Goblin tự động xuất hiện để tạo bất ngờ cho người chơi.
/// </summary>
[BurstCompile]
[UpdateAfter(typeof(SpawnSystem))]
public partial struct TreasureGoblinSpawnSystem : ISystem
{
    private float _timer;
    private EntityQuery _goblinQuery;
    private Unity.Mathematics.Random _rng;

    public void OnCreate(ref SystemState state)
    {
        _timer = 45f; // Xuất hiện lần đầu sau 45 giây chiến đấu
        _goblinQuery = SystemAPI.QueryBuilder().WithAll<TreasureGoblinTag>().Build();
        uint seed = (uint)System.DateTime.UtcNow.Ticks;
        _rng = new Unity.Mathematics.Random(seed == 0 ? 123456 : seed);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;

        // Nếu hiện đang có Goblin trên chiến trường thì hoãn đếm ngược
        if (!_goblinQuery.IsEmpty) return;

        float dt = SystemAPI.Time.DeltaTime;
        _timer -= dt;
        if (_timer > 0f) return;

        // Đặt lại thời gian cho đợt xuất hiện kế tiếp (75 đến 105 giây)
        _timer = _rng.NextFloat(75f, 105f);

        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
        float angle = _rng.NextFloat(0f, math.PI * 2f);
        float spawnDist = 11f; // Sinh cách Player 11m
        float3 spawnPos = playerPos + new float3(math.cos(angle) * spawnDist, math.sin(angle) * spawnDist, 0f);

        int playerLevel = 1;
        if (SystemAPI.HasComponent<PlayerLevelData>(playerEntity))
        {
            playerLevel = SystemAPI.GetComponent<PlayerLevelData>(playerEntity).Level;
        }

        // Máu của Goblin tỉ lệ nhẹ theo cấp của người chơi (vừa đủ để dồn damage rượt đuổi)
        float goblinHp = 70f + playerLevel * 8f;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        Entity goblin = ecb.CreateEntity();
        ecb.AddComponent(goblin, new EnemyTag());
        ecb.AddComponent(goblin, new TreasureGoblinTag());
        ecb.AddComponent(goblin, LocalTransform.FromPosition(spawnPos));
        ecb.AddComponent(goblin, new HealthData { CurrentHP = goblinHp, MaxHP = goblinHp });
        ecb.AddComponent(goblin, new MovementData { MoveSpeed = 0f, Direction = float2.zero });
        ecb.AddComponent(goblin, new TreasureGoblinData
        {
            EscapeTimer = 22f, // 22 giây để hạ gục một khi đã bị đánh thức
            FleeSpeed = 3.8f,
            ZigzagTimer = 0.3f,
            ZigzagAngle = 0f,
            HasDetectedPlayer = false,
            DetectionRange = 7.5f
        });
        ecb.AddComponent(goblin, new TreasureGoblinDropData { ExpReward = 60 });
        // Sát thương va chạm = 0 (Goblin không bao giờ cắn người chơi)
        ecb.AddComponent(goblin, new EnemyContactDamageData
        {
            Damage = 0f,
            ContactRadius = 0.6f,
            TickInterval = 1f,
            Timer = 0f
        });

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
