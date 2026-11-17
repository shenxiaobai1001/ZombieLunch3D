# CardKey Auth（Unity UPM 插件）

卡密登录门禁 + 心跳租约 + 功能预授权。已内置 BouncyCastle。

## 别人怎么接（约 2 分钟）

### 1. 安装插件（任选一种）

**方式 A · 本地包（推荐）**

1. 把整个文件夹 `com.zhengjia.cardkeyauth` 拷到工程的 `Packages/` 下  
2. 或在 `Packages/manifest.json` 增加：

```json
"com.zhengjia.cardkeyauth": "file:../相对或绝对路径/com.zhengjia.cardkeyauth"
```

**方式 B · 直接拖进 Packages**  
用 Unity Package Manager → `+` → `Add package from disk...` → 选本目录的 `package.json`。

### 2. 菜单配置

Unity 顶部菜单：**CardKey Auth → 接入向导**

填写：
- `ClientSignSecret`（与服务端一致）
- `GameName`（与后台游戏名完全一致）

保存即可。插件会自动生成 `Assets/Resources/CardKeyAuthSettings.asset`，进游戏自动弹登录窗。

### 3. 功能入口一行鉴权

```csharp
if (!CardKeyAuth.CardKeyGate.TryAuthorize("功能名", out var deny))
{
    Debug.LogWarning(deny);
    return;
}
// 执行功能…
```

功能名必须与后台开通的名称一字不差。

---

## 还要手写 Bootstrap 吗？

**不用。** 勾选 `AutoStart` 后插件会自动创建 `CardKeyAuth` 运行时。

## 注意

- 编辑器与真机一律强制登录
- Android 设备码使用系统 `ANDROID_ID`（同签名重装一般不变）
- API Compatibility 建议 .NET Standard 2.1 / .NET Framework 等价
