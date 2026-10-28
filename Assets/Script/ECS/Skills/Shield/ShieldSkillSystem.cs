using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống quản lý hồi chiêu, số lượng lớp khiên và đồng bộ trạng thái Khiên Hộ Thể (Shield Skill).
/// Tự động khởi tạo Skill Entity nếu chưa có, hỗ trợ kiểm soát cấp độ (0 - 4).
/// </summary>
[UpdateBefore(typeof(PlayerContactDamageSystem))]
public partial struct ShieldSkillSystem : ISystem
{
    private EntityQuery _playerQuery;
    private EntityQuery _skillQuery;

    public void OnCreate(ref SystemState state)
    {
        _playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, LocalTransform>()
            .Build();

        _skillQuery = SystemAPI.QueryBuilder()
            .WithAll<ShieldSkillTag, SkillRuntimeData, ShieldSkillData>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (_playerQuery.IsEmpty) return;
        Entity playerEntity = _playerQuery.GetSingletonEntity();

        // 1. Tự động Bootstrap Skill Entity nếu chưa có trong Scene
        if (_skillQuery.IsEmpty)
        {
            Entity skillEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<ShieldSkillTag>(skillEntity);
            state.EntityManager.AddComponentData(skillEntity, new SkillOwnerData { Owner = playerEntity });
            state.EntityManager.AddComponentData(skillEntity, new SkillRuntimeData
            {
                Cooldown = 30f,
                Timer = 0f,
                Level = 0, // Mặc định Level 0 (chưa học), nâng cấp qua Level-up hoặc Dev Menu
                SkillId = new FixedString32Bytes("shield")
            });
            state.EntityManager.AddComponentData(skillEntity, new ShieldSkillData
            {
                BaseCooldown = 30f,
                Lv2Cooldown = 18f,
                Lv3Cooldown = 16f,
                Lv4Cooldown = 14f,
                ExplosionRadius = 4.5f,
                ExplosionDamage = 100f,
                KnockbackForce = 2.5f
            });
            return;
        }

        // Đảm bảo Player entity có component PlayerShieldData
        if (!state.EntityManager.HasComponent<PlayerShieldData>(playerEntity))
        {
            state.EntityManager.AddComponentData(playerEntity, new PlayerShieldData
            {
                SkillLevel = 0,
                CurrentCharges = 0,
                MaxCharges = 1,
                HitsRemainingInCurrentShield = 0,
                HitsPerShield = 1,
                RechargeTimer = 0f,
                RechargeCooldown = 30f,
                HasJustBlocked = false
            });
            return;
        }

        Entity skill = _skillQuery.GetSingletonEntity();
        SkillRuntimeData runtime = state.EntityManager.GetComponentData<SkillRuntimeData>(skill);
        ShieldSkillData skillData = state.EntityManager.GetComponentData<ShieldSkillData>(skill);
        PlayerShieldData shield = state.EntityManager.GetComponentData<PlayerShieldData>(playerEntity);

        float dt = SystemAPI.Time.DeltaTime;

        // 2. Đồng bộ khi Skill Level thay đổi (nâng cấp hoặc điều chỉnh trong Dev Mode)
        if (shield.SkillLevel != runtime.Level)
        {
            int oldLevel = shield.SkillLevel;
            shield.SkillLevel = runtime.Level;

            if (shield.SkillLevel <= 0)
            {
                shield.CurrentCharges = 0;
                shield.HitsRemainingInCurrentShield = 0;
                shield.MaxCharges = 1;
                shield.HitsPerShield = 1;
                shield.RechargeCooldown = skillData.BaseCooldown;
                shield.RechargeTimer = 0f;
            }
            else
            {
                // Cấu hình thông số theo cấp độ
                switch (shield.SkillLevel)
                {
                    case 1:
                        shield.MaxCharges = 1;
                        shield.HitsPerShield = 1;
                        shield.RechargeCooldown = skillData.BaseCooldown; // 30s
                        break;
                    case 2:
                        shield.MaxCharges = 1;
                        shield.HitsPerShield = 1;
                        shield.RechargeCooldown = skillData.Lv2Cooldown;  // 18s
                        break;
                    case 3:
                        shield.MaxCharges = 2;
                        shield.HitsPerShield = 1;
                        shield.RechargeCooldown = skillData.Lv3Cooldown;  // 16s
                        break;
                    case 4:
                    default:
                        shield.MaxCharges = 2;
                        shield.HitsPerShield = 2;
                        shield.RechargeCooldown = skillData.Lv4Cooldown;  // 14s
                        break;
                }

                // Khi vừa học kỹ năng (từ Lv0 lên Lv1) hoặc nâng cấp, lập tức hồi đầy khiên để thưởng cho người chơi!
                if (oldLevel < shield.SkillLevel)
                {
                    shield.CurrentCharges = shield.MaxCharges;
                    shield.HitsRemainingInCurrentShield = shield.HitsPerShield;
                    shield.RechargeTimer = 0f;
                }
                else
                {
                    // Trường hợp hạ cấp qua Dev Mode
                    shield.CurrentCharges = math.clamp(shield.CurrentCharges, 0, shield.MaxCharges);
                    shield.HitsRemainingInCurrentShield = math.clamp(shield.HitsRemainingInCurrentShield, 0, shield.HitsPerShield);
                }
            }
        }

        // 3. Cơ chế hồi phục từng lớp khiên theo thời gian (Charge Recharge)
        if (shield.SkillLevel > 0)
        {
            if (shield.CurrentCharges < shield.MaxCharges)
            {
                shield.RechargeTimer += dt;
                if (shield.RechargeTimer >= shield.RechargeCooldown)
                {
                    shield.RechargeTimer = 0f;
                    shield.CurrentCharges++;

                    // Nếu chưa có lớp khiên nào đang kích hoạt thì bật lớp mới lên
                    if (shield.HitsRemainingInCurrentShield <= 0)
                    {
                        shield.HitsRemainingInCurrentShield = shield.HitsPerShield;
                    }
                }
            }
            else
            {
                shield.RechargeTimer = 0f;
            }
        }

        // Cập nhật lại SkillRuntimeData Cooldown và Timer để UI/Dev Mode tiện theo dõi
        runtime.Cooldown = shield.RechargeCooldown;
        runtime.Timer = shield.RechargeTimer;
        state.EntityManager.SetComponentData(skill, runtime);

        state.EntityManager.SetComponentData(playerEntity, shield);
    }
}
