using UnityEngine;

namespace CardKeyAuth
{
    /// <summary>
    /// 占位物体：名称即本轮 token。无业务逻辑，仅用于多层心跳校验。
    /// </summary>
    public sealed class IntegrityBeacon : MonoBehaviour
    {
        internal int Slot;
        internal int Round;
        internal int InstanceStamp;

        void OnDestroy()
        {
            Slot = -1;
            Round = -1;
            InstanceStamp = 0;
        }
    }
}
