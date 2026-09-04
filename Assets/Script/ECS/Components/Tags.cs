using Unity.Entities;

// Tag components — rỗng, dùng để lọc nhanh trong Query/Job (theo GDD mục 6.2)
public struct PlayerTag : IComponentData {}
public struct EnemyTag : IComponentData {}
public struct ProjectileTag : IComponentData {}
public struct EliteTag : IComponentData {}