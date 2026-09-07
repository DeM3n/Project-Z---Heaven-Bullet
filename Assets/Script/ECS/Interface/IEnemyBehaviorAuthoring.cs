using Unity.Entities;
 
// Behavior Authoring nào implement interface này sẽ được EnemyAuthoring.Baker tự động gọi Bake().
public interface IEnemyBehaviorAuthoring
{
    void Bake(IBaker baker, Entity entity);
}
 
















