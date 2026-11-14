using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Không [BurstCompile]: hệ thống này chỉ chạy trên 1 player entity/frame,
// cần Debug.Log (managed code) để xác nhận loop chạy đúng trước khi có UI thật.
[UpdateAfter(typeof(XPPickupSystem))]
public partial struct LevelUpSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;

        var level = SystemAPI.GetComponent<PlayerLevelData>(playerEntity);
        bool leveledUp = false;
        int levelsGained = 0;

        while (level.CurrentExp >= level.ExpToNextLevel)
        {
            level.CurrentExp -= level.ExpToNextLevel;
            level.Level += 1;
            // Scale lượng EXP cần thiết: x1.35 + 9.5f (giảm ~5% EXP yêu cầu ở mọi cấp để cân bằng nhịp độ lên cấp)
            level.ExpToNextLevel = Mathf.RoundToInt(level.ExpToNextLevel * 1.35f + 9.5f);
            leveledUp = true;
            levelsGained++;
        }

        if (leveledUp)
        {
            Debug.Log($"[LevelUp] Player reached Level {level.Level}! EXP: {level.CurrentExp}/{level.ExpToNextLevel}");
            SystemAPI.SetComponent(playerEntity, level);

            // Mỗi khi Player Level Up: +10 Máu Tối Đa (Max HP) cho mỗi cấp độ đạt được
            if (SystemAPI.HasComponent<HealthData>(playerEntity))
            {
                var hp = SystemAPI.GetComponent<HealthData>(playerEntity);
                float maxHpBonus = 10f * levelsGained;
                hp.MaxHP += maxHpBonus;
                hp.CurrentHP += maxHpBonus; // Đồng thời cộng thêm lượng máu tối đa vừa tăng vào máu hiện tại
                SystemAPI.SetComponent(playerEntity, hp);
                Debug.Log($"[LevelUp] Max HP increased by +{maxHpBonus}! Current HP: {hp.CurrentHP}/{hp.MaxHP}");
            }
        }
    }
}
