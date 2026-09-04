using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public partial struct ProjectileLifetimeSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        new ProjectileLifetimeJob
        {
            DeltaTime = dt,
            Ecb = ecb.AsParallelWriter()
        }.ScheduleParallel();

        state.Dependency.Complete();
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

[BurstCompile]
public partial struct ProjectileLifetimeJob : IJobEntity
{
    public float DeltaTime;
    public EntityCommandBuffer.ParallelWriter Ecb;

    void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ref ProjectileLifetimeData lifetime)
    {
        lifetime.TimeRemaining -= DeltaTime;
        if (lifetime.TimeRemaining <= 0f)
        {
            Ecb.DestroyEntity(chunkIndex, entity);
        }
    }
}