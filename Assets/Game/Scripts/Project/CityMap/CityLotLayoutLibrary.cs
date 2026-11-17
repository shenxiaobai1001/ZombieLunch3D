using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.CityMap
{
    [Serializable]
    public sealed class CityLotLayoutEntry
    {
        public CityLotSize LotSize;
        public GameObject LayoutPrefab;
        [Range(0.01f, 10f)] public float Weight = 1f;
    }

    /// <summary>
    /// 各地块尺寸可用的布局预制体池（槽位布局，建筑仍由 Catalog 随机）。
    /// </summary>
    [CreateAssetMenu(fileName = "CityLotLayoutLibrary", menuName = "Game/City Map/Lot Layout Library", order = 1)]
    public sealed class CityLotLayoutLibrary : ScriptableObject
    {
        public const string DefaultResourcesPath = "CityLotLayoutLibrary";

        public List<CityLotLayoutEntry> Entries = new List<CityLotLayoutEntry>();

        [Header("生成权重（越大越容易出现）")]
        public float Weight1x1 = 1.2f;
        public float Weight2x1 = 1.0f;
        public float Weight1x2 = 1.0f;
        public float Weight2x2 = 0.8f;

        public static CityLotLayoutLibrary Load()
        {
            return Resources.Load<CityLotLayoutLibrary>(DefaultResourcesPath);
        }

        public float GetSizeWeight(CityLotSize size)
        {
            switch (size)
            {
                case CityLotSize.Lot1x1: return Weight1x1;
                case CityLotSize.Lot2x1: return Weight2x1;
                case CityLotSize.Lot1x2: return Weight1x2;
                case CityLotSize.Lot2x2: return Weight2x2;
                default: return 1f;
            }
        }

        public GameObject PickLayout(CityLotSize size, System.Random rng)
        {
            float total = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e == null || e.LayoutPrefab == null || e.LotSize != size) continue;
                total += Mathf.Max(0.01f, e.Weight);
            }
            if (total <= 0f) return null;

            float roll = (float)(rng.NextDouble() * total);
            float acc = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e == null || e.LayoutPrefab == null || e.LotSize != size) continue;
                acc += Mathf.Max(0.01f, e.Weight);
                if (roll <= acc) return e.LayoutPrefab;
            }
            return null;
        }
    }
}
