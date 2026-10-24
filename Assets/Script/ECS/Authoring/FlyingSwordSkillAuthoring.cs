using Unity.Entities;
using UnityEngine;

public class FlyingSwordSkillAuthoring : MonoBehaviour, ISkillEffectAuthoring
{
    [Header("Sword Prefab")]
    public GameObject SwordPrefab;

    [Header("Chỉ số cơ bản (Level 1)")]
    public float BaseDamage = 18f;
    public float RushSpeed = 12f;
    public float ReturnSpeed = 14f;
    public float OrbitRadius = 2.2f;
    public float OrbitSpeed = 3.5f;
    public float AttackInterval = 0.5f; // Khoảng dừng ngắn khi ở IDLE
    public float HitRadius = 0.5f;

    [Header("Phạm vi hoạt động (Tầm nhìn Camera)")]
    [Tooltip("Chỉ phát hiện quái xuất hiện trong phạm vi camera quanh Player")]
    public float DetectRange = 6.5f;
    [Tooltip("Khoảng cách tối đa kiếm được phép cách xa Player trước khi bị buộc quay về")]
    public float MaxLeashRange = 7.5f;

    [Header("Chỉ số Ultimate (Level 4 AOE)")]
    [Tooltip("Thời gian hồi chiêu riêng của đòn Ultimate Level 4")]
    public float UltimateCooldown = 6.0f;
    public float AoeRadius = 3.5f;
    public float AoeDamage = 120f;

    public void Bake(IBaker baker, Entity entity)
    {
        if (SwordPrefab == null)
        {
            Debug.LogError($"[FlyingSwordSkillAuthoring] Chưa gán SwordPrefab trên {name}", this);
            return;
        }

        baker.AddComponent<FlyingSwordSkillTag>(entity);
        baker.AddComponent(entity, new FlyingSwordSkillData
        {
            SwordPrefab = baker.GetEntity(SwordPrefab, TransformUsageFlags.Dynamic),
            BaseDamage = BaseDamage,
            RushSpeed = RushSpeed,
            ReturnSpeed = ReturnSpeed,
            OrbitRadius = OrbitRadius,
            OrbitSpeed = OrbitSpeed,
            AttackInterval = AttackInterval,
            HitRadius = HitRadius,
            AoeRadius = AoeRadius,
            AoeDamage = AoeDamage,
            DetectRange = DetectRange,
            MaxLeashRange = MaxLeashRange,
            UltimateCooldown = UltimateCooldown
        });
    }
}
