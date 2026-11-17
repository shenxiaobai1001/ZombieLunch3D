using UnityEngine;

/// <summary>弹幕功能执行入口：统一转发到 GameBarrageBridge。</summary>
public class BarrageExecutting
{
    public static void OnExecutingBarrage(ActionTask task, int index, BarrageValue barrageFuncData)
    {
        if (task == null)
        {
            EventManager.Instance.SendMessage(Events.OnBarryExecutEnd, index);
            return;
        }

        if (string.IsNullOrEmpty(task.callName))
        {
            Debug.LogWarning("[BarrageExecutting] callName 为空");
            EventManager.Instance.SendMessage(Events.OnBarryExecutEnd, index);
            return;
        }

        GameBarrageBridge.Handle(task, index);
    }
}
