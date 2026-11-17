using System.Collections.Generic;
using UnityEngine;

namespace Game.CityMap
{
    /// <summary>
    /// 地块管理器：随玩家移动灵活拼接多种地块（1x1/2x1/1x2/2x2），地块之间自动铺路。
    /// 预生成：玩家在当前地块上朝某方向走过约 1/3 时，提前在该方向生成新地块。
    /// </summary>
    public sealed class CityLotManager : MonoBehaviour
    {
        [Header("引用")]
        public Transform Player;
        public CityLotLayoutLibrary LayoutLibrary;
        public CityBuildingCatalog BuildingCatalog;
        public Material LotPadMaterial;
        public Material RoadMaterial;

        [Header("生成")]
        public int WorldSeed = 12345;
        [Tooltip("初始在玩家周围预填的格子半径")]
        public int InitialRadiusCells = 2;
        [Tooltip("常态维持的缓冲半径（格子）")]
        public int KeepRadiusCells = 3;
        [Tooltip("走过地块的多少比例时触发该方向预生成")]
        [Range(0.05f, 0.9f)]
        public float PregenFraction = 1f / 3f;
        [Tooltip("预生成时沿运动方向多看几格")]
        public int PregenAheadCells = 2;
        [Tooltip("超出此半径的地块卸载")]
        public int UnloadRadiusCells = 6;

        [Header("尺寸权重覆盖（小于 0 则用 Library）")]
        public float OverrideWeight1x1 = -1f;
        public float OverrideWeight2x1 = -1f;
        public float OverrideWeight1x2 = -1f;
        public float OverrideWeight2x2 = -1f;

        readonly Dictionary<long, CityLotInstance> _cellToLot = new Dictionary<long, CityLotInstance>();
        readonly Dictionary<long, GameObject> _roads = new Dictionary<long, GameObject>();
        readonly List<CityLotInstance> _lots = new List<CityLotInstance>();
        readonly List<CityLotSize> _sizeScratch = new List<CityLotSize>(4);

        Vector3 _lastPlayerPos;
        Transform _lotsRoot;
        Transform _roadsRoot;

        static long CellKey(int x, int z) => ((long)x << 32) ^ (uint)z;
        static long RoadKey(int x, int z, bool east) => (((long)x << 32) ^ (uint)z) ^ (east ? 1L << 62 : 1L << 61);

        void Awake()
        {
            if (LayoutLibrary == null) LayoutLibrary = CityLotLayoutLibrary.Load();
            if (BuildingCatalog == null) BuildingCatalog = CityBuildingCatalog.Load();

            _lotsRoot = new GameObject("Lots").transform;
            _lotsRoot.SetParent(transform, false);
            _roadsRoot = new GameObject("Roads").transform;
            _roadsRoot.SetParent(transform, false);

            if (LotPadMaterial == null)
                LotPadMaterial = CreateRuntimeMat(new Color(0.75f, 0.72f, 0.65f));
            if (RoadMaterial == null)
                RoadMaterial = CreateRuntimeMat(new Color(0.22f, 0.22f, 0.24f));
        }

        void Start()
        {
            ResolvePlayer();
            if (Player != null)
            {
                _lastPlayerPos = Player.position;
                BootstrapAroundPlayer();
            }
        }

        void Update()
        {
            ResolvePlayer();
            if (Player == null) return;

            var pos = Player.position;
            var delta = pos - _lastPlayerPos;
            UpdateStreaming(pos, delta);
            _lastPlayerPos = pos;
        }

        void ResolvePlayer()
        {
            if (Player != null) return;
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) Player = tagged.transform;
        }

        void BootstrapAroundPlayer()
        {
            var cell = CityMapMetrics.WorldToCell(Player.position);
            EnsureRectFilled(cell.x - InitialRadiusCells, cell.x + InitialRadiusCells,
                cell.y - InitialRadiusCells, cell.y + InitialRadiusCells);
            // 初始外围也铺一圈路，避免边缘缺路
            EnsurePerimeterRoads(cell.x - InitialRadiusCells, cell.x + InitialRadiusCells,
                cell.y - InitialRadiusCells, cell.y + InitialRadiusCells);
        }

