#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.CityMap.Editor
{
    /// <summary>
    /// Game / City Map / Generate Placeholder Prefabs
    /// 布局只含地块底板+过道+建筑槽位；具体建筑类型在 Buildings/ 库中，运行时随机抽取。
    /// </summary>
    public static class CityMapPrefabBuilder
    {
        const string Root = "Assets/Game/Resources_moved/Prefabs/Game/CityMap";
        const string MatDir = Root + "/Materials";
        const string PieceDir = Root + "/Pieces";
        const string BuildingDir = Root + "/Buildings";
        const string FacilityDir = Root + "/Facilities";
        const string LayoutDir = Root + "/Layouts";
        const string DemoDir = Root + "/Demo";
        const string CatalogPath = "Assets/Game/Resources/CityBuildingCatalog.asset";
        const string LayoutLibraryPath = "Assets/Game/Resources/CityLotLayoutLibrary.asset";

        static readonly Color LotPadColor = new Color(0.75f, 0.72f, 0.65f);
        static readonly Color RoadColor = new Color(0.22f, 0.22f, 0.24f);
        static readonly Color CorridorColor = new Color(0.95f, 0.85f, 0.35f);
        static readonly Color SlotColor = new Color(1f, 1f, 1f, 0.15f);

        struct BuildingDef
        {
            public string TypeId;
            public CityBuildingFootprint Footprint;
            public Color Color;
            public float HeightScale;
            public float Weight;
            public bool Facility;
        }

        // 示例类型库：同 footprint 多种类型，后续可继续往对应目录加预制体并挂进 Catalog
        static readonly BuildingDef[] BuildingDefs =
        {
            // 1x1
            new BuildingDef { TypeId = "House", Footprint = CityBuildingFootprint.B1x1, Color = new Color(0.35f, 0.55f, 0.95f), HeightScale = 1.0f, Weight = 1.2f },
            new BuildingDef { TypeId = "Shop", Footprint = CityBuildingFootprint.B1x1, Color = new Color(0.20f, 0.70f, 0.95f), HeightScale = 0.85f, Weight = 1.0f },
            new BuildingDef { TypeId = "Garage", Footprint = CityBuildingFootprint.B1x1, Color = new Color(0.45f, 0.50f, 0.70f), HeightScale = 0.55f, Weight = 0.7f },
            new BuildingDef { TypeId = "Clinic", Footprint = CityBuildingFootprint.B1x1, Color = new Color(0.55f, 0.75f, 1.0f), HeightScale = 1.1f, Weight = 0.5f },
            new BuildingDef { TypeId = "Kiosk", Footprint = CityBuildingFootprint.B1x1, Color = new Color(0.30f, 0.85f, 0.90f), HeightScale = 0.45f, Weight = 0.6f },
            // 2x1 专属
            new BuildingDef { TypeId = "StripMall", Footprint = CityBuildingFootprint.B2x1, Color = new Color(0.20f, 0.80f, 0.85f), HeightScale = 1.0f, Weight = 1.0f },
            new BuildingDef { TypeId = "Warehouse", Footprint = CityBuildingFootprint.B2x1, Color = new Color(0.35f, 0.65f, 0.70f), HeightScale = 1.15f, Weight = 0.9f },
            new BuildingDef { TypeId = "Barracks", Footprint = CityBuildingFootprint.B2x1, Color = new Color(0.15f, 0.55f, 0.65f), HeightScale = 0.9f, Weight = 0.6f },
            // 1x2 专属
            new BuildingDef { TypeId = "TownhouseRow", Footprint = CityBuildingFootprint.B1x2, Color = new Color(0.20f, 0.70f, 0.55f), HeightScale = 1.0f, Weight = 1.0f },
            new BuildingDef { TypeId = "FactoryWing", Footprint = CityBuildingFootprint.B1x2, Color = new Color(0.25f, 0.55f, 0.45f), HeightScale = 1.2f, Weight = 0.8f },
            new BuildingDef { TypeId = "SchoolWing", Footprint = CityBuildingFootprint.B1x2, Color = new Color(0.40f, 0.75f, 0.60f), HeightScale = 1.05f, Weight = 0.5f },
            // 2x2 专属
            new BuildingDef { TypeId = "Apartment", Footprint = CityBuildingFootprint.B2x2, Color = new Color(0.65f, 0.35f, 0.90f), HeightScale = 1.0f, Weight = 1.0f },
            new BuildingDef { TypeId = "Mall", Footprint = CityBuildingFootprint.B2x2, Color = new Color(0.55f, 0.30f, 0.75f), HeightScale = 0.75f, Weight = 0.7f },
            new BuildingDef { TypeId = "Hospital", Footprint = CityBuildingFootprint.B2x2, Color = new Color(0.75f, 0.45f, 0.95f), HeightScale = 1.15f, Weight = 0.4f },
            // 开放设施（占 1x1 槽）
            new BuildingDef { TypeId = "Parking", Footprint = CityBuildingFootprint.B1x1, Color = new Color(0.35f, 0.75f, 0.45f), HeightScale = 0.12f, Weight = 1.0f, Facility = true },
            new BuildingDef { TypeId = "Plaza", Footprint = CityBuildingFootprint.B1x1, Color = new Color(0.45f, 0.85f, 0.50f), HeightScale = 0.08f, Weight = 0.8f, Facility = true },
            new BuildingDef { TypeId = "Playground", Footprint = CityBuildingFootprint.B1x1, Color = new Color(0.30f, 0.80f, 0.40f), HeightScale = 0.2f, Weight = 0.6f, Facility = true },
            new BuildingDef { TypeId = "Garden", Footprint = CityBuildingFootprint.B1x1, Color = new Color(0.25f, 0.70f, 0.35f), HeightScale = 0.15f, Weight = 0.7f, Facility = true },
        };

        [MenuItem("Game/City Map/Generate Placeholder Prefabs")]
        public static void GenerateAll()
        {
            EnsureFolders();
            var mats = CreateBaseMaterials();
            CreateInfrastructurePieces(mats);
            var catalog = CreateBuildingLibraryAndCatalog();
            CreateLayouts(mats);
            CreateLayoutLibraryAsset();
            CreateDemo(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CityMap] 已生成：建筑库 + 槽位布局 + LayoutLibrary。请把 CityLotManager 挂到场景。");
            EditorUtility.DisplayDialog("City Map",
                "已生成占位内容：\n" + Root +
                "\n\n流式生成：在场景挂 CityLotManager，指定 Player。\n" +
                "LayoutLibrary / BuildingCatalog 在 Resources。",
                "OK");
        }

        [MenuItem("Game/City Map/Create Lot Manager In Scene")]
        public static void CreateLotManagerInScene()
        {
            var existing = Object.FindObjectOfType<CityLotManager>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                EditorUtility.DisplayDialog("City Map", "场景里已有 CityLotManager。", "OK");
                return;
            }

            var go = new GameObject("CityLotManager");
            var mgr = go.AddComponent<CityLotManager>();
            mgr.LayoutLibrary = AssetDatabase.LoadAssetAtPath<CityLotLayoutLibrary>(LayoutLibraryPath);
            mgr.BuildingCatalog = AssetDatabase.LoadAssetAtPath<CityBuildingCatalog>(CatalogPath);
            mgr.LotPadMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Mat_LotPad.mat");
            mgr.RoadMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Mat_Road.mat");
            go.AddComponent<ShooterCameraBinder>();
            Undo.RegisterCreatedObjectUndo(go, "Create CityLotManager");
            Selection.activeObject = go;
        }

        [MenuItem("Game/City Map/Add Shooter Camera Binder")]
        public static void AddShooterCameraBinder()
        {
            var binder = Object.FindObjectOfType<ShooterCameraBinder>();
            if (binder != null)
            {
                Selection.activeObject = binder.gameObject;
                binder.Bind();
                EditorUtility.DisplayDialog("City Map", "已存在 ShooterCameraBinder，并已尝试绑定。", "OK");
                return;
            }

            var go = GameObject.Find("CityLotManager");
            if (go == null) go = new GameObject("ShooterCameraBinder");
            binder = go.GetComponent<ShooterCameraBinder>();
            if (binder == null) binder = go.AddComponent<ShooterCameraBinder>();
            binder.Bind();
            Undo.RegisterCreatedObjectUndo(go, "Add ShooterCameraBinder");
            Selection.activeObject = go;
            EditorUtility.DisplayDialog("City Map",
                "已添加 ShooterCameraBinder。\n若仍不跟随，请先执行：\nDias Games → Shooter System → Setup Scene for Shooter → Setup the Camera",
                "OK");
        }

        [MenuItem("Game/City Map/Select Prefab Folder")]
        public static void SelectFolder()
        {
            EnsureFolders();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(Root);
        }

        static void EnsureFolders()
        {
            CreateFolderRecursive(Root);
            CreateFolderRecursive(MatDir);
            CreateFolderRecursive(PieceDir);
            CreateFolderRecursive(BuildingDir);
            CreateFolderRecursive(FacilityDir);
            CreateFolderRecursive(LayoutDir);
            CreateFolderRecursive(DemoDir);
            CreateFolderRecursive("Assets/Game/Resources");
            CreateFolderRecursive(BuildingDir + "/B1x1");
            CreateFolderRecursive(BuildingDir + "/B2x1");
            CreateFolderRecursive(BuildingDir + "/B1x2");
            CreateFolderRecursive(BuildingDir + "/B2x2");
        }

        static void CreateFolderRecursive(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        static Material MakeMat(string name, Color color)
        {
            var path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Diffuse");
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.color = color;
            if (color.a < 0.99f)
            {
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        struct BaseMats
        {
            public Material LotPad, Road, Corridor, Slot;
        }

        static BaseMats CreateBaseMaterials()
        {
            return new BaseMats
            {
                LotPad = MakeMat("Mat_LotPad", LotPadColor),
                Road = MakeMat("Mat_Road", RoadColor),
                Corridor = MakeMat("Mat_Corridor", CorridorColor),
                Slot = MakeMat("Mat_BuildingSlot", SlotColor),
            };
        }

        static void CreateInfrastructurePieces(BaseMats mats)
        {
            float u = CityMapMetrics.UnitMeters;
            float rw = CityMapMetrics.RoadWidth;

            SaveCube("Piece_LotPad_1x1", PieceDir, CityMapMetrics.LotSize(CityLotSize.Lot1x1), mats.LotPad,
                m => { m.Kind = CityBlockKind.LotPad; m.LotSize = CityLotSize.Lot1x1; });
            SaveCube("Piece_LotPad_2x1", PieceDir, CityMapMetrics.LotSize(CityLotSize.Lot2x1), mats.LotPad,
                m => { m.Kind = CityBlockKind.LotPad; m.LotSize = CityLotSize.Lot2x1; });
            SaveCube("Piece_LotPad_1x2", PieceDir, CityMapMetrics.LotSize(CityLotSize.Lot1x2), mats.LotPad,
                m => { m.Kind = CityBlockKind.LotPad; m.LotSize = CityLotSize.Lot1x2; });
            SaveCube("Piece_LotPad_2x2", PieceDir, CityMapMetrics.LotSize(CityLotSize.Lot2x2), mats.LotPad,
                m => { m.Kind = CityBlockKind.LotPad; m.LotSize = CityLotSize.Lot2x2; });

            SaveCube("Piece_Road_Straight_X", PieceDir, new Vector3(u * 2f + rw, CityMapMetrics.RoadHeight, rw), mats.Road,
                m => { m.Kind = CityBlockKind.Road; m.LayoutId = "StraightX"; });
            SaveCube("Piece_Road_Straight_Z", PieceDir, new Vector3(rw, CityMapMetrics.RoadHeight, u * 2f + rw), mats.Road,
                m => { m.Kind = CityBlockKind.Road; m.LayoutId = "StraightZ"; });
            SaveCube("Piece_Road_Crossing", PieceDir, new Vector3(rw, CityMapMetrics.RoadHeight, rw), mats.Road,
                m => { m.Kind = CityBlockKind.Road; m.LayoutId = "Crossing"; });
            SaveCube("Piece_Corridor", PieceDir,
                new Vector3(CityMapMetrics.CorridorWidth, CityMapMetrics.CorridorHeight, u), mats.Corridor,
                m => { m.Kind = CityBlockKind.Corridor; });
        }

        static CityBuildingCatalog CreateBuildingLibraryAndCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CityBuildingCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CityBuildingCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Buildings1x1.Clear();
            catalog.Buildings2x1.Clear();
            catalog.Buildings1x2.Clear();
            catalog.Buildings2x2.Clear();
            catalog.OpenFacilities.Clear();

            foreach (var def in BuildingDefs)
            {
                var mat = MakeMat(def.Facility ? $"Mat_Facility_{def.TypeId}" : $"Mat_Building_{def.Footprint}_{def.TypeId}", def.Color);
                var size = BuildingVisualSize(def);
                string folder = def.Facility
                    ? FacilityDir
                    : $"{BuildingDir}/{def.Footprint}";
                CreateFolderRecursive(folder);

                var prefabName = def.Facility ? $"Facility_{def.TypeId}" : $"Building_{def.Footprint}_{def.TypeId}";
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = prefabName;
                // 保留碰撞：遮挡倾斜 Raycast / 角色阻挡需要
                go.transform.localScale = size;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;

                var variant = go.AddComponent<CityBuildingVariant>();
                variant.Footprint = def.Footprint;
                variant.TypeId = def.TypeId;
                variant.IsOpenFacility = def.Facility;
                variant.Weight = def.Weight;

                var marker = go.AddComponent<CityBlockMarker>();
                marker.Kind = def.Facility ? CityBlockKind.OpenFacility : CityBlockKind.Building;
                marker.BuildingFootprint = def.Footprint;
                marker.LayoutId = def.TypeId;

                var path = $"{folder}/{prefabName}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);

                var prefabVariant = prefab.GetComponent<CityBuildingVariant>();
                if (def.Facility) catalog.OpenFacilities.Add(prefabVariant);
                else
                {
                    switch (def.Footprint)
                    {
                        case CityBuildingFootprint.B1x1: catalog.Buildings1x1.Add(prefabVariant); break;
                        case CityBuildingFootprint.B2x1: catalog.Buildings2x1.Add(prefabVariant); break;
                        case CityBuildingFootprint.B1x2: catalog.Buildings1x2.Add(prefabVariant); break;
                        case CityBuildingFootprint.B2x2: catalog.Buildings2x2.Add(prefabVariant); break;
                    }
                }
            }

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static Vector3 BuildingVisualSize(BuildingDef def)
        {
            if (def.Facility)
            {
                float cell = CityMapMetrics.UnitMeters - CityMapMetrics.CorridorWidth;
                return new Vector3(cell, Mathf.Max(0.4f, CityMapMetrics.BuildingBaseHeight * def.HeightScale), cell);
            }

            var baseSize = CityMapMetrics.BuildingSize(def.Footprint);
            return new Vector3(baseSize.x, baseSize.y * def.HeightScale, baseSize.z);
        }

        static float U => CityMapMetrics.UnitMeters;
        static float C => CityMapMetrics.CorridorWidth;

        static void CreateLayouts(BaseMats mats)
        {
            CreateLayout1x1(mats);
            CreateLayout2x1Twin(mats);
            CreateLayout2x1Long(mats);
            CreateLayout1x2Twin(mats);
            CreateLayout1x2Long(mats);
            CreateLayout2x2Four(mats);
            CreateLayout2x2ThreeFacility(mats);
            CreateLayout2x2Two2x1(mats);
            CreateLayout2x2Two1x2(mats);
            CreateLayout2x2OneLarge(mats);
        }

        static GameObject LotPadPrefab(CityLotSize size)
        {
            string name = size == CityLotSize.Lot1x1 ? "Piece_LotPad_1x1"
                : size == CityLotSize.Lot2x1 ? "Piece_LotPad_2x1"
                : size == CityLotSize.Lot1x2 ? "Piece_LotPad_1x2"
                : "Piece_LotPad_2x2";
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PieceDir}/{name}.prefab");
        }

        static void CreateLayout1x1(BaseMats mats)
        {
            var root = NewRoot("Layout_Lot1x1_SingleSlot");
            AddPrefab(root, LotPadPrefab(CityLotSize.Lot1x1), Vector3.zero);
            AddSlot(root, "Slot_0", CityBuildingFootprint.B1x1, false,
                CityMapMetrics.BuildingSize(CityBuildingFootprint.B1x1),
                new Vector3(0f, CityMapMetrics.LotPadHeight, 0f), mats.Slot);
            Tag(root, CityLotSize.Lot1x1, "1x1_SingleSlot");
            AddFiller(root);
            SaveRoot(root, LayoutDir);
        }

        static void CreateLayout2x1Twin(BaseMats mats)
        {
            var root = NewRoot("Layout_Lot2x1_TwinSlots_Corridor");
            AddPrefab(root, LotPadPrefab(CityLotSize.Lot2x1), Vector3.zero);
            var bSize = CityMapMetrics.TwinBuildingSizeOn2x1();
            AddSlot(root, "Slot_L", CityBuildingFootprint.B1x1, false, bSize, new Vector3(-U * 0.5f, CityMapMetrics.LotPadHeight, 0f), mats.Slot);
            AddSlot(root, "Slot_R", CityBuildingFootprint.B1x1, false, bSize, new Vector3(U * 0.5f, CityMapMetrics.LotPadHeight, 0f), mats.Slot);
            AddSizedCube(root, "Corridor", new Vector3(C, CityMapMetrics.CorridorHeight, U - C), mats.Corridor,
                new Vector3(0f, CityMapMetrics.LotPadHeight * 0.5f + CityMapMetrics.CorridorHeight * 0.5f, 0f), CityBlockKind.Corridor);
            Tag(root, CityLotSize.Lot2x1, "2x1_TwinSlots");
            AddFiller(root);
            SaveRoot(root, LayoutDir);
        }

        static void CreateLayout2x1Long(BaseMats mats)
        {
            var root = NewRoot("Layout_Lot2x1_LongSlot");
            AddPrefab(root, LotPadPrefab(CityLotSize.Lot2x1), Vector3.zero);
            var size = CityMapMetrics.BuildingSize(CityBuildingFootprint.B2x1);
            AddSlot(root, "Slot_0", CityBuildingFootprint.B2x1, false, size, new Vector3(0f, CityMapMetrics.LotPadHeight, 0f), mats.Slot);
            Tag(root, CityLotSize.Lot2x1, "2x1_LongSlot");
            AddFiller(root);
            SaveRoot(root, LayoutDir);
        }

        static void CreateLayout1x2Twin(BaseMats mats)
        {
            var root = NewRoot("Layout_Lot1x2_TwinSlots_Corridor");
            AddPrefab(root, LotPadPrefab(CityLotSize.Lot1x2), Vector3.zero);
            var bSize = CityMapMetrics.TwinBuildingSizeOn1x2();
            AddSlot(root, "Slot_S", CityBuildingFootprint.B1x1, false, bSize, new Vector3(0f, CityMapMetrics.LotPadHeight, -U * 0.5f), mats.Slot);
            AddSlot(root, "Slot_N", CityBuildingFootprint.B1x1, false, bSize, new Vector3(0f, CityMapMetrics.LotPadHeight, U * 0.5f), mats.Slot);
            AddSizedCube(root, "Corridor", new Vector3(U - C, CityMapMetrics.CorridorHeight, C), mats.Corridor,
                new Vector3(0f, CityMapMetrics.LotPadHeight * 0.5f + CityMapMetrics.CorridorHeight * 0.5f, 0f), CityBlockKind.Corridor);
            Tag(root, CityLotSize.Lot1x2, "1x2_TwinSlots");
            AddFiller(root);
            SaveRoot(root, LayoutDir);
        }

        static void CreateLayout1x2Long(BaseMats mats)
        {
            var root = NewRoot("Layout_Lot1x2_LongSlot");
            AddPrefab(root, LotPadPrefab(CityLotSize.Lot1x2), Vector3.zero);
            var size = CityMapMetrics.BuildingSize(CityBuildingFootprint.B1x2);
            AddSlot(root, "Slot_0", CityBuildingFootprint.B1x2, false, size, new Vector3(0f, CityMapMetrics.LotPadHeight, 0f), mats.Slot);
            Tag(root, CityLotSize.Lot1x2, "1x2_LongSlot");
            AddFiller(root);
            SaveRoot(root, LayoutDir);
        }

        static void CreateLayout2x2Four(BaseMats mats)
        {
            var root = NewRoot("Layout_Lot2x2_Four1x1Slots");
            AddPrefab(root, LotPadPrefab(CityLotSize.Lot2x2), Vector3.zero);
            Place2x2Slots(root, mats, facilitySlot: -1);
            Tag(root, CityLotSize.Lot2x2, "2x2_FourSlots", CityLot2x2Layout.Four1x1);
            AddFiller(root);
            SaveRoot(root, LayoutDir);
        }

        static void CreateLayout2x2ThreeFacility(BaseMats mats)
        {
            var root = NewRoot("Layout_Lot2x2_Three1x1_PlusFacilitySlot");
            AddPrefab(root, LotPadPrefab(CityLotSize.Lot2x2), Vector3.zero);
            Place2x2Slots(root, mats, facilitySlot: 3);
            Tag(root, CityLotSize.Lot2x2, "2x2_ThreePlusFacility", CityLot2x2Layout.Three1x1PlusFacility);
            AddFiller(root);
            SaveRoot(root, LayoutDir);
        }

        static void Place2x2Slots(GameObject root, BaseMats mats, int facilitySlot)
        {
            Vector3[] centers =
            {
                new Vector3(-U * 0.5f, CityMapMetrics.LotPadHeight, -U * 0.5f),
                new Vector3(U * 0.5f, CityMapMetrics.LotPadHeight, -U * 0.5f),
                new Vector3(-U * 0.5f, CityMapMetrics.LotPadHeight, U * 0.5f),
                new Vector3(U * 0.5f, CityMapMetrics.LotPadHeight, U * 0.5f),
            };
            float cell = U - C;
            var slotSize = new Vector3(cell, CityMapMetrics.BuildingBaseHeight, cell);
            for (int i = 0; i < 4; i++)
            {
                bool facility = i == facilitySlot;
                AddSlot(root, facility ? "FacilitySlot_" + i : "Slot_" + i,
                    CityBuildingFootprint.B1x1, facility, slotSize, centers[i], mats.Slot);
            }
            AddSizedCube(root, "Corridor_X", new Vector3(U * 2f - C, CityMapMetrics.CorridorHeight, C), mats.Corridor,
                new Vector3(0f, CityMapMetrics.LotPadHeight * 0.5f + CityMapMetrics.CorridorHeight * 0.5f, 0f), CityBlockKind.Corridor);
            AddSizedCube(root, "Corridor_Z", new Vector3(C, CityMapMetrics.CorridorHeight, U * 2f - C), mats.Corridor,
                new Vector3(0f, CityMapMetrics.LotPadHeight * 0.5f + CityMapMetrics.CorridorHeight * 0.5f, 0f), CityBlockKind.Corridor);
        }

        static void CreateLayout2x2Two2x1(BaseMats mats)
        {
            var root = NewRoot("Layout_Lot2x2_Two2x1Slots");
            AddPrefab(root, LotPadPrefab(CityLotSize.Lot2x2), Vector3.zero);
            var size = CityMapMetrics.BuildingSize(CityBuildingFootprint.B2x1);
            AddSlot(root, "Slot_S", CityBuildingFootprint.B2x1, false, size, new Vector3(0f, CityMapMetrics.LotPadHeight, -U * 0.5f), mats.Slot);
            AddSlot(root, "Slot_N", CityBuildingFootprint.B2x1, false, size, new Vector3(0f, CityMapMetrics.LotPadHeight, U * 0.5f), mats.Slot);
            AddSizedCube(root, "Corridor", new Vector3(U * 2f - C, CityMapMetrics.CorridorHeight, C), mats.Corridor,
                new Vector3(0f, CityMapMetrics.LotPadHeight * 0.5f + CityMapMetrics.CorridorHeight * 0.5f, 0f), CityBlockKind.Corridor);
            Tag(root, CityLotSize.Lot2x2, "2x2_Two2x1Slots", CityLot2x2Layout.Two2x1);
            AddFiller(root);
            SaveRoot(root, LayoutDir);
        }

        static void CreateLayout2x2Two1x2(BaseMats mats)
        {
            var root = NewRoot("Layout_Lot2x2_Two1x2Slots");
            AddPrefab(root, LotPadPrefab(CityLotSize.Lot2x2), Vector3.zero);
            var size = CityMapMetrics.BuildingSize(CityBuildingFootprint.B1x2);
            AddSlot(root, "Slot_W", CityBuildingFootprint.B1x2, false, size, new Vector3(-U * 0.5f, CityMapMetrics.LotPadHeight, 0f), mats.Slot);
            AddSlot(root, "Slot_E", CityBuildingFootprint.B1x2, false, size, new Vector3(U * 0.5f, CityMapMetrics.LotPadHeight, 0f), mats.Slot);
            AddSizedCube(root, "Corridor", new Vector3(C, CityMapMetrics.CorridorHeight, U * 2f - C), mats.Corridor,
                new Vector3(0f, CityMapMetrics.LotPadHeight * 0.5f + CityMapMetrics.CorridorHeight * 0.5f, 0f), CityBlockKind.Corridor);
            Tag(root, CityLotSize.Lot2x2, "2x2_Two1x2Slots", CityLot2x2Layout.Two1x2);
            AddFiller(root);
            SaveRoot(root, LayoutDir);
        }

        static void CreateLayout2x2OneLarge(BaseMats mats)
        {
            var root = NewRoot("Layout_Lot2x2_OneLargeSlot");
            AddPrefab(root, LotPadPrefab(CityLotSize.Lot2x2), Vector3.zero);
            var size = CityMapMetrics.BuildingSize(CityBuildingFootprint.B2x2);
            AddSlot(root, "Slot_0", CityBuildingFootprint.B2x2, false, size, new Vector3(0f, CityMapMetrics.LotPadHeight, 0f), mats.Slot);
            Tag(root, CityLotSize.Lot2x2, "2x2_OneLargeSlot", CityLot2x2Layout.OneLarge2x2);
            AddFiller(root);
            SaveRoot(root, LayoutDir);
        }

        static void CreateDemo(CityBuildingCatalog catalog)
        {
            var root = NewRoot("Demo_CityBlock_Slots_WithRoads");
            float lot = U * 2f;
            float road = CityMapMetrics.RoadWidth;
            float step = lot + road;
            Vector3[] centers =
            {
                new Vector3(-step * 0.5f, 0f, -step * 0.5f),
                new Vector3(step * 0.5f, 0f, -step * 0.5f),
                new Vector3(-step * 0.5f, 0f, step * 0.5f),
                new Vector3(step * 0.5f, 0f, step * 0.5f),
            };
            string[] layouts =
            {
                "Layout_Lot2x2_Four1x1Slots",
                "Layout_Lot2x2_Three1x1_PlusFacilitySlot",
                "Layout_Lot2x2_Two2x1Slots",
                "Layout_Lot2x2_OneLargeSlot",
            };

            for (int i = 0; i < 4; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{LayoutDir}/{layouts[i]}.prefab");
                if (prefab == null) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.SetParent(root.transform, false);
                inst.transform.localPosition = centers[i];
                var filler = inst.GetComponent<CityLotFiller>();
                if (filler != null)
                {
                    filler.Catalog = catalog;
                    filler.Seed = 1000 + i * 17;
                    filler.FillOnAwake = true;
                }
            }

            var roadMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Mat_Road.mat");
            AddSizedCube(root, "Road_H", new Vector3(step * 2f, CityMapMetrics.RoadHeight, road), roadMat,
                new Vector3(0f, CityMapMetrics.RoadHeight * 0.5f, 0f), CityBlockKind.Road);
            AddSizedCube(root, "Road_V", new Vector3(road, CityMapMetrics.RoadHeight, step * 2f), roadMat,
                new Vector3(0f, CityMapMetrics.RoadHeight * 0.5f, 0f), CityBlockKind.Road);

            // 编辑器生成时就把 Demo 填一次，方便拖进场景直接看到随机结果
            var demoFillers = root.GetComponentsInChildren<CityLotFiller>(true);
            for (int i = 0; i < demoFillers.Length; i++)
            {
                demoFillers[i].Catalog = catalog;
                demoFillers[i].FillOnAwake = false; // prefab 里由实例 Seed 控制；下面立刻 Fill 烘焙预览
                demoFillers[i].Fill();
            }

            // Demo 根也挂一个说明用 filler（可选）
            var marker = root.AddComponent<CityBlockMarker>();
            marker.LayoutId = "Demo_Slots_WithRoads";
            SaveRoot(root, DemoDir);
        }

        static void CreateLayoutLibraryAsset()
        {
            var lib = AssetDatabase.LoadAssetAtPath<CityLotLayoutLibrary>(LayoutLibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<CityLotLayoutLibrary>();
                AssetDatabase.CreateAsset(lib, LayoutLibraryPath);
            }

            lib.Entries.Clear();
            void Add(CityLotSize size, string prefabName, float weight = 1f)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{LayoutDir}/{prefabName}.prefab");
                if (prefab == null) return;
                lib.Entries.Add(new CityLotLayoutEntry
                {
                    LotSize = size,
                    LayoutPrefab = prefab,
                    Weight = weight
                });
            }

            Add(CityLotSize.Lot1x1, "Layout_Lot1x1_SingleSlot", 1f);
            Add(CityLotSize.Lot2x1, "Layout_Lot2x1_TwinSlots_Corridor", 1f);
            Add(CityLotSize.Lot2x1, "Layout_Lot2x1_LongSlot", 1f);
            Add(CityLotSize.Lot1x2, "Layout_Lot1x2_TwinSlots_Corridor", 1f);
            Add(CityLotSize.Lot1x2, "Layout_Lot1x2_LongSlot", 1f);
            Add(CityLotSize.Lot2x2, "Layout_Lot2x2_Four1x1Slots", 1f);
            Add(CityLotSize.Lot2x2, "Layout_Lot2x2_Three1x1_PlusFacilitySlot", 0.8f);
            Add(CityLotSize.Lot2x2, "Layout_Lot2x2_Two2x1Slots", 0.9f);
            Add(CityLotSize.Lot2x2, "Layout_Lot2x2_Two1x2Slots", 0.9f);
            Add(CityLotSize.Lot2x2, "Layout_Lot2x2_OneLargeSlot", 0.7f);

            lib.Weight1x1 = 1.2f;
            lib.Weight2x1 = 1.0f;
            lib.Weight1x2 = 1.0f;
            lib.Weight2x2 = 0.85f;
            EditorUtility.SetDirty(lib);
        }

        static GameObject SaveCube(string name, string folder, Vector3 size, Material mat, System.Action<CityBlockMarker> setup)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            // 地块底板 / 马路保留碰撞；其它零件也保留，方便占位阶段站立行走
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var marker = go.AddComponent<CityBlockMarker>();
            setup?.Invoke(marker);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{folder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject NewRoot(string name) => new GameObject(name);

        static void AddPrefab(GameObject root, GameObject prefab, Vector3 localPos)
        {
            if (prefab == null) return;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(root.transform, false);
            inst.transform.localPosition = localPos;
        }

        static void AddSlot(GameObject root, string name, CityBuildingFootprint footprint, bool facility,
            Vector3 slotSize, Vector3 localPos, Material slotMat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;

            // 半透明预览框（仅示意槽位，不代表具体建筑）
            var preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            preview.name = "SlotPreview";
            Object.DestroyImmediate(preview.GetComponent<Collider>());
            preview.transform.SetParent(go.transform, false);
            preview.transform.localPosition = new Vector3(0f, slotSize.y * 0.5f, 0f);
            preview.transform.localScale = slotSize;
            preview.GetComponent<MeshRenderer>().sharedMaterial = slotMat;

            var slot = go.AddComponent<CityBuildingSlot>();
            slot.RequiredFootprint = footprint;
            slot.IsOpenFacility = facility;
            slot.SlotSize = slotSize;
        }

        static void AddSizedCube(GameObject root, string name, Vector3 size, Material mat, Vector3 localPos, CityBlockKind kind)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            // LotPad / Road 保留碰撞；走廊保留也无妨
            if (kind != CityBlockKind.LotPad && kind != CityBlockKind.Road)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var marker = go.AddComponent<CityBlockMarker>();
            marker.Kind = kind;
        }

        static void Tag(GameObject root, CityLotSize lot, string layoutId, CityLot2x2Layout layout2x2 = CityLot2x2Layout.Four1x1)
        {
            var marker = root.GetComponent<CityBlockMarker>() ?? root.AddComponent<CityBlockMarker>();
            marker.Kind = CityBlockKind.LotPad;
            marker.LotSize = lot;
            marker.LayoutId = layoutId;
            marker.Layout2x2 = layout2x2;
        }

        static void AddFiller(GameObject root)
        {
            var filler = root.GetComponent<CityLotFiller>() ?? root.AddComponent<CityLotFiller>();
            filler.Catalog = AssetDatabase.LoadAssetAtPath<CityBuildingCatalog>(CatalogPath);
            // 由 CityLotManager 在放置时按格子 Seed 填充，避免 Awake 抢跑
            filler.FillOnAwake = false;
            filler.Seed = layoutSeed(root.name);
        }

        static int layoutSeed(string name)
        {
            unchecked
            {
                int h = 23;
                for (int i = 0; i < name.Length; i++) h = h * 31 + name[i];
                return h;
            }
        }

        static void SaveRoot(GameObject root, string folder)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{root.name}.prefab");
            Object.DestroyImmediate(root);
        }
    }
}
#endif
