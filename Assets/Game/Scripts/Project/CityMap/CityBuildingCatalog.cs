using System.Collections.Generic;
using UnityEngine;

namespace Game.CityMap
{
    /// <summary>
    /// 按 footprint 分类的建筑/设施库。布局只放槽位，填充时从这里随机抽。
    /// </summary>
    [CreateAssetMenu(fileName = "CityBuildingCatalog", menuName = "Game/City Map/Building Catalog", order = 0)]
    public sealed class CityBuildingCatalog : ScriptableObject
    {
        public const string DefaultResourcesPath = "CityBuildingCatalog";

        public List<CityBuildingVariant> Buildings1x1 = new List<CityBuildingVariant>();
        public List<CityBuildingVariant> Buildings2x1 = new List<CityBuildingVariant>();
        public List<CityBuildingVariant> Buildings1x2 = new List<CityBuildingVariant>();
        public List<CityBuildingVariant> Buildings2x2 = new List<CityBuildingVariant>();
        public List<CityBuildingVariant> OpenFacilities = new List<CityBuildingVariant>();

        public static CityBuildingCatalog Load()
        {
            return Resources.Load<CityBuildingCatalog>(DefaultResourcesPath);
        }

        public IReadOnlyList<CityBuildingVariant> GetPool(CityBuildingFootprint footprint, bool openFacility)
        {
            if (openFacility) return OpenFacilities;
            switch (footprint)
            {
                case CityBuildingFootprint.B1x1: return Buildings1x1;
                case CityBuildingFootprint.B2x1: return Buildings2x1;
                case CityBuildingFootprint.B1x2: return Buildings1x2;
                case CityBuildingFootprint.B2x2: return Buildings2x2;
                default: return Buildings1x1;
            }
        }

        public CityBuildingVariant Pick(CityBuildingFootprint footprint, bool openFacility, System.Random rng, string forceTypeId = null)
        {
            var pool = GetPool(footprint, openFacility);
            if (pool == null || pool.Count == 0) return null;

            if (!string.IsNullOrEmpty(forceTypeId))
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != null && pool[i].TypeId == forceTypeId)
                        return pool[i];
                }
            }

            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null) total += Mathf.Max(0.01f, pool[i].Weight);
            }
            if (total <= 0f) return pool[0];

            float roll = (float)(rng.NextDouble() * total);
            float acc = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                var v = pool[i];
                if (v == null) continue;
                acc += Mathf.Max(0.01f, v.Weight);
                if (roll <= acc) return v;
            }
            return pool[pool.Count - 1];
        }
    }
}
