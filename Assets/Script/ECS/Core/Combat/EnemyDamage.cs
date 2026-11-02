using System.Runtime.CompilerServices;
using Unity.Entities;

/// <summary>
/// Helper duy nhất để đẩy gói sát thương vào buffer tiếp nhận đòn của quái vật (Enemy Incoming Damage Pipeline).
/// Các system gây sát thương TUYỆT ĐỐI gọi qua helper này, không tự GetBuffer/Add rải rác.
/// </summary>
public static class EnemyDamage
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add(ref BufferLookup<EnemyIncomingDamage> lookup, Entity target, ushort skillId, float amount)
    {
        if (amount <= 0f) return;

        if (lookup.HasBuffer(target))
        {
            lookup[target].Add(new EnemyIncomingDamage
            {
                SkillId = skillId,
                Amount = amount
            });
        }
#if UNITY_EDITOR
        else
        {
            UnityEngine.Debug.LogError($"[EnemyDamage] Quái vật Entity {target.Index} thiếu buffer EnemyIncomingDamage khi nhận sát thương từ SkillId {skillId}!");
        }
#endif
    }
}

