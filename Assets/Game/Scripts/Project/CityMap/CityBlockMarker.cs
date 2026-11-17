using UnityEngine;

namespace Game.CityMap
{
    /// <summary>挂在占位零件/布局根上，方便识别。</summary>
    public sealed class CityBlockMarker : MonoBehaviour
    {
        public CityBlockKind Kind;
        public CityLotSize LotSize;
        public CityBuildingFootprint BuildingFootprint;
        public CityLot2x2Layout Layout2x2;
        public string LayoutId;
    }
}
