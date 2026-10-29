using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class SlashSwordAuthoring : MonoBehaviour
{
    [Tooltip("Góc bù thị giác cho sprite kiếm (tính theo độ). Mặc định là 180 để mũi kiếm hướng đúng về phía trước. Nếu mũi kiếm hướng sang phải, đặt -90. Nếu để 0, hệ thống sẽ tự động lấy góc xoay Z hiện tại của GameObject/Prefab.")]
    public float visualRotationOffsetDegrees = 180f;

    class Baker : Baker<SlashSwordAuthoring>
    {
        public override void Bake(SlashSwordAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<SlashSwordTag>(entity);

            float offsetDeg = authoring.visualRotationOffsetDegrees;
            // Nếu trường offset chưa nhập mà GameObject lại được xoay trên Transform (ví dụ người dùng đã xoay Z 180 độ trên Prefab)
            if (Mathf.Abs(offsetDeg) < 0.001f && Mathf.Abs(authoring.transform.localEulerAngles.z) > 0.001f)
            {
                offsetDeg = authoring.transform.localEulerAngles.z;
            }

            AddComponent(entity, new SlashSwordVisualOffset
            {
                RadianOffset = Mathf.Deg2Rad * offsetDeg
            });

            AddComponent(entity, new SlashSwordData
            {
                SkillEntity = Entity.Null,
                State = 0,
                OrbitAngle = 0f,
                CooldownTimer = 0f,
                StateTimer = 0f,
                TargetPosition = float3.zero,
                TargetEnemy = Entity.Null,
                SlashDirection = new float3(1f, 0f, 0f),
                SwordIndex = 0,
                TotalSwords = 1,
                SlashProgress = 0f
            });
        }
    }
}
