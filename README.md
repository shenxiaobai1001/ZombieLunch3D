# BarrageFramework（纯净框架工程）

从沙威玛工程剥离业务逻辑后的 **Unity 模块骨架**，便于开新直播互动游戏时复用。

路径：`D:\MyWork1\UnityProject\SHAWEIMAProject\BarrageFramework`

首次用 Unity 打开后会生成 `Library/`（未包含在拷贝中）。

---

## 保留了什么

| 模块 | 位置 | 说明 |
|------|------|------|
| 弹幕 / 整蛊 UI | `Assets/Game/Barrage/` | WebSocket、Prank 配置、Normal/Box/Special/Lottery |
| 弹幕执行队列 | `Assets/Game/Scripts/Project/BarrageFunc/` | `BarrageFuncController`、`BarrageFuncBase`、`BarrageExecutting` |
| 游戏功能桥（空壳） | `GameBarrageBridge.cs` | **新游戏在这里写逻辑** |
| 视频 | `Project/Mod/` + `AVProVideo/` | `ModVideoPlayer` |
| 音频 | `Scripts/Plug/Sound.cs` | BGM / 音效 / 弹幕独立音量 |
| 事件 | `Scripts/Tools/EventManager.cs` + `Data/Config.cs` 的 `Events` | |
| 对象池 | `Scripts/Tools/SimplePool.cs` | |
| 资源加载 | `Plug/Loaded.cs` + `YooUpdateManager.cs` | YooAsset |
| CSV / 工具 | `Scripts/ConfigToCv/`、`Data/GameData.cs` | `barrage.csv` |
| 卡密鉴权 | `Assets/ZhengJiaAuth/` | `CardKeyGate.TryAuthorize(功能名)` |
| 启动场景 | `InitScence` → Yoo → `StartScence` | **已去掉 Main 厨房场景** |

---

## 刻意去掉了什么

- 厨房 / 沙威玛制作（`Kitchen`）
- 顾客 / 订单 / 货币（`Customer`）
- 排行榜、目标单数等业务 UI
- `ShawarmaBarrageBridge`、锁链、锁老头、薯条机 FX 等业务整蛊实现
- `Main.unity` 玩法场景

---

## 新游戏怎么接整蛊

1. 在后台开通功能名（与 Calls 一致）。
2. 改 [`GameBarrageBridge.AllCalls`](Assets/Game/Scripts/Project/BarrageFunc/GameBarrageBridge.cs) 与 `Handle` 里的 `switch`。
3. 同步 `barrage.csv`（`Resources_moved/Config`）与 PrankUI 配置。
4. 需要鉴权时保持 `CardKeyGate.TryAuthorize(task.callName)`（模板里已留）。

示例：

```csharp
case "大胃王":
    // 生成角色 / 播特效 …
    break;
```

---

## 使用注意

1. **用 Unity 2022.3 打开本文件夹**（与原工程同版本）。
2. `StartScence` 里可能仍有「Missing Script」（原沙威玛 UI 脚本已删），删掉缺失组件或换成你的菜单即可。
3. `StreamingAssets/yoo` 仍是原包资源目录；正式新项目建议清掉后重新打 Yoo 包。
4. `CardKeyAuthSettings`（`Assets/Resources`）里 `GameName` 请改成你的新游戏名。
5. 模板体积约 300MB（含 AVPro + 原 StreamingAssets）；不含 `Library`。

---

## 推荐目录约定（新业务）

```
Assets/Game/Scripts/Project/
  BarrageFunc/     # 框架（少改）
  YourGame/        # 新玩法逻辑放这里
  Mod/             # 视频/直播壳
```
