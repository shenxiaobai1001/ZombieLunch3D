using UnityEngine;

namespace Game.CityMap
{
    /// <summary>
    /// 城市比例尺：设计单位 1 = 25 米（相对角色整体缩小一半）。
    /// 地块之间隔马路；多格地块内部合并路缝为整块底板；地块内建筑用过道。
    /// </summary>
    public static class CityMapMetrics
    {
        public const float UnitMeters = 25f;
        public const float RoadWidth = 6f;
        public const float CorridorWidth = 3f;
        public const float LotPadHeight = 0.2f;
        public const float RoadHeight = 0.12f;
        public const float CorridorHeight = 0.15f;
        public const float FacilityHeight = 0.75f;
        public const float BuildingBaseHeight = 6f;

        /// <summary>相邻 1x1 地块原点间距 = 地块宽 + 马路宽。</summary>
        public static float CellPitch => UnitMeters + RoadWidth;

        public static Vector2Int LotCells(CityLotSize size)
        {
            switch (size)
            {
                case CityLotSize.Lot1x1: return new Vector2Int(1, 1);
                case CityLotSize.Lot2x1: return new Vector2Int(2, 1);
                case CityLotSize.Lot1x2: return new Vector2Int(1, 2);
                case CityLotSize.Lot2x2: return new Vector2Int(2, 2);
                default: return new Vector2Int(1, 1);
            }
        }

        public static CityLotSize LotSizeFromCells(int w, int h)
        {
            if (w == 2 && h == 2) return CityLotSize.Lot2x2;
            if (w == 2 && h == 1) return CityLotSize.Lot2x1;
            if (w == 1 && h == 2) return CityLotSize.Lot1x2;
            return CityLotSize.Lot1x1;
        }

        /// <summary>地块世界尺寸（多格时内部路缝并入地块）。</summary>
        public static Vector3 LotWorldSize(int cellsX, int cellsZ)
        {
            float x = cellsX * UnitMeters + Mathf.Max(0, cellsX - 1) * RoadWidth;
            float z = cellsZ * UnitMeters + Mathf.Max(0, cellsZ - 1) * RoadWidth;
            return new Vector3(x, LotPadHeight, z);
        }

        public static Vector3 LotSize(CityLotSize size)
        {
            var c = LotCells(size);
            return LotWorldSize(c.x, c.y);
        }

        /// <summary>格子 (gx,gz) 的 SW 角世界坐标。</summary>
        public static Vector3 CellOrigin(int gx, int gz)
        {
            return new Vector3(gx * CellPitch, 0f, gz * CellPitch);
        }

        /// <summary>地块中心世界坐标。</summary>
        public static Vector3 LotCenter(int gx, int gz, int cellsX, int cellsZ)
        {
            var origin = CellOrigin(gx, gz);
            var size = LotWorldSize(cellsX, cellsZ);
            return origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        }

        public static Vector2Int WorldToCell(Vector3 world)
        {
            return new Vector2Int(
                Mathf.FloorToInt(world.x / CellPitch),
                Mathf.FloorToInt(world.z / CellPitch));
        }

        /// <summary>玩家在某地块内的归一化进度 (0~1)。</summary>
        public static Vector2 LotLocal01(Vector3 world, int gx, int gz, int cellsX, int cellsZ)
        {
            var origin = CellOrigin(gx, gz);
            var size = LotWorldSize(cellsX, cellsZ);
            float lx = size.x > 0.01f ? (world.x - origin.x) / size.x : 0f;
            float lz = size.z > 0.01f ? (world.z - origin.z) / size.z : 0f;
            return new Vector2(Mathf.Clamp01(lx), Mathf.Clamp01(lz));
        }

        public static Vector3 BuildingSize(CityBuildingFootprint footprint)
        {
            float h = BuildingBaseHeight;
            switch (footprint)
            {
                case CityBuildingFootprint.B1x1:
                    return new Vector3(UnitMeters - CorridorWidth, h, UnitMeters - CorridorWidth);
                case CityBuildingFootprint.B2x1:
                    return new Vector3(UnitMeters * 2f - CorridorWidth, h * 1.25f, UnitMeters - CorridorWidth);
                case CityBuildingFootprint.B1x2:
                    return new Vector3(UnitMeters - CorridorWidth, h * 1.25f, UnitMeters * 2f - CorridorWidth);
                case CityBuildingFootprint.B2x2:
                    return new Vector3(UnitMeters * 2f - CorridorWidth, h * 2f, UnitMeters * 2f - CorridorWidth);
                default:
                    return new Vector3(UnitMeters - CorridorWidth, h, UnitMeters - CorridorWidth);
            }
        }

        public static Vector3 TwinBuildingSizeOn2x1()
        {
            float usable = UnitMeters * 2f - CorridorWidth;
            float each = (usable - CorridorWidth) * 0.5f;
            return new Vector3(each, BuildingBaseHeight, UnitMeters - CorridorWidth);
        }

        public static Vector3 TwinBuildingSizeOn1x2()
        {
            float usable = UnitMeters * 2f - CorridorWidth;
            float each = (usable - CorridorWidth) * 0.5f;
            return new Vector3(UnitMeters - CorridorWidth, BuildingBaseHeight, each);
        }
    }

    public enum CityLotSize
    {
        Lot1x1 = 0,
        Lot2x1 = 1,
        Lot1x2 = 2,
        Lot2x2 = 3,
    }

    public enum CityBuildingFootprint
    {
        B1x1 = 0,
        B2x1 = 1,
        B1x2 = 2,
        B2x2 = 3,
    }

    public enum CityLot2x2Layout
    {
        Four1x1 = 0,
        Three1x1PlusFacility = 1,
        Two2x1 = 2,
        Two1x2 = 3,
        OneLarge2x2 = 4,
    }

    public enum CityBlockKind
    {
        LotPad = 0,
        Road = 1,
        Corridor = 2,
        Building = 3,
        OpenFacility = 4,
    }
}
