using Unity.Entities;
using UnityEngine;

// Gắn lên 1 GameObject riêng cho MỖI skill (không gắn lên Player).
// Kéo Player vào OwnerAuthoring, rồi gắn thêm 1 component hiệu ứng (ProjectileSkillAuthoring/SummonSkillAuthoring...).
public class SkillAuthoring : MonoBehaviour
{
    [Header("Chủ sở hữu skill (kéo GameObject Player vào đây)")]
    public GameObject OwnerAuthoring;

    [Header("Chỉ số chung mọi skill")]
    public string SkillId = "skill_new";
    public int Level = 1;
    public float Cooldown = 1f;

    class Baker : Baker<SkillAuthoring>
    {
        public override void Bake(SkillAuthoring authoring)
        {
            if (authoring.OwnerAuthoring == null)
            {
                Debug.LogError($"[SkillAuthoring] Chưa gán OwnerAuthoring trên {authoring.name}", authoring);
                return;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new SkillOwnerData
            {
                Owner = GetEntity(authoring.OwnerAuthoring, TransformUsageFlags.Dynamic)
            });
            AddComponent(entity, new SkillRuntimeData
            {
                Cooldown = authoring.Cooldown,
                Timer = 0f,
                Level = authoring.Level,
                SkillId = authoring.SkillId
            });

            // Tự động bake hiệu ứng gắn kèm — thêm skill type mới KHÔNG cần sửa file này.
            var effects = authoring.GetComponents<ISkillEffectAuthoring>();
            if (effects.Length != 1)
            {
                Debug.LogWarning($"[SkillAuthoring] {authoring.name} cần đúng 1 hiệu ứng skill (Projectile/Summon/...), hiện có {effects.Length}.", authoring);
            }

            foreach (var effect in effects)
            {
                DependsOn(effect as Object); // đảm bảo incremental baking re-chạy khi số liệu hiệu ứng đổi
                effect.Bake(this, entity);
            }
        }
    }
}