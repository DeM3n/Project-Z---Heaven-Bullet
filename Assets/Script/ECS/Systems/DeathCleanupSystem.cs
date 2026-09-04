using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public partial struct DeathCleanupSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (hp, entity) in
                 SystemAPI.Query<RefRO<HealthData>>().WithAll<EnemyTag>().WithEntityAccess())
        {
            if (hp.ValueRO.CurrentHP <= 0f)
            {
                ecb.DestroyEntity(entity); // destroy ngay ở prototype; deferred/rải theo frame (GDD 11.5) để dành khi có mass-death spike
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}