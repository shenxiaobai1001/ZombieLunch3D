using UnityEngine;

namespace Game.CityMap
{
    /// <summary>
    /// 地块布局上的建筑槽位：只规定 footprint，具体建筑类型运行时从 Catalog 随机抽取。
    /// </summary>
    public sealed class CityBuildingSlot : MonoBehaviour
    {
        [Tooltip("此槽需要的建筑占地")]
        public CityBuildingFootprint RequiredFootprint = CityBuildingFootprint.B1x1;

        [Tooltip("为 true 时填开放设施库，而不是建筑库")]
        public bool IsOpenFacility;

        [Tooltip("可选：覆盖随机，指定 Catalog 中的 TypeId；空则随机")]
        public string ForceTypeId;

        /// <summary>槽位设计尺寸（本地），用于预览框与对齐。</summary>
        public Vector3 SlotSize = Vector3.one;
    }
}
