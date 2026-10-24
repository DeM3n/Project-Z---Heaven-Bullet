using Unity.Entities;
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

        while (level.CurrentExp >= level.ExpToNextLevel)
        {
            level.CurrentExp -= level.ExpToNextLevel;
            level.Level += 1;
            // Scale lượng EXP cần thiết: x1.35 + 10 giúp giãn nhịp độ lên cấp, cân bằng độ khó theo thời gian
            level.ExpToNextLevel = Mathf.RoundToInt(level.ExpToNextLevel * 1.35f + 10f);
            leveledUp = true;
        }

        if (leveledUp)
        {
            Debug.Log($"[LevelUp] Player reached Level {level.Level}! EXP: {level.CurrentExp}/{level.ExpToNextLevel}");
            SystemAPI.SetComponent(playerEntity, level);
        }
    }
}