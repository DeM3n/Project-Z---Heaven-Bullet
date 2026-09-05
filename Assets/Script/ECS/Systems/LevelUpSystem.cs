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
            level.ExpToNextLevel = Mathf.RoundToInt(level.ExpToNextLevel * 1.2f); // soft-exponential theo GDD 5.1
            leveledUp = true;
        }

        if (leveledUp)
        {
            Debug.Log($"[LevelUp] Player lên Level {level.Level}! EXP: {level.CurrentExp}/{level.ExpToNextLevel}");
            SystemAPI.SetComponent(playerEntity, level);
        }
    }
}