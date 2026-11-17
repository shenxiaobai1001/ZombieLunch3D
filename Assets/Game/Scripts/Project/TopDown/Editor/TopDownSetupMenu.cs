#if UNITY_EDITOR
using Game.OcclusionLean;
using UnityEditor;
using UnityEngine;

namespace Game.TopDown.Editor
{
    public static class TopDownSetupMenu
    {
        [MenuItem("Game/Top Down/Create Top Down Camera + Occlusion Lean")]
        public static void CreateSystems()
        {
            var camGo = GameObject.Find("TopDownCameraController");
            if (camGo == null)
            {
                camGo = new GameObject("TopDownCameraController");
                Undo.RegisterCreatedObjectUndo(camGo, "Create TopDownCameraController");
            }
            if (camGo.GetComponent<TopDownCameraController>() == null)
                Undo.AddComponent<TopDownCameraController>(camGo);

            var leanGo = GameObject.Find("OcclusionLeanManager");
            if (leanGo == null)
            {
                leanGo = new GameObject("OcclusionLeanManager");
                Undo.RegisterCreatedObjectUndo(leanGo, "Create OcclusionLeanManager");
            }
            var lean = leanGo.GetComponent<OcclusionLeanManager>();
            if (lean == null)
                lean = Undo.AddComponent<OcclusionLeanManager>(leanGo);

            lean.MaxLeanAngle = 28f;
            lean.LeanSpeed = 12f;
            lean.RecoverSpeed = 10f;
            lean.AutoAddTargetOnHit = true;
            lean.EnableProximityLean = true;

            Selection.activeGameObject = camGo;
            EditorUtility.DisplayDialog("Top Down",
                "已创建：\n• TopDownCameraController（俯视跟随 + WASD 屏幕相对）\n• OcclusionLeanManager（遮挡倾斜，MaxLeanAngle 可调）\n\n请确保角色 Tag=Player，然后 Play。",
                "OK");
        }
    }
}
#endif
