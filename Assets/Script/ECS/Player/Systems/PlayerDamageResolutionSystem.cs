using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống TRUNG TÂM duy nhất giải quyết toàn bộ sát thương tiếp nhận của Player (Damage Resolution Pipeline).
/// Gom toàn bộ các cú đánh từ mọi nguồn (quái cắn, đạn bắn, bom nổ, bẫy) thông qua DynamicBuffer<IncomingDamage>,
/// sau đó giải quyết tuần tự qua các lớp phòng ngự: i-Frames -> Khiên (Shield) -> Giảm thương -> Trừ HP.
/// </summary>
[UpdateAfter(typeof(PlayerContactDamageSystem))]
[UpdateAfter(typeof(EnemyProjectileHitPlayerSystem))]
[UpdateAfter(typeof(BomberExplodeSystem))]
[UpdateBefore(typeof(PlayerDeathCheckSystem))]
[UpdateBefore(typeof(ShieldExplosionSystem))]
[UpdateBefore(typeof(DeathCleanupSystem))]
[BurstCompile]
public partial struct PlayerDamageResolutionSystem : ISystem
{
    private EntityQuery _playerQuery;

    public void OnCreate(ref SystemState state)
    {
        _playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, LocalTransform>()
            .WithAllRW<HealthData, PlayerInvulnerabilityData>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (_playerQuery.IsEmpty) return;
        Entity playerEntity = _playerQuery.GetSingletonEntity();

        // Đảm bảo Player có buffer IncomingDamage
        if (!state.EntityManager.HasBuffer<IncomingDamage>(playerEntity))
        {
            state.EntityManager.AddBuffer<IncomingDamage>(playerEntity);
            return;
        }

        var damageBuffer = SystemAPI.GetBuffer<IncomingDamage>(playerEntity);
        if (damageBuffer.IsEmpty) return;

        LocalTransform playerTransform = SystemAPI.GetComponent<LocalTransform>(playerEntity);
        HealthData playerHealth = SystemAPI.GetComponent<HealthData>(playerEntity);
        PlayerInvulnerabilityData invuln = SystemAPI.GetComponent<PlayerInvulnerabilityData>(playerEntity);

        // 1. Kiểm tra trạng thái miễn sát thương (i-Frames)
        // Nếu Player đang trong thời gian bất tử, toàn bộ sát thương gửi đến trong frame này bị vô hiệu hóa
        if (invuln.CurrentTimer > 0f)
        {
            damageBuffer.Clear();
            return;
        }

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        bool hasShield = SystemAPI.HasComponent<PlayerShieldData>(playerEntity);
        PlayerShieldData shield = hasShield ? SystemAPI.GetComponent<PlayerShieldData>(playerEntity) : default;
        bool shieldModified = false;

        float totalUnblockedDamage = 0f;

        // 2. Duyệt qua từng cú đánh gửi vào buffer trong frame này
        for (int i = 0; i < damageBuffer.Length; i++)
        {
            var hit = damageBuffer[i];

            // Kiểm tra xem Khiên Hộ Thể (Aegis Shield) có sẵn sàng đỡ cú đánh này không
            if (hasShield && shield.SkillLevel > 0 && shield.CurrentCharges > 0 && shield.HitsRemainingInCurrentShield > 0)
            {
                // Khiên hấp thụ trọn vẹn cú đánh
                shield.HitsRemainingInCurrentShield--;
                shield.HasJustBlocked = true;
                shieldModified = true;

                // Nếu lớp khiên hiện tại hết hit -> vỡ khiên
                if (shield.HitsRemainingInCurrentShield <= 0)
                {
                    shield.CurrentCharges--;

                    // Kích hoạt event vỡ khiên (Chỉ nổ AOE ở Cấp 4)
                    bool isLvl4 = (shield.SkillLevel >= 4);
                    Entity breakEvent = ecb.CreateEntity();
                    ecb.AddComponent(breakEvent, new ShieldBreakEvent
                    {
                        Position = playerTransform.Position,
                        Radius = isLvl4 ? 4.5f : 0f,
                        Damage = isLvl4 ? 100f : 0f,
                        KnockbackForce = isLvl4 ? 2.5f : 0f,
                        IsExplosion = isLvl4
                    });

                    // Nếu còn lớp khiên dự phòng, mở ngay lớp khiên tiếp theo
                    if (shield.CurrentCharges > 0)
                    {
                        shield.HitsRemainingInCurrentShield = shield.HitsPerShield;
                    }
                }

                // Khi đỡ đòn thành công, kích hoạt thời gian đệm i-frames tự nhiên (0.2s)
                // để người chơi không bị ăn liên hoàn nhiều hit trong đúng 1 khung hình
                invuln.CurrentTimer = invuln.Duration;

                // Vì i-frames đã được kích hoạt, các đòn còn lại trong cùng frame này bị chặn lại
                break;
            }
            else
            {
                // Không có khiên bảo vệ: Cộng dồn sát thương chưa bị chặn
                totalUnblockedDamage += hit.Amount;
            }
        }

        // Cập nhật lại dữ liệu Khiên nếu có thay đổi
        if (shieldModified)
        {
            SystemAPI.SetComponent(playerEntity, shield);
        }

        // 3. Trừ máu thật (HealthData) nếu có sát thương lọt qua khiên
        if (totalUnblockedDamage > 0f)
        {
            playerHealth.CurrentHP -= totalUnblockedDamage;
            SystemAPI.SetComponent(playerEntity, playerHealth);

            // Kích hoạt i-frames (0.2s chớp đỏ giật lùi)
            invuln.CurrentTimer = invuln.Duration;
        }

        SystemAPI.SetComponent(playerEntity, invuln);

        // 4. Dọn sạch buffer cho frame tiếp theo
        damageBuffer.Clear();

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
