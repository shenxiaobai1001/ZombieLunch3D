using System.Collections.Generic;
using Game.OcclusionLean;
using UnityEngine;

namespace Game.CityMap
{
    /// <summary>
    /// 扫描子节点上的 CityBuildingSlot，从 Catalog 按 footprint 随机实例化建筑/设施。
    /// 布局预制体本身不绑死具体建筑类型。
    /// </summary>
    public sealed class CityLotFiller : MonoBehaviour
    {
        public CityBuildingCatalog Catalog;
        public int Seed = 1;
        public bool FillOnAwake = true;
        public bool ClearExistingFilled = true;
        [Tooltip("生成建筑时自动挂 OcclusionLeanTarget，遮挡倾斜系统可独立工作")]
        public bool AttachOcclusionLeanTarget = true;

        readonly List<GameObject> _spawned = new List<GameObject>();

        void Awake()
        {
            if (FillOnAwake) Fill();
        }

        [ContextMenu("Fill Slots Now")]
        public void Fill()
        {
            if (Catalog == null)
                Catalog = CityBuildingCatalog.Load();
            if (Catalog == null)
            {
                Debug.LogWarning("[CityMap] CityLotFiller: 缺少 CityBuildingCatalog", this);
                return;
            }

            if (ClearExistingFilled)
                ClearSpawned();

            var rng = new System.Random(Seed);
            var slots = GetComponentsInChildren<CityBuildingSlot>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                var variant = Catalog.Pick(slot.RequiredFootprint, slot.IsOpenFacility, rng, slot.ForceTypeId);
                if (variant == null)
                {
                    Debug.LogWarning($"[CityMap] 无可用变体 footprint={slot.RequiredFootprint} facility={slot.IsOpenFacility}", slot);
                    continue;
                }

                var go = Instantiate(variant.gameObject, slot.transform, false);
                go.name = variant.TypeId;
                go.transform.localRotation = Quaternion.identity;
                FitIntoSlot(go, slot);
                if (AttachOcclusionLeanTarget && !slot.IsOpenFacility)
                    OcclusionLeanManager.EnsureTarget(go);
                _spawned.Add(go);
            }
        }

        static void FitIntoSlot(GameObject go, CityBuildingSlot slot)
        {
            // 占位方体用 localScale 表示尺寸；按槽位 XZ 适配（双拼槽比标准 1x1 略窄）
            var target = slot.SlotSize;
            if (target.x <= 0.01f || target.z <= 0.01f)
            {
                go.transform.localPosition = Vector3.zero;
                return;
            }

            var cur = go.transform.localScale;
            float sx = target.x / Mathf.Max(0.01f, cur.x);
            float sz = target.z / Mathf.Max(0.01f, cur.z);
            // 高度保持类型自身比例，仅在 XZ 压进槽位
            go.transform.localScale = new Vector3(cur.x * sx, cur.y, cur.z * sz);
            go.transform.localPosition = new Vector3(0f, go.transform.localScale.y * 0.5f, 0f);
        }

        public void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    if (Application.isPlaying) Destroy(_spawned[i]);
                    else DestroyImmediate(_spawned[i]);
                }
            }
            _spawned.Clear();

            // 编辑器下也可能已有旧实例
            var slots = GetComponentsInChildren<CityBuildingSlot>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                var t = slots[i].transform;
                for (int c = t.childCount - 1; c >= 0; c--)
                {
                    var child = t.GetChild(c).gameObject;
                    if (child.GetComponent<CityBuildingVariant>() != null)
                    {
                        if (Application.isPlaying) Destroy(child);
                        else DestroyImmediate(child);
                    }
                }
            }
        }
    }
}
