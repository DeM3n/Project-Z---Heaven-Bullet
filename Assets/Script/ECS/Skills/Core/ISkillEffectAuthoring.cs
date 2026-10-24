using Unity.Entities;

// Hiệu ứng skill nào implement interface này sẽ được SkillAuthoring tự động gọi Bake().
// Thêm skill type mới = tạo class mới implement interface này, KHÔNG cần sửa SkillAuthoring.
public interface ISkillEffectAuthoring
{
    void Bake(IBaker baker, Entity entity);
}