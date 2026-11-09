using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Hệ thống điều phối vòng đời và tự động nhân bản toàn bộ kỹ năng của Phân Thân Ảo Ảnh:
/// 1. Sinh 2 Phân thân (Trái -1.6m, Phải +1.6m) khi Người chơi sở hữu Cổ Vật Mirror of Illusion (MirrorLevel >= 1).
/// 2. Cơ chế Generic Zero-Boilerplate: Tự động nhân bản (ecb.Instantiate) MỌI thực thể kỹ năng của Player
///    sang 2 Phân thân mà không cần code riêng cho từng kỹ năng. Bất kỳ kỹ năng mới nào được thêm vào game
///    sẽ tự động hoạt động trên Phân Thân với 0 dòng code cấu hình.
/// 3. Tự động áp dụng Cooldown = 150% (1.50x) trực tiếp vào SkillRuntimeData và đồng bộ cấp độ khi Player lên cấp.
/// </summary>
[UpdateAfter(typeof(PlayerCloneMovementSystem))]
public partial struct PlayerCloneSkillSyncSystem : ISystem
{
    private EntityQuery _playerQuery;
    private EntityQuery _cloneQuery;
    private EntityQuery _playerSkillQuery;
    private EntityQuery _clonedSkillQuery;

    public void OnCreate(ref SystemState state)
    {
        _playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerTag, PlayerRelicData, LocalTransform>()
            .Build();

        _cloneQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerCloneTag, PlayerCloneData, LocalTransform>()
            .Build();

        // Kỹ năng gốc của Player: sở hữu bởi Player và KHÔNG mang CloneSkillTag
        _playerSkillQuery = SystemAPI.QueryBuilder()
            .WithAll<SkillOwnerData, SkillRuntimeData>()
            .WithNone<CloneSkillTag>()
            .Build();

        // Kỹ năng nhân bản của Phân thân
        _clonedSkillQuery = SystemAPI.QueryBuilder()
            .WithAll<SkillOwnerData, SkillRuntimeData, ClonedSkillLink, CloneSkillTag>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (_playerQuery.IsEmpty) return;

        Entity playerEntity = _playerQuery.GetSingletonEntity();
        var relicData = SystemAPI.GetComponent<PlayerRelicData>(playerEntity);

        // Chưa sở hữu Cổ Vật Mirror of Illusion -> Không kích hoạt phân thân
        if (relicData.MirrorLevel <= 0) return;

        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        PlayerShieldData playerShield = SystemAPI.HasComponent<PlayerShieldData>(playerEntity)
            ? SystemAPI.GetComponent<PlayerShieldData>(playerEntity)
            : default;

        int cloneCount = _cloneQuery.CalculateEntityCount();
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 1. SINH 2 PHÂN THÂN NẾU CHƯA CÓ
        if (cloneCount < 2)
        {
            // Phân thân 0 (Bên Trái: -5.2m, đảm bảo quỹ đạo kiếm OrbitRadius 2.2m không chạm nhau)
            Entity clone0 = ecb.CreateEntity();
            ecb.AddComponent<PlayerCloneTag>(clone0);
            ecb.AddComponent(clone0, LocalTransform.FromPosition(playerPos + new float3(-5.2f, 0f, 0f)));
            ecb.AddComponent(clone0, new PlayerCloneData
            {
                CloneIndex = 0,
                BaseOffset = new float3(-5.2f, 0f, 0f),
                FacingDirection = 1f,
                FollowSpeed = 22f
            });
            ecb.AddComponent(clone0, playerShield);

            // Phân thân 1 (Bên Phải: +5.2m, đảm bảo quỹ đạo kiếm OrbitRadius 2.2m không chạm nhau)
            Entity clone1 = ecb.CreateEntity();
            ecb.AddComponent<PlayerCloneTag>(clone1);
            ecb.AddComponent(clone1, LocalTransform.FromPosition(playerPos + new float3(5.2f, 0f, 0f)));
            ecb.AddComponent(clone1, new PlayerCloneData
            {
                CloneIndex = 1,
                BaseOffset = new float3(5.2f, 0f, 0f),
                FacingDirection = 1f,
                FollowSpeed = 22f
            });
            ecb.AddComponent(clone1, playerShield);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            return;
        }

        // 2. ĐỒNG BỘ THÔNG SỐ SHIELD VÀ ĐẢM BẢO BASE OFFSET CHO TỪNG PHÂN THÂN
        var cloneEntities = _cloneQuery.ToEntityArray(Allocator.Temp);
        for (int c = 0; c < cloneEntities.Length; c++)
        {
            Entity clone = cloneEntities[c];

            // Tự động điều chỉnh khoảng cách sườn nếu clone đang dùng khoảng cách cũ
            if (SystemAPI.HasComponent<PlayerCloneData>(clone))
            {
                var cloneData = SystemAPI.GetComponent<PlayerCloneData>(clone);
                float targetOffsetX = cloneData.CloneIndex == 0 ? -5.2f : 5.2f;
                if (math.abs(cloneData.BaseOffset.x - targetOffsetX) > 0.01f)
                {
                    cloneData.BaseOffset = new float3(targetOffsetX, 0f, 0f);
                    SystemAPI.SetComponent(clone, cloneData);
                }
            }

            if (SystemAPI.HasComponent<PlayerShieldData>(clone) && SystemAPI.HasComponent<PlayerShieldData>(playerEntity))
            {
                var cloneShield = SystemAPI.GetComponent<PlayerShieldData>(clone);
                if (cloneShield.SkillLevel != playerShield.SkillLevel)
                {
                    cloneShield.SkillLevel = playerShield.SkillLevel;
                    cloneShield.MaxCharges = playerShield.MaxCharges;
                    cloneShield.HitsPerShield = playerShield.HitsPerShield;
                    cloneShield.RechargeCooldown = playerShield.RechargeCooldown;
                    if (cloneShield.CurrentCharges <= 0 && playerShield.CurrentCharges > 0)
                    {
                        cloneShield.CurrentCharges = playerShield.CurrentCharges;
                        cloneShield.HitsRemainingInCurrentShield = playerShield.HitsRemainingInCurrentShield;
                    }
                    SystemAPI.SetComponent(clone, cloneShield);
                }
            }
        }

        // 3. GENERIC SKILL SYNC PIPELINE (Zero-Boilerplate)
        // Duyệt toàn bộ kỹ năng gốc của Player, tự động nhân bản và đồng bộ cấp sang Phân thân
        var playerSkillEntities = _playerSkillQuery.ToEntityArray(Allocator.Temp);
        var playerSkillOwners = _playerSkillQuery.ToComponentDataArray<SkillOwnerData>(Allocator.Temp);
        var playerSkillRuntimes = _playerSkillQuery.ToComponentDataArray<SkillRuntimeData>(Allocator.Temp);

        var clonedSkillEntities = _clonedSkillQuery.ToEntityArray(Allocator.Temp);
        var clonedSkillLinks = _clonedSkillQuery.ToComponentDataArray<ClonedSkillLink>(Allocator.Temp);
        var clonedSkillRuntimes = _clonedSkillQuery.ToComponentDataArray<SkillRuntimeData>(Allocator.Temp);

        // A. Dọn dẹp các kỹ năng phân thân mà kỹ năng gốc của Player không còn tồn tại
        for (int i = 0; i < clonedSkillEntities.Length; i++)
        {
            Entity originalPlayerSkill = clonedSkillLinks[i].PlayerSkillEntity;
            if (!state.EntityManager.Exists(originalPlayerSkill))
            {
                ecb.DestroyEntity(clonedSkillEntities[i]);
            }
        }

        // B. Đồng bộ và nhân bản kỹ năng cho từng phân thân
        for (int c = 0; c < cloneEntities.Length; c++)
        {
            Entity clone = cloneEntities[c];

            for (int p = 0; p < playerSkillEntities.Length; p++)
            {
                if (playerSkillOwners[p].Owner != playerEntity) continue;

                Entity playerSkill = playerSkillEntities[p];
                SkillRuntimeData pRuntime = playerSkillRuntimes[p];

                // Tìm kỹ năng nhân bản tương ứng trên phân thân này
                Entity existingCloneSkill = Entity.Null;
                int existingCloneSkillIndex = -1;

                for (int k = 0; k < clonedSkillEntities.Length; k++)
                {
                    if (clonedSkillLinks[k].CloneOwner == clone &&
                        clonedSkillLinks[k].PlayerSkillEntity == playerSkill)
                    {
                        existingCloneSkill = clonedSkillEntities[k];
                        existingCloneSkillIndex = k;
                        break;
                    }
                }

                if (existingCloneSkill == Entity.Null)
                {
                    // Chỉ nhân bản nếu kỹ năng đã được mở khóa (Level > 0)
                    if (pRuntime.Level > 0)
                    {
                        Entity cloneSkill = ecb.Instantiate(playerSkill);
                        ecb.SetComponent(cloneSkill, new SkillOwnerData { Owner = clone });
                        ecb.AddComponent(cloneSkill, new ClonedSkillLink
                        {
                            PlayerSkillEntity = playerSkill,
                            CloneOwner = clone
                        });
                        ecb.AddComponent<CloneSkillTag>(cloneSkill);

                        // Cooldown kỹ năng phân thân = 150% (1.50x) Cooldown kỹ năng Player
                        var cRuntime = pRuntime;
                        cRuntime.Cooldown = pRuntime.Cooldown * 1.50f;
                        cRuntime.Timer = 0.4f + c * 0.25f; // Lệch pha nhẹ để 2 clone không xuất chiêu cùng 1 microsecond
                        ecb.SetComponent(cloneSkill, cRuntime);
                    }
                }
                else
                {
                    // Kỹ năng đã tồn tại trên clone -> Đồng bộ Level và Cooldown khi có thay đổi
                    SkillRuntimeData cRuntime = clonedSkillRuntimes[existingCloneSkillIndex];
                    if (cRuntime.Level != pRuntime.Level)
                    {
                        cRuntime.Level = pRuntime.Level;
                        cRuntime.Cooldown = pRuntime.Cooldown * 1.50f;
                        ecb.SetComponent(existingCloneSkill, cRuntime);
                    }
                }
            }
        }

        playerSkillEntities.Dispose();
        playerSkillOwners.Dispose();
        playerSkillRuntimes.Dispose();

        clonedSkillEntities.Dispose();
        clonedSkillLinks.Dispose();
        clonedSkillRuntimes.Dispose();

        cloneEntities.Dispose();

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
