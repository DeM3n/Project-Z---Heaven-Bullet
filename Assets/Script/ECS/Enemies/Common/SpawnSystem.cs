using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct SpawnSystem : ISystem
{
    /// <summary>
    /// Hệ số điều chỉnh số lượng quái xuất hiện toàn game (mặc định giảm 10% = 0.90f theo yêu cầu cân bằng).
    /// </summary>
    public const float ENEMY_SPAWN_COUNT_MULTIPLIER = 0.90f;

    /// <summary>
    /// Giảm số lượng quái xuất hiện 10% (ENEMY_SPAWN_COUNT_MULTIPLIER = 0.90f).
    /// Áp dụng thuật toán làm tròn ngẫu nhiên (Stochastic / Probabilistic Rounding):
    /// Giữ nguyên kỳ vọng toán học (Mathematical Expectation) chính xác 100% (-10% số quái)
    /// cho cả các số lượng nhỏ (1, 2, 3...) lẫn các đợt bão quái lớn.
    /// </summary>
    [BurstCompile]
    public static int ScaleSpawnCount(int baseCount, ref Unity.Mathematics.Random rng, float multiplier = ENEMY_SPAWN_COUNT_MULTIPLIER)
    {
        if (baseCount <= 0) return 0;
        float target = baseCount * multiplier;
        int floor = (int)math.floor(target);
        float frac = target - floor;
        int result = floor + ((frac > 0f && rng.NextFloat() < frac) ? 1 : 0);
        return math.max(0, result);
    }

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out Entity playerEntity)) return;
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

        int playerLevel = 1;
        if (SystemAPI.HasComponent<PlayerLevelData>(playerEntity))
        {
            playerLevel = SystemAPI.GetComponent<PlayerLevelData>(playerEntity).Level;
        }

        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (spawner, waveManager, spawnEntries) in
                 SystemAPI.Query<RefRW<SpawnerData>, RefRW<WaveManagerData>, DynamicBuffer<EnemySpawnEntry>>())
        {
            if (spawnEntries.Length == 0) continue;

            Entity chaserPrefab = GetPrefab(spawnEntries, EnemyBehaviorType.Chaser);
            if (chaserPrefab == Entity.Null) chaserPrefab = spawnEntries[0].Prefab;
            Entity bomberPrefab = GetPrefab(spawnEntries, EnemyBehaviorType.Bomber);
            if (bomberPrefab == Entity.Null) bomberPrefab = chaserPrefab;
            Entity shooterPrefab = GetPrefab(spawnEntries, EnemyBehaviorType.Shooter);
            if (shooterPrefab == Entity.Null) shooterPrefab = chaserPrefab;
            Entity tankPrefab = GetPrefab(spawnEntries, EnemyBehaviorType.Tank);
            if (tankPrefab == Entity.Null) tankPrefab = chaserPrefab;
            Entity chargerPrefab = GetPrefab(spawnEntries, EnemyBehaviorType.Charger);

            // =========================================================================
            // GIAI ĐOẠN 1: ĐANG DIỄN RA MEGA WAVE (THỬ THÁCH SINH TỒN TRONG 1 PHÚT)
            // =========================================================================
            if (waveManager.ValueRO.State == WaveState.MegaWaveActive)
            {
                // Đồng hồ tiếp tục đếm tiến trình thời gian sinh tồn
                waveManager.ValueRW.IsTimerPaused = false;
                waveManager.ValueRW.ElapsedGameTime += dt;
                spawner.ValueRW.ElapsedGameTime = waveManager.ValueRO.ElapsedGameTime;
                waveManager.ValueRW.MegaWaveTimer -= dt;

                // 1. Kiểm tra điều kiện hoàn thành: Sống sót trọn vẹn 1 phút (60 giây)!
                if (waveManager.ValueRO.MegaWaveTimer <= 0f)
                {
                    // Thưởng lớn khi vượt qua 1 phút sinh tồn khốc liệt: Rương Cổ Vật + Nam Châm Hút EXP
                    SpawnVictoryRewards(ref ecb, playerPos, playerLevel);

                    // Chuyển trạng thái, kết thúc đợt Mega Wave
                    waveManager.ValueRW.State = WaveState.NormalTimeline;
                    waveManager.ValueRW.MegaWaveTimer = 0f;
                    continue;
                }

                int minute = waveManager.ValueRO.ActiveMegaWaveMinute;
                float currentElapsed = waveManager.ValueRO.ElapsedGameTime;
                float currentMinutes = currentElapsed / 60f;
                float currentHpMult = (1.0f + currentMinutes * 0.30f) * (1.0f + math.max(0, playerLevel - 1) * 0.05f);

                // 2. Kích hoạt các đợt đột kích tăng cường (Surges tại giây thứ 20 và giây thứ 40 của đợt)
                // - Đợt đột kích 1 (khi còn 40s, tức sau 20s sinh tồn):
                if (waveManager.ValueRO.MegaWaveTimer <= 40f && waveManager.ValueRO.MegaWaveSurgeStage == 0)
                {
                    waveManager.ValueRW.MegaWaveSurgeStage = 1;
                    SpawnMegaWaveSurge(
                        ref ecb,
                        ref spawner.ValueRW.Rng,
                        ref waveManager.ValueRW,
                        playerPos,
                        minute,
                        1,
                        currentHpMult,
                        chaserPrefab,
                        bomberPrefab,
                        shooterPrefab,
                        tankPrefab,
                        chargerPrefab);
                }
                // - Đợt đột kích 2 (khi còn 20s, tức sau 40s sinh tồn):
                else if (waveManager.ValueRO.MegaWaveTimer <= 20f && waveManager.ValueRO.MegaWaveSurgeStage == 1)
                {
                    waveManager.ValueRW.MegaWaveSurgeStage = 2;
                    SpawnMegaWaveSurge(
                        ref ecb,
                        ref spawner.ValueRW.Rng,
                        ref waveManager.ValueRW,
                        playerPos,
                        minute,
                        2,
                        currentHpMult,
                        chaserPrefab,
                        bomberPrefab,
                        shooterPrefab,
                        tankPrefab,
                        chargerPrefab);
                }

                // 3. Nhịp sinh quái liên tục dồn dập trong suốt 1 phút
                waveManager.ValueRW.MegaWaveSubSpawnTimer -= dt;
                if (waveManager.ValueRO.MegaWaveSubSpawnTimer <= 0f)
                {
                    float subInterval = (minute == 3) ? 1.5f : (minute == 6 ? 1.2f : 1.0f);
                    waveManager.ValueRW.MegaWaveSubSpawnTimer = subInterval;

                    SpawnMegaWaveContinuousPack(
                        ref ecb,
                        ref spawner.ValueRW.Rng,
                        ref waveManager.ValueRW,
                        playerPos,
                        minute,
                        currentHpMult,
                        chaserPrefab,
                        bomberPrefab,
                        shooterPrefab,
                        tankPrefab,
                        chargerPrefab);
                }

                continue; // Trong lúc Mega Wave diễn ra, dừng nhịp sinh quái thường
            }

            // =========================================================================
            // GIAI ĐOẠN 2: TIẾN TRÌNH THỜI GIAN BÌNH THƯỜNG (NORMAL TIMELINE)
            // =========================================================================
            waveManager.ValueRW.IsTimerPaused = false;
            waveManager.ValueRW.ElapsedGameTime += dt;
            spawner.ValueRW.ElapsedGameTime = waveManager.ValueRO.ElapsedGameTime;
            float elapsed = waveManager.ValueRO.ElapsedGameTime;
            float timeMinutesNorm = elapsed / 60f;
            float hpMultiplierNorm = (1.0f + timeMinutesNorm * 0.30f) * (1.0f + math.max(0, playerLevel - 1) * 0.05f);

            // KIỂM TRA KÍCH HOẠT CÁC MỐC MEGA WAVE: 3:00 (180s), 6:00 (360s), 9:00 (540s)
            if (elapsed >= 180f && waveManager.ValueRO.Triggered3Min == 0)
            {
                TriggerMegaWave(ref waveManager.ValueRW, 3, 60f);
                SpawnInitialAmbush(
                    ref ecb,
                    ref spawner.ValueRW.Rng,
                    ref waveManager.ValueRW,
                    playerPos,
                    3,
                    hpMultiplierNorm,
                    chaserPrefab,
                    bomberPrefab,
                    shooterPrefab,
                    tankPrefab,
                    chargerPrefab);
                continue;
            }
            if (elapsed >= 360f && waveManager.ValueRO.Triggered6Min == 0)
            {
                TriggerMegaWave(ref waveManager.ValueRW, 6, 60f);
                SpawnInitialAmbush(
                    ref ecb,
                    ref spawner.ValueRW.Rng,
                    ref waveManager.ValueRW,
                    playerPos,
                    6,
                    hpMultiplierNorm,
                    chaserPrefab,
                    bomberPrefab,
                    shooterPrefab,
                    tankPrefab,
                    chargerPrefab);
                continue;
            }
            if (elapsed >= 540f && waveManager.ValueRO.Triggered9Min == 0)
            {
                TriggerMegaWave(ref waveManager.ValueRW, 9, 60f);
                SpawnInitialAmbush(
                    ref ecb,
                    ref spawner.ValueRW.Rng,
                    ref waveManager.ValueRW,
                    playerPos,
                    9,
                    hpMultiplierNorm,
                    chaserPrefab,
                    bomberPrefab,
                    shooterPrefab,
                    tankPrefab,
                    chargerPrefab);
                continue;
            }

            // SINH QUÁI THEO NHỊP ĐỘ TIẾN TRÌNH THỜI GIAN (WAVE TIMELINE)
            spawner.ValueRW.Timer -= dt;
            if (spawner.ValueRO.Timer > 0f) continue;

            // Tính toán nhịp sinh và số lượng quái theo từng giai đoạn trận đấu:
            // - Giai đoạn đầu (0 - 2 phút): Giảm số lượng, nhịp thưa để người chơi làm quen & nhặt EXP nâng cấp
            // - Giai đoạn giữa (2 - 5 phút): Tăng dần mật độ tạo thử thách
            // - Giai đoạn sau (5 - 9 phút & Endless): Quái ùa ra cực đông (10 - 20 quái mỗi đợt) thử thách hỏa lực tối đa
            float timeMinutes = elapsed / 60f;
            float spawnInterval;
            int countToSpawn;

            if (timeMinutes < 0.5f) // 0s - 30s: Khởi động cực nhẹ
            {
                spawnInterval = 2.0f;
                countToSpawn = 1;
            }
            else if (timeMinutes < 1.0f) // 30s - 60s
            {
                spawnInterval = 1.7f;
                countToSpawn = (spawner.ValueRW.Rng.NextInt(0, 2) == 0) ? 1 : 2;
            }
            else if (timeMinutes < 2.0f) // 1m - 2m
            {
                spawnInterval = 1.4f;
                countToSpawn = 2;
            }
            else if (timeMinutes < 3.0f) // 2m - 3m: Chuẩn bị trước Mega Wave 1
            {
                spawnInterval = 1.1f;
                countToSpawn = 3;
            }
            else if (timeMinutes < 4.0f) // 3m - 4m: (Nằm trong khung Mega Wave 1)
            {
                spawnInterval = 0.95f;
                countToSpawn = 4;
            }
            else if (timeMinutes < 5.0f) // 4m - 5m: Sau Mega Wave 1
            {
                spawnInterval = 0.8f;
                countToSpawn = 6;
            }
            else if (timeMinutes < 6.0f) // 5m - 6m: Dày đặc trước Mega Wave 2
            {
                spawnInterval = 0.7f;
                countToSpawn = 8;
            }
            else if (timeMinutes < 7.0f) // 6m - 7m: (Nằm trong khung Mega Wave 2)
            {
                spawnInterval = 0.6f;
                countToSpawn = 10;
            }
            else if (timeMinutes < 8.0f) // 7m - 8m: Sau Mega Wave 2
            {
                spawnInterval = 0.5f;
                countToSpawn = 12;
            }
            else if (timeMinutes < 9.0f) // 8m - 9m: Bão quái trước Mega Wave 3
            {
                spawnInterval = 0.45f;
                countToSpawn = 14;
            }
            else // 9m+ (Endless Swarm - Biển quái vô tận)
            {
                spawnInterval = 0.4f;
                int extra = math.min(8, (int)((timeMinutes - 9f) * 1.5f));
                countToSpawn = 16 + extra; // 16 đến 24 quái mỗi đợt 0.4s
            }

            // Áp dụng giảm 10% số lượng quái xuất hiện theo thuật toán stochastic rounding
            countToSpawn = ScaleSpawnCount(countToSpawn, ref spawner.ValueRW.Rng);

            spawner.ValueRW.Timer = spawnInterval;
            float hpMultiplier = (1.0f + timeMinutes * 0.30f) * (1.0f + math.max(0, playerLevel - 1) * 0.05f);

            // Xác định trọng số xuất hiện của các loại quái theo giai đoạn
            int wChaser = 50, wShooter = 25, wTank = 15, wBomber = 10;
            if (timeMinutes < 1f)
            {
                wChaser = 80; wBomber = 20; wShooter = 0; wTank = 0;
            }
            else if (timeMinutes < 2f)
            {
                wChaser = 50; wShooter = 30; wBomber = 20; wTank = 0;
            }
            else if (timeMinutes < 3f)
            {
                wChaser = 35; wTank = 30; wShooter = 25; wBomber = 10;
            }
            else if (timeMinutes < 4f)
            {
                wChaser = 40; wBomber = 35; wShooter = 25; wTank = 0;
            }
            else if (timeMinutes < 5f)
            {
                wShooter = 35; wTank = 30; wChaser = 25; wBomber = 10;
            }
            else if (timeMinutes < 6f)
            {
                wChaser = 25; wShooter = 25; wTank = 25; wBomber = 25;
            }
            else if (timeMinutes < 7f)
            {
                wTank = 35; wShooter = 30; wBomber = 20; wChaser = 15;
            }
            else
            {
                wChaser = 30; wShooter = 25; wTank = 25; wBomber = 20;
            }

            int wCharger = (chargerPrefab != Entity.Null && timeMinutes >= 1.0f) ? 20 : 0;
            int totalWeight = wChaser + wShooter + wTank + wBomber + wCharger;

            for (int c = 0; c < countToSpawn; c++)
            {
                int roll = spawner.ValueRW.Rng.NextInt(0, totalWeight);
                Entity chosenPrefab = chaserPrefab;
                if (roll < wChaser)
                {
                    chosenPrefab = chaserPrefab;
                }
                else if (roll < wChaser + wShooter)
                {
                    chosenPrefab = shooterPrefab;
                }
                else if (roll < wChaser + wShooter + wTank)
                {
                    chosenPrefab = tankPrefab;
                }
                else if (roll < wChaser + wShooter + wTank + wBomber)
                {
                    chosenPrefab = bomberPrefab;
                }
                else
                {
                    chosenPrefab = chargerPrefab;
                }

                if (chosenPrefab == Entity.Null) chosenPrefab = spawnEntries[0].Prefab;

                float angle = spawner.ValueRW.Rng.NextFloat(0f, math.PI * 2f);
                float radius = spawner.ValueRO.SpawnRadius + spawner.ValueRW.Rng.NextFloat(-1f, 1.5f);
                float3 spawnPos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);

                Entity newEnemy = ecb.Instantiate(chosenPrefab);
                ecb.SetComponent(newEnemy, LocalTransform.FromPosition(spawnPos));

                // Scale máu theo thời gian
                if (SystemAPI.HasComponent<HealthData>(chosenPrefab))
                {
                    var baseHp = SystemAPI.GetComponent<HealthData>(chosenPrefab);
                    float scaledHp = math.round(baseHp.MaxHP * hpMultiplier);
                    ecb.SetComponent(newEnemy, new HealthData { CurrentHP = scaledHp, MaxHP = scaledHp });
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private static Entity GetPrefab(DynamicBuffer<EnemySpawnEntry> entries, EnemyBehaviorType behavior)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Behavior == behavior)
                return entries[i].Prefab;
        }
        return Entity.Null;
    }

    private static void TriggerMegaWave(ref WaveManagerData wave, int minute, float duration = 60f)
    {
        wave.State = WaveState.MegaWaveActive;
        wave.IsTimerPaused = false;
        wave.ActiveMegaWaveMinute = minute;
        wave.MegaWaveDuration = duration;
        wave.MegaWaveTimer = duration;
        wave.MegaWaveSubSpawnTimer = 0.6f; // Nhịp quái dồn dập đầu tiên sẽ đến sau 0.6s
        wave.MegaWaveSurgeStage = 0;
        wave.MegaWaveEnemiesKilled = 0;
        wave.MegaWaveRemainingEnemies = 0;
        wave.MegaWaveTotalEnemies = 0;

        if (minute == 3) wave.Triggered3Min = 1;
        else if (minute == 6) wave.Triggered6Min = 1;
        else if (minute == 9) wave.Triggered9Min = 1;
    }

    // =========================================================================
    // 1. AMBUSH KHỞI ĐẦU ĐỢT MEGA WAVE (GIÂY 0): VÒNG VÂY KHẨN CẤP
    // =========================================================================
    private static void SpawnInitialAmbush(
        ref EntityCommandBuffer ecb,
        ref Unity.Mathematics.Random rng,
        ref WaveManagerData wave,
        float3 playerPos,
        int minute,
        float hpMultiplier,
        Entity chaserPrefab,
        Entity bomberPrefab,
        Entity shooterPrefab,
        Entity tankPrefab,
        Entity chargerPrefab)
    {
        if (minute == 3)
        {
            // Phút 3: 16 quái bao vây (10 Chasers + 6 Bombers) + 1 Elite Tank -> Giảm 10% còn ~14 quái
            int total = ScaleSpawnCount(16, ref rng);
            for (int i = 0; i < total; i++)
            {
                float angle = (i / (float)total) * math.PI * 2f + rng.NextFloat(-0.06f, 0.06f);
                float radius = 11.0f + rng.NextFloat(-0.5f, 0.5f);
                float3 pos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);

                Entity prefab = (i % 3 == 0) ? bomberPrefab : chaserPrefab;
                SpawnMegaEnemy(ref ecb, prefab, pos, hpMultiplier, 60f);
            }

            // 1 Elite Tank dẫn đầu
            float eliteAngle = rng.NextFloat(0f, math.PI * 2f);
            float3 elitePos = playerPos + new float3(math.cos(eliteAngle) * 11.5f, math.sin(eliteAngle) * 11.5f, 0);
            SpawnMegaElite(ref ecb, tankPrefab, elitePos, 450f * hpMultiplier, 1.4f);

            wave.MegaWaveRemainingEnemies += total + 1;
            wave.MegaWaveTotalEnemies += total + 1;
        }
        else if (minute == 6)
        {
            // Phút 6: 14 Shooters bắn tỉa vòng ngoài + 10 Bombers áp sát vòng trong + 1 Elite Shooter -> Giảm 10%
            int shooterCount = ScaleSpawnCount(14, ref rng);
            for (int i = 0; i < shooterCount; i++)
            {
                float angle = (i / (float)shooterCount) * math.PI * 2f + rng.NextFloat(-0.05f, 0.05f);
                float radius = 12.5f + rng.NextFloat(-0.4f, 0.4f);
                float3 pos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);
                SpawnMegaEnemy(ref ecb, shooterPrefab, pos, hpMultiplier, 100f);
            }

            int innerCount = ScaleSpawnCount(10, ref rng);
            for (int i = 0; i < innerCount; i++)
            {
                float angle = (i / (float)innerCount) * math.PI * 2f + rng.NextFloat(-0.08f, 0.08f);
                float radius = 9.5f + rng.NextFloat(-0.5f, 0.5f);
                float3 pos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);
                Entity prefab = (i % 2 == 0) ? bomberPrefab : chaserPrefab;
                SpawnMegaEnemy(ref ecb, prefab, pos, hpMultiplier, 110f);
            }

            // 1 Elite Shooter
            float3 elitePos = playerPos + new float3(0f, 11f, 0);
            SpawnMegaElite(ref ecb, shooterPrefab, elitePos, 700f * hpMultiplier, 1.45f);

            wave.MegaWaveRemainingEnemies += shooterCount + innerCount + 1;
            wave.MegaWaveTotalEnemies += shooterCount + innerCount + 1;
        }
        else // minute == 9
        {
            // Phút 9: 24 quái đủ mọi chủng loại + 2 Super Elite Bosses ở 2 hướng đối diện -> Giảm 10%
            int count = ScaleSpawnCount(24, ref rng);
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * math.PI * 2f + rng.NextFloat(-0.05f, 0.05f);
                float radius = 11.5f + rng.NextFloat(-0.8f, 0.8f);
                float3 pos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);

                Entity prefab = chaserPrefab;
                int mod = i % 4;
                if (mod == 1) prefab = bomberPrefab;
                else if (mod == 2) prefab = shooterPrefab;
                else if (mod == 3) prefab = (chargerPrefab != Entity.Null) ? chargerPrefab : tankPrefab;

                SpawnMegaEnemy(ref ecb, prefab, pos, hpMultiplier, 180f);
            }

            // 2 Super Elite Bosses
            SpawnMegaElite(ref ecb, tankPrefab, playerPos + new float3(11.5f, 0f, 0), 1600f * hpMultiplier, 1.6f);
            SpawnMegaElite(ref ecb, shooterPrefab, playerPos + new float3(-11.5f, 0f, 0), 1400f * hpMultiplier, 1.6f);

            wave.MegaWaveRemainingEnemies += count + 2;
            wave.MegaWaveTotalEnemies += count + 2;
        }
    }

    // =========================================================================
    // 2. NHỊP SINH QUÁI LIÊN TỤC TRONG SUỐT 1 PHÚT (MỖI 1.0s - 1.5s)
    // =========================================================================
    private static void SpawnMegaWaveContinuousPack(
        ref EntityCommandBuffer ecb,
        ref Unity.Mathematics.Random rng,
        ref WaveManagerData wave,
        float3 playerPos,
        int minute,
        float hpMultiplier,
        Entity chaserPrefab,
        Entity bomberPrefab,
        Entity shooterPrefab,
        Entity tankPrefab,
        Entity chargerPrefab)
    {
        int countToSpawn;
        float baseHp;
        if (minute == 3)
        {
            countToSpawn = ScaleSpawnCount(rng.NextInt(4, 7), ref rng);
            baseHp = 60f;
        }
        else if (minute == 6)
        {
            countToSpawn = ScaleSpawnCount(rng.NextInt(5, 8), ref rng);
            baseHp = 100f;
        }
        else
        {
            countToSpawn = ScaleSpawnCount(rng.NextInt(7, 11), ref rng);
            baseHp = 180f;
        }

        for (int i = 0; i < countToSpawn; i++)
        {
            float angle = rng.NextFloat(0f, math.PI * 2f);
            float radius = 11.5f + rng.NextFloat(-1.0f, 1.5f);
            float3 pos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);

            Entity chosenPrefab = chaserPrefab;
            if (minute == 3)
            {
                int r = rng.NextInt(0, 100);
                if (r < 55) chosenPrefab = chaserPrefab;
                else if (r < 80) chosenPrefab = bomberPrefab;
                else if (chargerPrefab != Entity.Null && r < 92) chosenPrefab = chargerPrefab;
                else chosenPrefab = tankPrefab;
            }
            else if (minute == 6)
            {
                int r = rng.NextInt(0, 100);
                if (r < 35) chosenPrefab = shooterPrefab;
                else if (r < 65) chosenPrefab = bomberPrefab;
                else if (chargerPrefab != Entity.Null && r < 85) chosenPrefab = chargerPrefab;
                else chosenPrefab = tankPrefab;
            }
            else // minute == 9
            {
                int r = rng.NextInt(0, 100);
                if (r < 30) chosenPrefab = chaserPrefab;
                else if (r < 55) chosenPrefab = shooterPrefab;
                else if (r < 75) chosenPrefab = bomberPrefab;
                else if (chargerPrefab != Entity.Null && r < 90) chosenPrefab = chargerPrefab;
                else chosenPrefab = tankPrefab;
            }

            SpawnMegaEnemy(ref ecb, chosenPrefab, pos, hpMultiplier, baseHp);
        }

        wave.MegaWaveRemainingEnemies += countToSpawn;
        wave.MegaWaveTotalEnemies += countToSpawn;
    }

    // =========================================================================
    // 3. ĐỢT ĐỘT KÍCH TĂNG CƯỜNG (SURGES TẠI GIÂY 20 VÀ GIÂY 40 CỦA ĐỢT)
    // =========================================================================
    private static void SpawnMegaWaveSurge(
        ref EntityCommandBuffer ecb,
        ref Unity.Mathematics.Random rng,
        ref WaveManagerData wave,
        float3 playerPos,
        int minute,
        int surgeIndex,
        float hpMultiplier,
        Entity chaserPrefab,
        Entity bomberPrefab,
        Entity shooterPrefab,
        Entity tankPrefab,
        Entity chargerPrefab)
    {
        if (minute == 3)
        {
            if (surgeIndex == 1)
            {
                // Giây 20: 10 Bombers & Chargers từ 2 hướng đối diện kẹp lại -> Giảm 10%
                int count = ScaleSpawnCount(10, ref rng);
                float baseDir = rng.NextFloat(0f, math.PI);
                for (int i = 0; i < count; i++)
                {
                    float dir = (i % 2 == 0) ? baseDir : baseDir + math.PI;
                    float angle = dir + rng.NextFloat(-0.3f, 0.3f);
                    float radius = 12f + rng.NextFloat(-0.5f, 0.5f);
                    float3 pos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);

                    Entity prefab = (chargerPrefab != Entity.Null && i % 3 == 0) ? chargerPrefab : bomberPrefab;
                    SpawnMegaEnemy(ref ecb, prefab, pos, hpMultiplier, 60f);
                }
                wave.MegaWaveRemainingEnemies += count;
                wave.MegaWaveTotalEnemies += count;
            }
            else // surgeIndex == 2 (tại giây 40)
            {
                // Giây 40: Vòng vây 12 Chasers + 1 Elite Tank -> Giảm 10%
                int count = ScaleSpawnCount(12, ref rng);
                for (int i = 0; i < count; i++)
                {
                    float angle = (i / (float)count) * math.PI * 2f + rng.NextFloat(-0.06f, 0.06f);
                    float radius = 11f + rng.NextFloat(-0.5f, 0.5f);
                    float3 pos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);
                    SpawnMegaEnemy(ref ecb, chaserPrefab, pos, hpMultiplier, 65f);
                }
                float3 elitePos = playerPos + new float3(0f, -11.5f, 0);
                SpawnMegaElite(ref ecb, tankPrefab, elitePos, 500f * hpMultiplier, 1.4f);

                wave.MegaWaveRemainingEnemies += count + 1;
                wave.MegaWaveTotalEnemies += count + 1;
            }
        }
        else if (minute == 6)
        {
            if (surgeIndex == 1)
            {
                // Giây 20: 12 Shooters bao vây bán nguyệt + 4 Chargers -> Giảm 10%
                int count = ScaleSpawnCount(12, ref rng);
                float centerAngle = rng.NextFloat(0f, math.PI * 2f);
                for (int i = 0; i < count; i++)
                {
                    float offset = (i / (float)count - 0.5f) * math.PI * 1.2f;
                    float angle = centerAngle + offset;
                    float radius = 12.5f + rng.NextFloat(-0.4f, 0.4f);
                    float3 pos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);
                    SpawnMegaEnemy(ref ecb, shooterPrefab, pos, hpMultiplier, 100f);
                }
                int chargerCount = ScaleSpawnCount(4, ref rng);
                for (int i = 0; i < chargerCount; i++)
                {
                    float angle = centerAngle + math.PI + rng.NextFloat(-0.4f, 0.4f);
                    float3 pos = playerPos + new float3(math.cos(angle) * 11.5f, math.sin(angle) * 11.5f, 0);
                    Entity p = (chargerPrefab != Entity.Null) ? chargerPrefab : bomberPrefab;
                    SpawnMegaEnemy(ref ecb, p, pos, hpMultiplier, 110f);
                }
                wave.MegaWaveRemainingEnemies += count + chargerCount;
                wave.MegaWaveTotalEnemies += count + chargerCount;
            }
            else // surgeIndex == 2 (tại giây 40)
            {
                // Giây 40: 14 Bombers + 2 Elite Commanders (1 Tank, 1 Shooter) -> Giảm 10%
                int count = ScaleSpawnCount(14, ref rng);
                for (int i = 0; i < count; i++)
                {
                    float angle = (i / (float)count) * math.PI * 2f + rng.NextFloat(-0.06f, 0.06f);
                    float radius = 10f + rng.NextFloat(-0.5f, 0.5f);
                    float3 pos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);
                    SpawnMegaEnemy(ref ecb, bomberPrefab, pos, hpMultiplier, 120f);
                }
                SpawnMegaElite(ref ecb, tankPrefab, playerPos + new float3(11f, 0, 0), 900f * hpMultiplier, 1.45f);
                SpawnMegaElite(ref ecb, shooterPrefab, playerPos + new float3(-11f, 0, 0), 750f * hpMultiplier, 1.45f);

                wave.MegaWaveRemainingEnemies += count + 2;
                wave.MegaWaveTotalEnemies += count + 2;
            }
        }
        else // minute == 9
        {
            if (surgeIndex == 1)
            {
                // Giây 20: 24 quái (Bombers + Chargers) lao nhanh từ mọi phía -> Giảm 10%
                int count = ScaleSpawnCount(24, ref rng);
                for (int i = 0; i < count; i++)
                {
                    float angle = (i / (float)count) * math.PI * 2f + rng.NextFloat(-0.05f, 0.05f);
                    float radius = 11.5f + rng.NextFloat(-0.8f, 0.8f);
                    float3 pos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);
                    Entity p = (i % 3 == 0 && chargerPrefab != Entity.Null) ? chargerPrefab : bomberPrefab;
                    SpawnMegaEnemy(ref ecb, p, pos, hpMultiplier, 180f);
                }
                wave.MegaWaveRemainingEnemies += count;
                wave.MegaWaveTotalEnemies += count;
            }
            else // surgeIndex == 2 (tại giây 40)
            {
                // Giây 40: 20 Shooters bao vây khép góc + 2 Super Elite Bosses (HP 2200, 1800) -> Giảm 10%
                int count = ScaleSpawnCount(20, ref rng);
                for (int i = 0; i < count; i++)
                {
                    float angle = (i / (float)count) * math.PI * 2f + rng.NextFloat(-0.05f, 0.05f);
                    float radius = 12.5f + rng.NextFloat(-0.4f, 0.4f);
                    float3 pos = playerPos + new float3(math.cos(angle) * radius, math.sin(angle) * radius, 0);
                    SpawnMegaEnemy(ref ecb, shooterPrefab, pos, hpMultiplier, 190f);
                }
                SpawnMegaElite(ref ecb, tankPrefab, playerPos + new float3(0, 11.5f, 0), 2200f * hpMultiplier, 1.6f);
                SpawnMegaElite(ref ecb, shooterPrefab, playerPos + new float3(0, -11.5f, 0), 1800f * hpMultiplier, 1.6f);

                wave.MegaWaveRemainingEnemies += count + 2;
                wave.MegaWaveTotalEnemies += count + 2;
            }
        }
    }

    private static void SpawnMegaEnemy(
        ref EntityCommandBuffer ecb,
        Entity prefab,
        float3 pos,
        float hpMultiplier,
        float baseHp)
    {
        Entity enemy = ecb.Instantiate(prefab);
        ecb.AddComponent<MegaWaveEnemyTag>(enemy);
        ecb.SetComponent(enemy, LocalTransform.FromPosition(pos));

        float scaledHp = math.round(baseHp * hpMultiplier);
        ecb.SetComponent(enemy, new HealthData { CurrentHP = scaledHp, MaxHP = scaledHp });
    }

    private static void SpawnMegaElite(
        ref EntityCommandBuffer ecb,
        Entity prefab,
        float3 pos,
        float eliteHp,
        float scale)
    {
        Entity enemy = ecb.Instantiate(prefab);
        ecb.AddComponent<MegaWaveEnemyTag>(enemy);
        ecb.AddComponent(enemy, new MegaWaveEliteTag { BaseScale = scale });
        ecb.SetComponent(enemy, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, scale));
        ecb.SetComponent(enemy, new HealthData { CurrentHP = eliteHp, MaxHP = eliteHp });
    }

    private static void SpawnVictoryRewards(ref EntityCommandBuffer ecb, float3 playerPos, int playerLevel)
    {
        // 1. Rương Cổ Vật (Ancient Treasure Chest)
        Entity chest = ecb.CreateEntity();
        ecb.AddComponent(chest, LocalTransform.FromPosition(playerPos + new float3(1.2f, 0.5f, 0f)));
        ecb.AddComponent(chest, new PickupItemTag());
        ecb.AddComponent(chest, new PickupData
        {
            Type = PickupType.TreasureChest,
            Value = 0f,
            Radius = 0.9f,
            FlySpeed = 0f
        });

        // 2. Nam Châm Hút EXP Toàn Bản Đồ (Exp Magnet)
        Entity magnet = ecb.CreateEntity();
        ecb.AddComponent(magnet, LocalTransform.FromPosition(playerPos + new float3(-1.2f, 0.5f, 0f)));
        ecb.AddComponent(magnet, new PickupItemTag());
        ecb.AddComponent(magnet, new PickupData
        {
            Type = PickupType.ExpMagnet,
            Value = 0f,
            Radius = 0.8f,
            FlySpeed = 0f
        });
    }
}
