using UnityEngine;

namespace Game.CityMap
{
    /// <summary>挂在建筑/设施预制体根上，声明自己的占地与类型 ID。</summary>
    public sealed class CityBuildingVariant : MonoBehaviour
    {
        public CityBuildingFootprint Footprint = CityBuildingFootprint.B1x1;

        [Tooltip("类型唯一名，如 House / Shop / StripMall")]
        public string TypeId = "Default";

        [Tooltip("开放设施（停车场、广场等），不进建筑随机池")]
        public bool IsOpenFacility;

        [Range(0.01f, 10f)]
        public float Weight = 1f;
    }
}