        void UpdateStreaming(Vector3 pos, Vector3 delta)
        {
            var cell = CityMapMetrics.WorldToCell(pos);

            // 常驻缓冲
            EnsureRectFilled(cell.x - KeepRadiusCells, cell.x + KeepRadiusCells,
                cell.y - KeepRadiusCells, cell.y + KeepRadiusCells);

            // 1/3 预生成
            var lot = FindLotAtWorld(pos);
            if (lot != null)
            {
                var local = CityMapMetrics.LotLocal01(pos, lot.GridX, lot.GridZ, lot.CellsX, lot.CellsZ);
                int ahead = PregenAheadCells;

                if (delta.x > 0.01f && local.x >= PregenFraction)
                {
                    int start = lot.GridX + lot.CellsX;
                    EnsureRectFilled(start, start + ahead, lot.GridZ - 1, lot.GridZ + lot.CellsZ);
                }
                else if (delta.x < -0.01f && local.x <= 1f - PregenFraction)
                {
                    int end = lot.GridX - 1;
                    EnsureRectFilled(end - ahead, end, lot.GridZ - 1, lot.GridZ + lot.CellsZ);
                }

                if (delta.z > 0.01f && local.y >= PregenFraction)
                {
                    int start = lot.GridZ + lot.CellsZ;
                    EnsureRectFilled(lot.GridX - 1, lot.GridX + lot.CellsX, start, start + ahead);
                }
                else if (delta.z < -0.01f && local.y <= 1f - PregenFraction)
                {
                    int end = lot.GridZ - 1;
                    EnsureRectFilled(lot.GridX - 1, lot.GridX + lot.CellsX, end - ahead, end);
                }
            }
            else
            {
                // 人在路上：按位移方向预生成
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.z))
                {
                    int dir = delta.x >= 0f ? 1 : -1;
                    EnsureRectFilled(cell.x + dir, cell.x + dir * (1 + PregenAheadCells), cell.y - 1, cell.y + 1);
                }
                else if (Mathf.Abs(delta.z) > 0.01f)
                {
                    int dir = delta.z >= 0f ? 1 : -1;
                    EnsureRectFilled(cell.x - 1, cell.x + 1, cell.y + dir, cell.y + dir * (1 + PregenAheadCells));
                }
            }

            UnloadFarLots(cell);
        }

        CityLotInstance FindLotAtWorld(Vector3 world)
        {
            var cell = CityMapMetrics.WorldToCell(world);
            if (_cellToLot.TryGetValue(CellKey(cell.x, cell.y), out var lot) && lot != null)
                return lot;

            // 多格地块中心可能落在合并区域，再扫一遍
            for (int i = 0; i < _lots.Count; i++)
            {
                if (_lots[i] != null && _lots[i].ContainsWorldPoint(world))
                    return _lots[i];
            }
            return null;
        }

        void EnsureRectFilled(int minX, int maxX, int minZ, int maxZ)
        {
            if (minX > maxX) { int t = minX; minX = maxX; maxX = t; }
            if (minZ > maxZ) { int t = minZ; minZ = maxZ; maxZ = t; }

            // 扫描空格子，尝试放下尽量多样的地块
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                if (_cellToLot.ContainsKey(CellKey(x, z))) continue;
                TryPlaceLotAt(x, z);
            }
        }

        bool TryPlaceLotAt(int gx, int gz)
        {
            if (_cellToLot.ContainsKey(CellKey(gx, gz))) return false;

            var rng = RngForCell(gx, gz);
            BuildSizeCandidates(rng);

            for (int i = 0; i < _sizeScratch.Count; i++)
            {
                var size = _sizeScratch[i];
                var cells = CityMapMetrics.LotCells(size);
                if (!CanPlace(gx, gz, cells.x, cells.y)) continue;
                SpawnLot(gx, gz, cells.x, cells.y, size, rng);
                return true;
            }

            // 兜底 1x1
            if (CanPlace(gx, gz, 1, 1))
            {
                SpawnLot(gx, gz, 1, 1, CityLotSize.Lot1x1, rng);
                return true;
            }
            return false;
        }

        void BuildSizeCandidates(System.Random rng)
        {
            _sizeScratch.Clear();
            // 按 weight * random 排序，权重大更容易排前，但仍可能各种尺寸交错出现
            var scored = new List<(CityLotSize size, float score)>(4);
            scored.Add((CityLotSize.Lot2x2, SizeWeight(CityLotSize.Lot2x2) * (0.25f + (float)rng.NextDouble())));
            scored.Add((CityLotSize.Lot2x1, SizeWeight(CityLotSize.Lot2x1) * (0.25f + (float)rng.NextDouble())));
            scored.Add((CityLotSize.Lot1x2, SizeWeight(CityLotSize.Lot1x2) * (0.25f + (float)rng.NextDouble())));
            scored.Add((CityLotSize.Lot1x1, SizeWeight(CityLotSize.Lot1x1) * (0.25f + (float)rng.NextDouble())));
            scored.Sort((a, b) => b.score.CompareTo(a.score));
            for (int i = 0; i < scored.Count; i++)
                _sizeScratch.Add(scored[i].size);
        }

        float SizeWeight(CityLotSize size)
        {
            float o = -1f;
            switch (size)
            {
                case CityLotSize.Lot1x1: o = OverrideWeight1x1; break;
                case CityLotSize.Lot2x1: o = OverrideWeight2x1; break;
                case CityLotSize.Lot1x2: o = OverrideWeight1x2; break;
                case CityLotSize.Lot2x2: o = OverrideWeight2x2; break;
            }
            if (o >= 0f) return o;
            return LayoutLibrary != null ? LayoutLibrary.GetSizeWeight(size) : 1f;
        }

        bool CanPlace(int gx, int gz, int w, int h)
        {
            for (int z = gz; z < gz + h; z++)
            for (int x = gx; x < gx + w; x++)
            {
                if (_cellToLot.ContainsKey(CellKey(x, z))) return false;
            }
            return true;
        }

        void SpawnLot(int gx, int gz, int w, int h, CityLotSize size, System.Random rng)
        {
            var center = CityMapMetrics.LotCenter(gx, gz, w, h);
            GameObject root;

            var layoutPrefab = LayoutLibrary != null ? LayoutLibrary.PickLayout(size, rng) : null;
            if (layoutPrefab != null)
            {
                root = Instantiate(layoutPrefab, center, Quaternion.identity, _lotsRoot);
                root.name = $"Lot_{size}_{gx}_{gz}";
            }
            else
            {
                root = BuildFallbackLot(size, center);
                root.name = $"LotFallback_{size}_{gx}_{gz}";
            }

            root.transform.position = center;

            var inst = root.GetComponent<CityLotInstance>();
            if (inst == null) inst = root.AddComponent<CityLotInstance>();
            inst.LotSize = size;
            inst.GridX = gx;
            inst.GridZ = gz;
            inst.CellsX = w;
            inst.CellsZ = h;

            var filler = root.GetComponent<CityLotFiller>();
            if (filler != null)
            {
                filler.Catalog = BuildingCatalog;
                filler.Seed = Hash(WorldSeed, gx, gz, 11);
                filler.FillOnAwake = false;
                filler.Fill();
            }

            // 先填建筑再整体缩放，避免槽位本地尺寸错乱
            FitLayoutScale(root, w, h);
            EnsureLotPadCollider(root, w, h);

            for (int z = gz; z < gz + h; z++)
            for (int x = gx; x < gx + w; x++)
                _cellToLot[CellKey(x, z)] = inst;

            _lots.Add(inst);

            // 该地块东/北侧马路（避免重复）
            SpawnOwnedRoads(inst);
            // 若西/南侧尚无邻居，补外围路，保证可走
            SpawnMissingPerimeterRoads(inst);
        }

        static void FitLayoutScale(GameObject root, int w, int h)
        {
            // 旧布局按 w*U×h*U 做；流式打包多格地块为 w*U+(w-1)*R，这里统一缩放到世界尺寸
            var target = CityMapMetrics.LotWorldSize(w, h);
            float designX = w * CityMapMetrics.UnitMeters;
            float designZ = h * CityMapMetrics.UnitMeters;
            if (designX < 0.01f || designZ < 0.01f) return;
            var s = root.transform.localScale;
            root.transform.localScale = new Vector3(
                s.x * (target.x / designX),
                s.y,
                s.z * (target.z / designZ));
        }

        GameObject BuildFallbackLot(CityLotSize size, Vector3 center)
        {
            var go = new GameObject("FallbackLot");
            go.transform.SetParent(_lotsRoot, false);
            go.transform.position = center;

            var cells = CityMapMetrics.LotCells(size);
            var padSize = CityMapMetrics.LotWorldSize(cells.x, cells.y);
            CreateGroundCube(go.transform, "LotPad", padSize, LotPadMaterial,
                new Vector3(0f, padSize.y * 0.5f, 0f), CityBlockKind.LotPad);

            // 简单中心槽
            var slotGo = new GameObject("Slot_0");
            slotGo.transform.SetParent(go.transform, false);
            slotGo.transform.localPosition = new Vector3(0f, CityMapMetrics.LotPadHeight, 0f);
            var slot = slotGo.AddComponent<CityBuildingSlot>();
            slot.RequiredFootprint = size == CityLotSize.Lot2x2 ? CityBuildingFootprint.B2x2
                : size == CityLotSize.Lot2x1 ? CityBuildingFootprint.B2x1
                : size == CityLotSize.Lot1x2 ? CityBuildingFootprint.B1x2
                : CityBuildingFootprint.B1x1;
            slot.SlotSize = CityMapMetrics.BuildingSize(slot.RequiredFootprint);

            var filler = go.AddComponent<CityLotFiller>();
            filler.Catalog = BuildingCatalog;
            filler.FillOnAwake = false;
            return go;
        }

        void EnsureLotPadCollider(GameObject root, int w, int h)
        {
            // 若布局底板没有碰撞，补一个贴地 BoxCollider
            var markers = root.GetComponentsInChildren<CityBlockMarker>(true);
            bool hasPadCollider = false;
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i].Kind != CityBlockKind.LotPad) continue;
                var col = markers[i].GetComponent<Collider>();
                if (col == null)
                {
                    var box = markers[i].gameObject.AddComponent<BoxCollider>();
                    box.size = Vector3.one;
                    box.center = Vector3.zero;
                }
                hasPadCollider = true;
            }

            if (!hasPadCollider)
            {
                var size = CityMapMetrics.LotWorldSize(w, h);
                var colGo = new GameObject("LotGroundCollider");
                colGo.transform.SetParent(root.transform, false);
                colGo.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
                var box = colGo.AddComponent<BoxCollider>();
                box.size = size;
            }
        }

        void SpawnOwnedRoads(CityLotInstance lot)
        {
            // 东侧竖向路：覆盖地块整个 Z 向，并延伸到与北路交汇
            float road = CityMapMetrics.RoadWidth;
            var lotSize = CityMapMetrics.LotWorldSize(lot.CellsX, lot.CellsZ);
            var origin = CityMapMetrics.CellOrigin(lot.GridX, lot.GridZ);

            // East road (vertical strip)
            int eastX = lot.GridX + lot.CellsX - 1;
            for (int z = lot.GridZ; z < lot.GridZ + lot.CellsZ; z++)
                EnsureRoadSegment(eastX, z, east: true, lengthCellsAlongEdge: 1);

            // 额外：东侧整段连续马路（视觉合并）
            {
                long key = RoadKey(lot.GridX + lot.CellsX, lot.GridZ, true) ^ (1L << 60);
                if (!_roads.ContainsKey(key))
                {
                    float x = origin.x + lotSize.x + road * 0.5f;
                    float z = origin.z + lotSize.z * 0.5f;
                    var size = new Vector3(road, CityMapMetrics.RoadHeight, lotSize.z);
                    _roads[key] = CreateRoad($"Road_E_{lot.GridX}_{lot.GridZ}", size, new Vector3(x, CityMapMetrics.RoadHeight * 0.5f, z));
                }
            }

            // North road (horizontal strip)
            {
                long key = RoadKey(lot.GridX, lot.GridZ + lot.CellsZ, false) ^ (1L << 59);
                if (!_roads.ContainsKey(key))
                {
                    float x = origin.x + lotSize.x * 0.5f;
                    float z = origin.z + lotSize.z + road * 0.5f;
                    var size = new Vector3(lotSize.x, CityMapMetrics.RoadHeight, road);
                    _roads[key] = CreateRoad($"Road_N_{lot.GridX}_{lot.GridZ}", size, new Vector3(x, CityMapMetrics.RoadHeight * 0.5f, z));
                }
            }

            // NE corner crossing
            {
                long key = RoadKey(lot.GridX + lot.CellsX, lot.GridZ + lot.CellsZ, true) ^ (1L << 58);
                if (!_roads.ContainsKey(key))
                {
                    float x = origin.x + lotSize.x + road * 0.5f;
                    float z = origin.z + lotSize.z + road * 0.5f;
                    var size = new Vector3(road, CityMapMetrics.RoadHeight, road);
                    _roads[key] = CreateRoad($"Road_NE_{lot.GridX}_{lot.GridZ}", size, new Vector3(x, CityMapMetrics.RoadHeight * 0.5f, z));
                }
            }
        }

        void EnsureRoadSegment(int cellX, int cellZ, bool east, int lengthCellsAlongEdge)
        {
            // 保留接口；当前用整段 SpawnOwnedRoads 合并生成
        }

        void SpawnMissingPerimeterRoads(CityLotInstance lot)
        {
            float road = CityMapMetrics.RoadWidth;
            var lotSize = CityMapMetrics.LotWorldSize(lot.CellsX, lot.CellsZ);
            var origin = CityMapMetrics.CellOrigin(lot.GridX, lot.GridZ);

            // West：西侧无邻居地块时补路
            bool hasWest = false;
            for (int z = lot.GridZ; z < lot.GridZ + lot.CellsZ; z++)
            {
                if (_cellToLot.ContainsKey(CellKey(lot.GridX - 1, z))) { hasWest = true; break; }
            }
            if (!hasWest)
            {
                long key = RoadKey(lot.GridX, lot.GridZ, false) ^ (1L << 57);
                if (!_roads.ContainsKey(key))
                {
                    float x = origin.x - road * 0.5f;
                    float z = origin.z + lotSize.z * 0.5f;
                    var size = new Vector3(road, CityMapMetrics.RoadHeight, lotSize.z);
                    _roads[key] = CreateRoad($"Road_W_{lot.GridX}_{lot.GridZ}", size, new Vector3(x, CityMapMetrics.RoadHeight * 0.5f, z));
                }
            }

            // South
            bool hasSouth = false;
            for (int x = lot.GridX; x < lot.GridX + lot.CellsX; x++)
            {
                if (_cellToLot.ContainsKey(CellKey(x, lot.GridZ - 1))) { hasSouth = true; break; }
            }
            if (!hasSouth)
            {
                long key = RoadKey(lot.GridX, lot.GridZ, true) ^ (1L << 56);
                if (!_roads.ContainsKey(key))
                {
                    float x = origin.x + lotSize.x * 0.5f;
                    float z = origin.z - road * 0.5f;
                    var size = new Vector3(lotSize.x, CityMapMetrics.RoadHeight, road);
                    _roads[key] = CreateRoad($"Road_S_{lot.GridX}_{lot.GridZ}", size, new Vector3(x, CityMapMetrics.RoadHeight * 0.5f, z));
                }
            }
        }

        void EnsurePerimeterRoads(int minX, int maxX, int minZ, int maxZ)
        {
            // Bootstrap 后道路已由各 lot 自己补齐
        }

        GameObject CreateRoad(string name, Vector3 size, Vector3 worldPos)
        {
            return CreateGroundCube(_roadsRoot, name, size, RoadMaterial, worldPos, CityBlockKind.Road, worldSpace: true);
        }

        static GameObject CreateGroundCube(Transform parent, string name, Vector3 size, Material mat, Vector3 pos,
            CityBlockKind kind, bool worldSpace = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (worldSpace) go.transform.position = pos;
            else go.transform.localPosition = pos;
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            if (mat != null) renderer.sharedMaterial = mat;
            // CreatePrimitive 自带 BoxCollider，保留作为地面碰撞
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one;
            box.center = Vector3.zero;

            var marker = go.AddComponent<CityBlockMarker>();
            marker.Kind = kind;
            return go;
        }

        void UnloadFarLots(Vector2Int playerCell)
        {
            for (int i = _lots.Count - 1; i >= 0; i--)
            {
                var lot = _lots[i];
                if (lot == null)
                {
                    _lots.RemoveAt(i);
                    continue;
                }

                int cx = lot.GridX + lot.CellsX / 2;
                int cz = lot.GridZ + lot.CellsZ / 2;
                if (Mathf.Abs(cx - playerCell.x) <= UnloadRadiusCells
                    && Mathf.Abs(cz - playerCell.y) <= UnloadRadiusCells)
                    continue;

                for (int z = lot.GridZ; z < lot.GridZ + lot.CellsZ; z++)
                for (int x = lot.GridX; x < lot.GridX + lot.CellsX; x++)
                    _cellToLot.Remove(CellKey(x, z));

                _lots.RemoveAt(i);
                Destroy(lot.gameObject);
            }
        }

        System.Random RngForCell(int gx, int gz) => new System.Random(Hash(WorldSeed, gx, gz, 7));

        static int Hash(int a, int b, int c, int d)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + a;
                h = h * 31 + b;
                h = h * 31 + c;
                h = h * 31 + d;
                return h;
            }
        }

        static Material CreateRuntimeMat(Color color)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            return new Material(shader) { color = color };
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (Player == null) return;
            var cell = CityMapMetrics.WorldToCell(Player.position);
            Gizmos.color = Color.cyan;
            var origin = CityMapMetrics.CellOrigin(cell.x, cell.y);
            Gizmos.DrawWireCube(origin + new Vector3(CityMapMetrics.UnitMeters, 1f, CityMapMetrics.UnitMeters) * 0.5f,
                new Vector3(CityMapMetrics.UnitMeters, 2f, CityMapMetrics.UnitMeters));
        }
#endif
    }
}
