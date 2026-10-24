using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct SpatialGridSingleton : IComponentData
{
    public NativeParallelMultiHashMap<int, Entity> Grid;
    public float CellSize; // TODO: kéo ra config asset thay vì hardcode (GDD 11.7 - entity budget configurable)
}

public static class SpatialGridUtility
{
    public static int2 ToCell(float2 posXY, float cellSize) =>
        (int2)math.floor(posXY / cellSize);

    public static int HashCell(int2 cell) =>
        cell.x * 92837111 ^ cell.y * 689287499;
}

[BurstCompile]
public partial struct SpatialGridBuildSystem : ISystem
{
    private EntityQuery _enemyQuery;

    public void OnCreate(ref SystemState state)
    {
        _enemyQuery = SystemAPI.QueryBuilder()
            .WithAll<EnemyTag, LocalTransform>()
            .Build();

        Entity singletonEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(singletonEntity, new SpatialGridSingleton
        {
            Grid = new NativeParallelMultiHashMap<int, Entity>(1024, Allocator.Persistent),
            CellSize = 2f
        });
    }

    public void OnDestroy(ref SystemState state)
    {
        SystemAPI.GetSingleton<SpatialGridSingleton>().Grid.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        var singletonRW = SystemAPI.GetSingletonRW<SpatialGridSingleton>();
        int enemyCount = _enemyQuery.CalculateEntityCount();

        if (singletonRW.ValueRO.Grid.Capacity < enemyCount)
            singletonRW.ValueRW.Grid.Capacity = enemyCount * 2;
        singletonRW.ValueRW.Grid.Clear();

        state.Dependency = new BuildSpatialGridJob
        {
            CellSize = singletonRW.ValueRO.CellSize,
            GridWriter = singletonRW.ValueRW.Grid.AsParallelWriter()
        }.ScheduleParallel(_enemyQuery, state.Dependency);
        state.Dependency.Complete();
    }
}

[BurstCompile]
public partial struct BuildSpatialGridJob : IJobEntity
{
    public float CellSize;
    public NativeParallelMultiHashMap<int, Entity>.ParallelWriter GridWriter;

    void Execute(in LocalTransform transform, in EnemyTag tag, Entity entity)
    {
        int hash = SpatialGridUtility.HashCell(SpatialGridUtility.ToCell(transform.Position.xy, CellSize));
        GridWriter.Add(hash, entity);
    }
}