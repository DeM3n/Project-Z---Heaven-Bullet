using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Thuật toán chọn ngẫu nhiên các lựa chọn Perk khi lên cấp,
/// hỗ trợ độ ưu tiên (Priority), trọng số (Weight) và loại trừ thẻ cũ khi Reroll.
/// </summary>
public class PerkSelector
{
    private readonly List<IPerkDefinition> _definitions;

    public PerkSelector(IEnumerable<IPerkDefinition> definitions)
    {
        _definitions = new List<IPerkDefinition>(definitions);
    }

    /// <summary>
    /// Lấy danh sách perk ngẫu nhiên có xét Priority và Weight.
    /// </summary>
    public List<IPerkDefinition> SelectPerks(EntityManager em, Entity player, int count, HashSet<string> excludedIds = null)
    {
        var available = _definitions
            .Where(p => p.IsAvailable(em, player))
            .ToList();

        // 1. Tách các Perk có Priority đặc biệt (ví dụ: SwordFusion khi đủ điều kiện)
        var highPriority = available.Where(p => p.Priority > 0).OrderByDescending(p => p.Priority).ToList();
        var normalPool = available.Where(p => p.Priority <= 0).ToList();

        // 2. Lọc trừ danh sách excluded khi reroll nếu có đủ lựa chọn thay thế
        if (excludedIds != null && excludedIds.Count > 0)
        {
            var filtered = normalPool.Where(p => !excludedIds.Contains(p.PerkId)).ToList();
            if (filtered.Count + highPriority.Count >= count)
            {
                normalPool = filtered;
            }
        }

        var selected = new List<IPerkDefinition>();

        // Ưu tiên đưa các thẻ High Priority vào trước (ví dụ Sword Fusion)
        foreach (var hp in highPriority)
        {
            if (selected.Count < count)
            {
                selected.Add(hp);
            }
        }

        // 3. Chọn phần còn lại theo Weighted Random
        while (selected.Count < count && normalPool.Count > 0)
        {
            float totalWeight = normalPool.Sum(p => Mathf.Max(0.1f, p.Weight));
            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float currentSum = 0f;
            int pickedIndex = 0;

            for (int i = 0; i < normalPool.Count; i++)
            {
                currentSum += Mathf.Max(0.1f, normalPool[i].Weight);
                if (roll <= currentSum)
                {
                    pickedIndex = i;
                    break;
                }
            }

            selected.Add(normalPool[pickedIndex]);
            normalPool.RemoveAt(pickedIndex);
        }

        return selected;
    }

    /// <summary>
    /// Lấy danh sách count vũ khí khởi đầu ngẫu nhiên chưa mở khóa (Level == 0) để người chơi lựa chọn.
    /// </summary>
    public List<IPerkDefinition> SelectStarterWeapons(EntityManager em, Entity player, int count, HashSet<string> excludedIds = null)
    {
        var starterPool = _definitions
            .Where(p => p.IsWeapon && p.IsAvailable(em, player))
            .Where(p =>
            {
                var opt = p.GetOptionData(em, player);
                return !opt.IsFusion && opt.Level == 1; // opt.Level == 1 tức hiện tại đang Level 0 (chưa mở khóa)
            })
            .ToList();

        if (excludedIds != null && excludedIds.Count > 0)
        {
            var filtered = starterPool.Where(p => !excludedIds.Contains(p.PerkId)).ToList();
            if (filtered.Count >= count)
            {
                starterPool = filtered;
            }
        }

        var selected = new List<IPerkDefinition>();
        var poolCopy = new List<IPerkDefinition>(starterPool);

        while (selected.Count < count && poolCopy.Count > 0)
        {
            int idx = UnityEngine.Random.Range(0, poolCopy.Count);
            selected.Add(poolCopy[idx]);
            poolCopy.RemoveAt(idx);
        }

        return selected;
    }
}

