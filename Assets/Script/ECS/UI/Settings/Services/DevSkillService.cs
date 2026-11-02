using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Nơi duy nhất đọc và ghi dữ liệu cấp độ kỹ năng vào ECS cho Developer Control Panel:
/// - Lazy EntityQuery (không lưu lâu dài qua các lần chuyển cảnh / restart World).
/// - Sử dụng TryGetSingletonEntity an toàn.
/// - Xử lý tăng cấp từ 0 lên 1 an toàn (không return im lặng).
/// </summary>
public class DevSkillService
{
    private EntityManager EntityManager
    {
        get
        {
            var world = World.DefaultGameObjectInjectionWorld;
            return (world != null && world.IsCreated) ? world.EntityManager : default;
        }
    }

    public List<DevSkillInfo> GetDevSkillInfos()
    {
        var list = new List<DevSkillInfo>();
        foreach (var def in SkillDefinitionRegistry.AllSkills)
        {
            list.Add(new DevSkillInfo(def.SkillId, def.DisplayTitle, def.MaxLevel, def.ThemeColor, def.IsFusion));
        }
        return list;
    }

    public List<DevSkillState> GetAllSkillStates()
    {
        var list = new List<DevSkillState>();
        var em = EntityManager;
        if (em == default) return list;

        foreach (var def in SkillDefinitionRegistry.AllSkills)
        {
            list.Add(GetSkillState(def.SkillId));
        }
        return list;
    }

    public DevSkillState GetSkillState(string skillId)
    {
        var em = EntityManager;
        if (em == default) return new DevSkillState(skillId, 0, false);

        var query = GetQueryForSkill(em, skillId);
        if (query.IsEmpty)
        {
            return new DevSkillState(skillId, 0, false);
        }

        Entity e = query.GetSingletonEntity();
        if (!em.HasComponent<SkillRuntimeData>(e))
        {
            return new DevSkillState(skillId, 0, false);
        }

        var runtime = em.GetComponentData<SkillRuntimeData>(e);
        return new DevSkillState(skillId, runtime.Level, runtime.Level > 0);
    }

    public void SetSkillLevel(string skillId, int delta, int directLevel = -1)
    {
        var em = EntityManager;
        if (em == default) return;

        var def = SkillDefinitionRegistry.GetById(skillId);
        if (def == null) return;

        var query = GetQueryForSkill(em, skillId);
        if (query.IsEmpty) return;

        Entity skillEntity = query.GetSingletonEntity();
        if (!em.HasComponent<SkillRuntimeData>(skillEntity)) return;

        var runtime = em.GetComponentData<SkillRuntimeData>(skillEntity);
        int targetLvl = (directLevel >= 0) ? directLevel : math.clamp(runtime.Level + delta, 0, def.MaxLevel);

        runtime.Level = targetLvl;
        if (targetLvl > 0 && runtime.Timer <= 0f)
        {
            runtime.Timer = 0.5f; // Khởi tạo timer ra đòn ban đầu nếu vừa kích hoạt từ 0 -> 1
        }
        em.SetComponentData(skillEntity, runtime);
    }

    public void ToggleFusion()
    {
        var em = EntityManager;
        if (em == default) return;

        var query = em.CreateEntityQuery(ComponentType.ReadOnly<SwordFusionTag>(), ComponentType.ReadWrite<SkillRuntimeData>());
        if (query.IsEmpty) return;

        Entity e = query.GetSingletonEntity();
        var runtime = em.GetComponentData<SkillRuntimeData>(e);
        runtime.Level = (runtime.Level > 0) ? 0 : 1;
        runtime.Timer = 0.5f;
        em.SetComponentData(e, runtime);
    }

    public void MaxAllSkills()
    {
        var em = EntityManager;
        if (em == default) return;

        foreach (var def in SkillDefinitionRegistry.AllSkills)
        {
            if (def.IsFusion)
            {
                var fQuery = em.CreateEntityQuery(ComponentType.ReadOnly<SwordFusionTag>(), ComponentType.ReadWrite<SkillRuntimeData>());
                if (!fQuery.IsEmpty)
                {
                    Entity e = fQuery.GetSingletonEntity();
                    var runtime = em.GetComponentData<SkillRuntimeData>(e);
                    runtime.Level = 1;
                    runtime.Timer = 0.5f;
                    em.SetComponentData(e, runtime);
                }
            }
            else
            {
                SetSkillLevel(def.SkillId, 0, def.MaxLevel);
            }
        }
    }

    private static EntityQuery GetQueryForSkill(EntityManager em, string skillId)
    {
        return skillId switch
        {
            "FlyingSword" => em.CreateEntityQuery(ComponentType.ReadOnly<FlyingSwordSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>()),
            "SlashSword" => em.CreateEntityQuery(ComponentType.ReadOnly<SlashSwordSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>()),
            "ProjectileSkill" => em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>()),
            "Fireball" => em.CreateEntityQuery(ComponentType.ReadOnly<FireballSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>()),
            "Shield" => em.CreateEntityQuery(ComponentType.ReadOnly<ShieldSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>()),
            "Boomerang" => em.CreateEntityQuery(ComponentType.ReadOnly<BoomerangSkillTag>(), ComponentType.ReadWrite<SkillRuntimeData>()),
            "SwordFusion" => em.CreateEntityQuery(ComponentType.ReadOnly<SwordFusionTag>(), ComponentType.ReadWrite<SkillRuntimeData>()),
            _ => default
        };
    }
}

