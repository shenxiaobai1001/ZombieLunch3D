using UnityEngine;

namespace Game.CityMap
{
    /// <summary>已生成地块的运行时记录。</summary>
    public sealed class CityLotInstance : MonoBehaviour
    {
        public CityLotSize LotSize;
        public int GridX;
        public int GridZ;
        public int CellsX = 1;
        public int CellsZ = 1;

        public bool ContainsCell(int cx, int cz)
        {
            return cx >= GridX && cx < GridX + CellsX
                   && cz >= GridZ && cz < GridZ + CellsZ;
        }

        public bool ContainsWorldPoint(Vector3 world)
        {
            var origin = CityMapMetrics.CellOrigin(GridX, GridZ);
            var size = CityMapMetrics.LotWorldSize(CellsX, CellsZ);
            return world.x >= origin.x && world.x < origin.x + size.x
                   && world.z >= origin.z && world.z < origin.z + size.z;
        }
    }
}
