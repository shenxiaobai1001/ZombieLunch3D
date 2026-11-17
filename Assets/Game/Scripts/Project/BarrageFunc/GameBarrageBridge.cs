using CardKeyAuth;
using UnityEngine;

/// <summary>
/// 游戏弹幕功能桥（框架模板）：按 callName 分发。
/// 新游戏在此填写功能常量与 Handle 分支；可配合 CardKeyGate 做功能鉴权。
/// </summary>
public static class GameBarrageBridge
{
    // —— 示例功能名（与后台 / barrage.csv / PrankUI Calls 保持一致）——
    public const string CallSampleA = "示例功能A";
    public const string CallSampleB = "示例功能B";

    /// <summary>PrankUI Calls 默认列表；新游戏改成自己的功能表。</summary>
    public static readonly string[] AllCalls =
    {
        CallSampleA,
        CallSampleB,
    };

    public static void Handle(ActionTask task, int barrageIndex)
    {
        if (task == null || string.IsNullOrEmpty(task.callName))
        {
            Finish(barrageIndex);
            return;
        }

        // 功能名与后台开通名称一致时可打开鉴权
        if (!CardKeyGate.TryAuthorize(task.callName, out var deny))
        {
            Debug.LogWarning($"[GameBarrage] 鉴权拒绝「{task.callName}」: {deny}");
            Finish(barrageIndex);
            return;
        }

        switch (task.callName)
        {
            case CallSampleA:
                Debug.Log($"[GameBarrage] 示例功能A user={task.user}");
                break;
            case CallSampleB:
                Debug.Log($"[GameBarrage] 示例功能B user={task.user}");
                break;
            default:
                Debug.LogWarning($"[GameBarrage] 未处理功能: {task.callName}（请在 GameBarrageBridge.Handle 中接入）");
                break;
        }

        Finish(barrageIndex);
    }

    static void Finish(int barrageIndex)
    {
        EventManager.Instance.SendMessage(Events.OnBarryExecutEnd, barrageIndex);
    }
}
