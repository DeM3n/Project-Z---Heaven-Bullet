using Unity.Entities;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    public float MoveSpeed = 5f;
    public int StartingExpToNextLevel = 25;
    public float PickupRange = 2f;
    public float MaxHP = 100f;
    [Tooltip("Thời gian miễn nhiễm sát thương sau khi bị trúng đòn (i-frames tính bằng giây)")]
    public float InvulnerabilityDuration = 0.2f;

    class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerTag>(entity);
            AddComponent(entity, new MovementData { MoveSpeed = authoring.MoveSpeed });
            AddComponent(entity, new PlayerLevelData { Level = 1, CurrentExp = 0, ExpToNextLevel = authoring.StartingExpToNextLevel });
            AddComponent(entity, new PickupRangeData {Range = authoring.PickupRange});
            AddComponent(entity, new HealthData { CurrentHP = authoring.MaxHP, MaxHP = authoring.MaxHP });
            AddComponent(entity, new PlayerInvulnerabilityData
            {
                Duration = authoring.InvulnerabilityDuration,
                CurrentTimer = 0f
            });
            AddComponent(entity, new PlayerShieldData
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
            AddComponent(entity, new PlayerRelicData
            {
                BootsLevel = 0,
                MagnetLevel = 0,
                VitalityLevel = 0,
                PowerLevel = 0,
                FortuneDiceLevel = 0,
                DamageMultiplier = 1.0f
            });
            AddBuffer<IncomingDamage>(entity);
        }
    }
}