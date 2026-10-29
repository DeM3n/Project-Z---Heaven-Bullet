using Unity.Entities;
using UnityEngine;

public class FlyingSwordAuthoring : MonoBehaviour
{
    [Tooltip("Góc bù thị giác cho sprite kiếm (tính theo độ). Mặc định là 180 để mũi kiếm hướng đúng về phía trước. Nếu mũi kiếm hướng sang phải, đặt -90. Nếu để 0, hệ thống sẽ tự động lấy góc xoay Z hiện tại của GameObject/Prefab.")]
    public float visualRotationOffsetDegrees = 180f;

    class Baker : Baker<FlyingSwordAuthoring>
    {
        public override void Bake(FlyingSwordAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<FlyingSwordTag>(entity);

            float offsetDeg = authoring.visualRotationOffsetDegrees;
            // Nếu trường offset chưa nhập mà GameObject lại được xoay trên Transform (ví dụ người dùng đã xoay Z 180 độ trên Prefab)
            if (Mathf.Abs(offsetDeg) < 0.001f && Mathf.Abs(authoring.transform.localEulerAngles.z) > 0.001f)
            {
                offsetDeg = authoring.transform.localEulerAngles.z;
            }

            AddComponent(entity, new FlyingSwordVisualOffset
            {
                RadianOffset = Mathf.Deg2Rad * offsetDeg
            });

            AddComponent(entity, new FlyingSwordData
            {
                State = 0,
                CurrentAngle = 0f,
                Timer = 0f,
                SwordIndex = 0,
                TotalSwords = 1,
                UltimateTimer = 0f,
                IsUltimate = false,
                LastTarget = Entity.Null,
                LoopDirection = 1
            });
            AddBuffer<SwordHitTargetBuffer>(entity);
        }
    }
}
