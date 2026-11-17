using System;
using UnityEngine;

namespace CardKeyAuth
{
    /// <summary>
    /// 墨金夜场风格卡密登录悬浮窗（对齐 UE CardKeyLoginWidget）。
    /// 全屏遮罩 + 居中卡片：入场上浮、金边呼吸、错误抖动、成功淡出。
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class CardKeyLoginOverlay : MonoBehaviour
    {
        /// <summary>低于其它 OnGUI 的 depth，保证画在最上层（Unity：越小越靠上）。</summary>
        const int TopGuiDepth = -10000;
        // 墨金夜场色板（对齐 UE CardKeyLoginStyle）
        static readonly Color DimOverlay = new Color(0.01f, 0.01f, 0.015f, 0.88f);
        static readonly Color CardOuter = new Color(0.14f, 0.12f, 0.09f, 0.55f);
        static readonly Color CardBg = new Color(0.06f, 0.055f, 0.05f, 0.98f);
        static readonly Color Gold = new Color(0.83f, 0.68f, 0.38f, 1f);
        static readonly Color GoldSoft = new Color(0.83f, 0.68f, 0.38f, 0.55f);
        static readonly Color GoldBright = new Color(0.93f, 0.80f, 0.48f, 1f);
        static readonly Color TitleColor = new Color(0.96f, 0.93f, 0.88f, 1f);
        static readonly Color MutedColor = new Color(0.55f, 0.52f, 0.48f, 1f);
        static readonly Color Accent = new Color(0.78f, 0.58f, 0.28f, 1f);
        static readonly Color AccentHover = new Color(0.90f, 0.70f, 0.36f, 1f);
        static readonly Color AccentPressed = new Color(0.62f, 0.45f, 0.20f, 1f);
        static readonly Color InputBg = new Color(0.03f, 0.03f, 0.03f, 1f);
        static readonly Color ErrorColor = new Color(0.90f, 0.42f, 0.36f, 1f);
        static readonly Color OkColor = new Color(0.62f, 0.78f, 0.52f, 1f);
        static readonly Color ButtonText = new Color(0.06f, 0.05f, 0.03f, 1f);

        CardKeyAuthRuntime _runtime;
        string _cardKey = "";
        string _status = "";
        bool _statusError;
        bool _busy;
        bool _visible = true;
        bool _focusInput;

        float _animTime;
        float _entranceAlpha;
        float _shakeTimeLeft;
        float _successFade;
        float _layoutScale = 1f;

        Texture2D _texWhite;
        Texture2D _texClear;
        Texture2D _texBtnNormal;
        Texture2D _texBtnHover;
        Texture2D _texBtnBusy;
        GUIStyle _brandStyle;
        GUIStyle _titleStyle;
        GUIStyle _dotStyle;
        GUIStyle _fieldStyle;
        GUIStyle _btnStyle;
        GUIStyle _statusStyle;
        GUIStyle _expireStyle;
        GUIStyle _footerStyle;
        bool _stylesReady;

        public bool IsVisible => _visible;
        public bool IsBusy => _busy;

        public void Bind(CardKeyAuthRuntime runtime)
        {
            _runtime = runtime;
        }

        public void Show()
        {
            _visible = true;
            _successFade = 0f;
            _animTime = 0f;
            _entranceAlpha = 0f;
            _shakeTimeLeft = 0f;
            _status = "";
            _statusError = false;
            _busy = false;
            _focusInput = true;
            ApplySavedCardKey();
            BringToFront();
        }

        public void Hide()
        {
            _visible = false;
            _busy = false;
            RestoreCanvases();
        }

        void BringToFront()
        {
            transform.SetAsLastSibling();
            // 压低场景里其它 Overlay Canvas，避免盖住登录门禁
            _canvasOrders ??= new System.Collections.Generic.List<(Canvas, int, bool)>();
            _canvasOrders.Clear();
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                var c = canvases[i];
                if (c == null || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                _canvasOrders.Add((c, c.sortingOrder, c.enabled));
                if (c.sortingOrder >= 1000)
                    c.sortingOrder = 100; // 仍可见，但不压过登录 OnGUI 交互层观感
            }
        }

        void RestoreCanvases()
        {
            if (_canvasOrders == null) return;
            for (int i = 0; i < _canvasOrders.Count; i++)
            {
                var (c, order, _) = _canvasOrders[i];
                if (c != null) c.sortingOrder = order;
            }
            _canvasOrders.Clear();
        }

        System.Collections.Generic.List<(Canvas, int, bool)> _canvasOrders;

        public void SetBusy(bool busy) => _busy = busy;

        public void SetStatus(string message, bool isError)
        {
            _status = message ?? "";
            _statusError = isError;
            if (isError && !string.IsNullOrEmpty(message))
                _shakeTimeLeft = 0.42f;
        }

        public void PlaySuccessAndHide()
        {
            _busy = false;
            _statusError = false;
            _successFade = 0.45f;
        }

        public void ApplySavedCardKey()
        {
            if (_runtime?.Config == null) return;
            if (!_runtime.Config.RememberCardKey) return;
            var saved = _runtime.Config.LoadSavedCardKey();
            if (!string.IsNullOrEmpty(saved))
                _cardKey = saved;
        }

        void OnDestroy()
        {
            RestoreCanvases();
            DestroyTex(ref _texWhite);
            DestroyTex(ref _texClear);
            DestroyTex(ref _texBtnNormal);
            DestroyTex(ref _texBtnHover);
            DestroyTex(ref _texBtnBusy);
        }

        static void DestroyTex(ref Texture2D tex)
        {
            if (tex == null) return;
            Destroy(tex);
            tex = null;
        }

        void Update()
        {
            if (!_visible) return;
            float dt = Time.unscaledDeltaTime;
            _animTime += dt;

            const float entranceDur = 0.72f;
            _entranceAlpha = Mathf.Clamp01(_animTime / entranceDur);

            if (_shakeTimeLeft > 0f)
                _shakeTimeLeft = Mathf.Max(0f, _shakeTimeLeft - dt);

            if (_successFade > 0f)
            {
                _successFade = Mathf.Max(0f, _successFade - dt);
                if (_successFade <= 0f)
                {
                    _visible = false;
                    RestoreCanvases();
                }
            }
        }

        void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();

            // 压过 PhoneServerHud 等其它 OnGUI
            int prevDepth = GUI.depth;
            GUI.depth = TopGuiDepth;

            float ease = EaseOutCubic(_entranceAlpha);
            float bounce = EaseOutBack(Mathf.Clamp01(_entranceAlpha * 1.05f));
            // 慢速柔和呼吸，仅调透明度，不改边框宽度/字号（避免一卡一卡）
            float pulse = 0.5f + 0.5f * Mathf.Sin(_animTime * 1.15f);

            float successMul = _successFade > 0f
                ? Mathf.Clamp01(_successFade / 0.45f)
                : 1f;
            float dimA = ease * successMul;
            float cardA = ease * successMul;

            // 全屏遮罩
            var prevColor = GUI.color;
            GUI.color = new Color(DimOverlay.r, DimOverlay.g, DimOverlay.b, DimOverlay.a * dimA);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _texWhite);
            GUI.color = prevColor;

            float sw = Screen.width;
            float sh = Screen.height;
            bool portrait = sh >= sw * 1.05f;
            bool mobile = Application.isMobilePlatform || (portrait && Mathf.Min(sw, sh) >= 600f);
            const float baselineW = 392f;
            float cardW;
            if (portrait || Application.isMobilePlatform)
            {
                // 手机竖屏：尽量铺满宽度，并抬高缩放上限，避免看起来过小
                float maxW = Mathf.Min(sw - (mobile ? 28f : 24f), mobile ? 860f : 720f);
                cardW = Mathf.Clamp(sw * (mobile ? 0.94f : 0.92f), mobile ? 420f : 360f, maxW);
            }
            else
            {
                cardW = Mathf.Clamp(Mathf.Min(sw * 0.42f, 520f), 380f, 560f);
            }
            float maxScale = mobile ? 2.45f : 1.9f;
            float minScale = mobile ? 1.25f : 1.0f;
            _layoutScale = Mathf.Clamp(cardW / baselineW, minScale, maxScale);
            // 超高分辨率手机再补一层整体放大，保证手指可点
            if (Application.isMobilePlatform)
                _layoutScale = Mathf.Max(_layoutScale, Mathf.Clamp(Mathf.Min(sw, sh) / 520f, 1.35f, 2.45f));
            float s = _layoutScale;

            float padX = (portrait ? 30f : 32f) * s;
            float padY = (portrait ? 38f : 32f) * s;
            float inputBoxH = (mobile ? 58f : 46f) * s;
            float buttonBoxH = (mobile ? 60f : 48f) * s;
            float contentH =
                16f * s +          // brand
                12f * s +
                (mobile ? 48f : 40f) * s + // title
                12f * s +
                24f * s +          // divider
                20f * s +
                inputBoxH +        // input
                20f * s +
                buttonBoxH +       // button
                18f * s +
                44f * s +          // status
                10f * s +
                22f * s +          // expire
                12f * s +
                20f * s;           // footer
            float cardH = contentH + padY * 1.85f;
            // 高度不够时整体再压一点，避免超出屏幕
            float maxH = sh * 0.92f;
            if (cardH > maxH && cardH > 1f)
            {
                float fit = maxH / cardH;
                s *= fit;
                _layoutScale = s;
                padX = (portrait ? 30f : 32f) * s;
                padY = (portrait ? 38f : 32f) * s;
                inputBoxH = (mobile ? 58f : 46f) * s;
                buttonBoxH = (mobile ? 60f : 48f) * s;
                contentH =
                    16f * s + 12f * s + (mobile ? 48f : 40f) * s + 12f * s + 24f * s + 20f * s +
                    inputBoxH + 20f * s + buttonBoxH + 18f * s + 44f * s + 10f * s + 22f * s + 12f * s + 20f * s;
                cardH = contentH + padY * 1.85f;
                cardW *= fit;
            }

            float scaleAnim = Mathf.Lerp(0.88f, 1f, bounce);
            if (_successFade > 0f)
            {
                float outT = 1f - Mathf.Clamp01(_successFade / 0.45f);
                scaleAnim *= Mathf.Lerp(1f, 1.04f, outT);
            }

            float riseY = Mathf.Lerp(28f, 0f, ease);
            float shakeX = 0f;
            if (_shakeTimeLeft > 0f)
            {
                float shakeT = 1f - (_shakeTimeLeft / 0.42f);
                float decay = 1f - shakeT;
                shakeX = Mathf.Sin(shakeT * Mathf.PI * 8f) * 10f * decay;
            }

            float drawW = cardW * scaleAnim;
            float drawH = cardH * scaleAnim;
            float cx = (sw - drawW) * 0.5f + shakeX;
            float cy = (sh - drawH) * 0.5f + riseY;

            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(cx + drawW * 0.5f, cy + drawH * 0.5f, 0f),
                Quaternion.identity,
                new Vector3(scaleAnim, scaleAnim, 1f)) *
                Matrix4x4.TRS(new Vector3(-cardW * 0.5f, -cardH * 0.5f, 0f), Quaternion.identity, Vector3.one);

            Color guiAlpha = new Color(1f, 1f, 1f, cardA);
            GUI.color = guiAlpha;

            // 外框
            DrawFramedCard(new Rect(0, 0, cardW, cardH), pulse);

            float x = padX;
            float y = padY;
            float fw = cardW - padX * 2f;

            // Brand
            var brandCol = new Color(Gold.r, Gold.g, Gold.b, 0.82f + 0.12f * pulse);
            _brandStyle.fontSize = Mathf.RoundToInt((mobile ? 15f : 12f) * s);
            _brandStyle.normal.textColor = brandCol;
            GUI.Label(new Rect(x, y, fw, (mobile ? 22f : 18f) * s), "授权登录", _brandStyle);
            y += (mobile ? 18f : 14f) * s + 12f * s;

            // Title
            _titleStyle.fontSize = Mathf.RoundToInt((mobile ? 42f : (portrait ? 36f : 34f)) * s);
            _titleStyle.normal.textColor = TitleColor;
            GUI.Label(new Rect(x, y, fw, (mobile ? 52f : 42f) * s), "正佳传媒", _titleStyle);
            y += (mobile ? 48f : 40f) * s + 12f * s;

            // Divider
            DrawGoldDivider(new Rect(x + 12f * s, y, fw - 24f * s, 24f * s), s, pulse, portrait);
            GUI.color = guiAlpha;
            y += 24f * s + 20f * s;

            // Input
            float inputH = inputBoxH;
            var inputRect = new Rect(x, y, fw, inputH);
            DrawFilled(inputRect, InputBg);
            DrawHairlineBorder(inputRect, new Color(0.55f, 0.46f, 0.30f, 0.95f), 1f);
            _fieldStyle.fontSize = Mathf.RoundToInt((mobile ? 18f : 15f) * s);
            _fieldStyle.padding = new RectOffset(
                Mathf.RoundToInt(16f * s), Mathf.RoundToInt(16f * s),
                Mathf.RoundToInt(14f * s), Mathf.RoundToInt(14f * s));
            GUI.SetNextControlName("CardKeyInput");
            _cardKey = GUI.TextField(new Rect(x + 2f, y + 2f, fw - 4f, inputH - 4f), _cardKey ?? "", _fieldStyle);
            if (_focusInput)
            {
                GUI.FocusControl("CardKeyInput");
                _focusInput = false;
            }
            if (string.IsNullOrEmpty(_cardKey) && GUI.GetNameOfFocusedControl() != "CardKeyInput")
            {
                var ph = new GUIStyle(_fieldStyle) { normal = { textColor = MutedColor }, alignment = TextAnchor.MiddleCenter };
                GUI.Label(inputRect, "请输入卡密", ph);
            }
            y += inputH + 20f * s;

            // Button — 圆角香槟金实心（对齐 UE，无硬色带）
            float btnH = buttonBoxH;
            var btnRect = new Rect(x, y, fw, btnH);
            var mouse = Event.current.mousePosition;
            bool hover = btnRect.Contains(mouse);
            float breathe = _busy ? (0.97f + 0.03f * pulse) : 1f;
            var prevBtn = GUI.color;
            GUI.color = new Color(guiAlpha.r, guiAlpha.g, guiAlpha.b, guiAlpha.a * breathe);
            DrawGoldButton(btnRect, _busy, hover && !_busy);
            GUI.color = prevBtn;

            _btnStyle.fontSize = Mathf.RoundToInt((mobile ? 20f : 16f) * s);
            var labelCol = _busy ? new Color(0.75f, 0.72f, 0.68f, 1f) : ButtonText;
            _btnStyle.normal.textColor = labelCol;
            _btnStyle.hover.textColor = labelCol;
            _btnStyle.active.textColor = labelCol;
            _btnStyle.focused.textColor = labelCol;
            GUI.enabled = !_busy;
            if (GUI.Button(btnRect, _busy ? "正在验证…" : "进入游戏", _btnStyle))
                Submit();
            GUI.enabled = true;
            GUI.color = guiAlpha;
            y += btnH + 18f * s;

            // Status
            float statusH = Mathf.Max(40f, 44f * s);
            _statusStyle.fontSize = Mathf.RoundToInt((mobile ? 15f : 12f) * s);
            if (_busy)
            {
                float sa = 0.75f + 0.25f * pulse;
                _statusStyle.normal.textColor = new Color(MutedColor.r, MutedColor.g, MutedColor.b, sa);
            }
            else
            {
                _statusStyle.normal.textColor = _statusError ? ErrorColor
                    : (string.IsNullOrEmpty(_status) ? MutedColor : (_status.Contains("成功") ? OkColor : MutedColor));
            }
            GUI.Label(new Rect(x, y, fw, statusH), _status ?? "", _statusStyle);
            y += statusH + 8f * s;

            // Expire
            string expire = ResolveExpireText();
            _expireStyle.fontSize = Mathf.RoundToInt((mobile ? 14f : 12f) * s);
            _expireStyle.normal.textColor = GoldSoft;
            if (!string.IsNullOrEmpty(expire))
                GUI.Label(new Rect(x, y, fw, 22f * s), expire, _expireStyle);
            y += 22f * s + 12f * s;

            // Footer
            float fa = 0.38f + 0.10f * pulse;
            _footerStyle.fontSize = Mathf.RoundToInt((mobile ? 13f : 11f) * s);
            _footerStyle.normal.textColor = new Color(Gold.r, Gold.g, Gold.b, fa);
            GUI.Label(new Rect(x, y, fw, 20f * s), "正佳传媒 · 版权所有", _footerStyle);

            GUI.color = prevColor;
            GUI.matrix = matrix;

            // Enter 提交
            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                if (!_busy)
                {
                    Submit();
                    e.Use();
                }
            }

            // 吃掉其余点击，避免穿透到下层 HUD / UI
            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp ||
                e.type == EventType.MouseDrag || e.type == EventType.ScrollWheel)
                e.Use();

            GUI.depth = prevDepth;
        }

        void Submit()
        {
            if (_busy || _runtime == null) return;
            var key = (_cardKey ?? "").Trim();
            if (string.IsNullOrEmpty(key))
            {
                SetStatus("请先输入卡密", true);
                return;
            }
            _ = _runtime.LoginFromOverlayAsync(key);
        }

        string ResolveExpireText()
        {
            if (_runtime == null) return "";
            long expire = 0;
            if (_runtime.IsLoggedIn && _runtime.Client != null)
                expire = _runtime.Client.CardExpireAt;
            if (expire <= 0 && _runtime.Config != null)
                expire = _runtime.Config.LoadSavedExpireAt();
            return CardKeyAuthConfig.FormatExpireAtDisplayText(expire);
        }

        void DrawFramedCard(Rect r, float pulse01)
        {
            // 外层暗金底 + 双细线金框（不用“放大填充块”假描边，避免厚重方框感）
            DrawFilled(r, CardOuter);

            float inset = 3f;
            var inner = new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f);
            DrawFilled(inner, CardBg);

            float softA = 0.28f + 0.08f * pulse01;
            float mainA = 0.78f + 0.10f * pulse01;
            // 外圈软金
            DrawHairlineBorder(r, new Color(Gold.r, Gold.g, Gold.b, softA), 1f);
            // 内圈亮金
            DrawHairlineBorder(inner, new Color(GoldBright.r, GoldBright.g, GoldBright.b, mainA), 1f);
            // 中间再加一条极淡分隔，做出双层框厚度
            var mid = new Rect(r.x + 1.5f, r.y + 1.5f, r.width - 3f, r.height - 3f);
            DrawHairlineBorder(mid, new Color(GoldSoft.r, GoldSoft.g, GoldSoft.b, 0.35f + 0.08f * pulse01), 1f);
        }

        void DrawGoldButton(Rect r, bool busy, bool hover)
        {
            EnsureButtonTextures();
            Texture2D tex = busy ? _texBtnBusy : (hover ? _texBtnHover : _texBtnNormal);
            if (tex == null) return;
            // 整图拉伸，避免 9 切片接缝出现竖线
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
        }

        void EnsureButtonTextures()
        {
            if (_texBtnNormal != null && _texBtnHover != null && _texBtnBusy != null) return;
            // 用更接近按钮宽高比的贴图，拉伸后圆角仍自然
            _texBtnNormal = CreateRoundedGoldButton(256, 48, 14,
                new Color(0.86f, 0.70f, 0.40f, 1f),
                new Color(0.74f, 0.56f, 0.28f, 1f));
            _texBtnHover = CreateRoundedGoldButton(256, 48, 14,
                new Color(0.94f, 0.80f, 0.48f, 1f),
                new Color(0.84f, 0.66f, 0.34f, 1f));
            _texBtnBusy = CreateRoundedGoldButton(256, 48, 14,
                new Color(0.28f, 0.25f, 0.20f, 1f),
                new Color(0.20f, 0.18f, 0.14f, 1f));
        }

        /// <summary>圆角按钮贴图：柔和上下渐变 + 抗锯齿圆角（对齐 UE RoundedBox）。</summary>
        static Texture2D CreateRoundedGoldButton(int w, int h, float radius, Color top, Color bottom)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float r = Mathf.Max(1f, radius);
            for (int y = 0; y < h; y++)
            {
                // Texture y=0 在底部；顶部应更亮
                float tFromTop = h <= 1 ? 0f : 1f - y / (float)(h - 1);
                float ease = tFromTop * tFromTop * (3f - 2f * tFromTop);
                Color body = Color.Lerp(top, bottom, ease * 0.45f);
                for (int x = 0; x < w; x++)
                {
                    float a = RoundedRectCoverage(x + 0.5f, y + 0.5f, w, h, r);
                    var c = body;
                    float hi = Mathf.Clamp01(1f - tFromTop * 2.4f) * 0.14f * a;
                    c.r = Mathf.Min(1f, c.r + hi);
                    c.g = Mathf.Min(1f, c.g + hi * 0.85f);
                    c.b = Mathf.Min(1f, c.b + hi * 0.55f);
                    c.a = a;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply(false, true);
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        static float RoundedRectCoverage(float px, float py, float w, float h, float radius)
        {
            float r = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f);
            float cx = Mathf.Clamp(px, r, w - r);
            float cy = Mathf.Clamp(py, r, h - r);
            float dx = px - cx;
            float dy = py - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            // 1px 抗锯齿边缘
            return Mathf.Clamp01(r - dist + 0.5f);
        }

        void DrawGoldDivider(Rect r, float s, float pulse, bool portrait)
        {
            float lineH = Mathf.Max(1.5f, (portrait ? 2f : 1.5f) * s);
            float midW = 28f * s;
            float sideW = (r.width - midW) * 0.5f;
            float ly = r.y + (r.height - lineH) * 0.5f;
            float lineA = (portrait ? 0.70f : 0.55f) * (0.90f + 0.10f * pulse);

            var prev = GUI.color;
            GUI.color = new Color(Gold.r, Gold.g, Gold.b, lineA * prev.a);
            GUI.DrawTexture(new Rect(r.x, ly, sideW - 4f * s, lineH), _texWhite);
            GUI.DrawTexture(new Rect(r.xMax - (sideW - 4f * s), ly, sideW - 4f * s, lineH), _texWhite);

            float glow = 0.82f + 0.14f * pulse;
            _dotStyle.fontSize = Mathf.RoundToInt((portrait ? 14f : 12f) * s);
            _dotStyle.normal.textColor = new Color(Gold.r, Gold.g, Gold.b, glow);
            GUI.color = new Color(1f, 1f, 1f, prev.a);
            GUI.Label(new Rect(r.x + sideW, r.y, midW, r.height), "◆", _dotStyle);
            GUI.color = prev;
        }

        void DrawFilled(Rect r, Color fill)
        {
            var prev = GUI.color;
            GUI.color = new Color(fill.r, fill.g, fill.b, fill.a * prev.a);
            GUI.DrawTexture(r, _texWhite);
            GUI.color = prev;
        }

        /// <summary>1px 细线描边（四边），比“外扩实心块”干净得多。</summary>
        void DrawHairlineBorder(Rect r, Color outline, float thickness)
        {
            if (outline.a <= 0.01f) return;
            var prev = GUI.color;
            GUI.color = new Color(outline.r, outline.g, outline.b, outline.a * prev.a);
            float t = Mathf.Max(1f, thickness);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), _texWhite); // top
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), _texWhite); // bottom
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), _texWhite); // left
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), _texWhite); // right
            GUI.color = prev;
        }

        void EnsureStyles()
        {
            if (_stylesReady && _texWhite != null && _texClear != null) return;
            _texWhite = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _texWhite.SetPixel(0, 0, Color.white);
            _texWhite.Apply(false, true);
            _texWhite.hideFlags = HideFlags.HideAndDontSave;

            // 必须用透明贴图，background=null 会回退成 Unity 默认灰按钮
            _texClear = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _texClear.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            _texClear.Apply(false, true);
            _texClear.hideFlags = HideFlags.HideAndDontSave;

            _brandStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal,
                wordWrap = false
            };
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            _dotStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            _fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(0, 0, 0, 0),
                normal = { background = _texClear, textColor = TitleColor },
                focused = { background = _texClear, textColor = TitleColor },
                active = { background = _texClear, textColor = TitleColor },
                hover = { background = _texClear, textColor = TitleColor }
            };
            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                overflow = new RectOffset(0, 0, 0, 0),
                normal = { background = _texClear, textColor = ButtonText },
                hover = { background = _texClear, textColor = ButtonText },
                active = { background = _texClear, textColor = ButtonText },
                focused = { background = _texClear, textColor = ButtonText },
                onNormal = { background = _texClear, textColor = ButtonText },
                onHover = { background = _texClear, textColor = ButtonText },
                onActive = { background = _texClear, textColor = ButtonText }
            };
            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _expireStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter
            };
            _footerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter
            };
            _stylesReady = true;
        }

        static float EaseOutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static string LocalizeError(string error)
        {
            var raw = (error ?? "").Trim();
            if (string.IsNullOrEmpty(raw))
                return "卡密无效或网络错误";

            // 去掉尾部 " (401)" / " (403)"
            var noStatus = System.Text.RegularExpressions.Regex.Replace(raw, @"\s*\(\d{3}\)\s*$", "").Trim();
            var upper = noStatus.ToUpperInvariant();

            // 优先按错误码映射（完整中文，不展示英文码）
            string code = null;
            string detail = noStatus;
            int colon = noStatus.IndexOf(':');
            if (colon > 0 && colon < 64)
            {
                var maybe = noStatus.Substring(0, colon).Trim();
                if (maybe.Length > 0 && maybe[0] < 128 && (maybe.Contains("_") || maybe == maybe.ToUpperInvariant()))
                {
                    code = maybe.ToUpperInvariant();
                    detail = noStatus.Substring(colon + 1).Trim();
                }
            }

            if (!string.IsNullOrEmpty(code))
            {
                switch (code)
                {
                    case "EMPTY_CARD_KEY": return "请先输入卡密";
                    case "CARD_INVALID": return "卡密不存在或格式错误";
                    case "CARD_EXPIRED": return "卡密已过期";
                    case "CARD_REVOKED": return "卡密已吊销";
                    case "CARD_LOCKED": return "卡密已锁定";
                    case "GAME_INVALID": return "游戏不存在，请检查配置";
                    case "GAME_DISABLED": return "游戏已停用";
                    case "GAME_MISMATCH": return "卡密与当前游戏不匹配";
                    case "DEVICE_MISMATCH":
                    case "DEVICE_BOUND":
                    case "DEVICE_LIMIT": return "设备绑定异常，请联系客服";
                    case "CLIENT_OUTDATED": return "客户端版本过旧，请升级";
                    case "SESSION_EXPIRED": return "会话已过期，请重新登录";
                    case "PREAUTH_EXPIRED":
                    case "PREAUTH_EXPIRED_OR_DENIED": return "授权已失效，请重新登录";
                    case "PREAUTH_INVALID": return "授权校验失败，请重新登录";
                    case "INTEGRITY_FAIL": return "客户端完整性校验失败";
                    case "CRYPTO_REQUIRED":
                    case "CRYPTO_KID_MISMATCH":
                    case "CRYPTO_UNWRAP_FAIL":
                    case "CRYPTO_DECRYPT_FAIL": return "安全通道异常，请重试";
                    case "CLOCK_SKEW": return "设备时间偏差过大，请校准时间";
                    case "RATE_LIMITED": return "请求过于频繁，请稍后再试";
                    case "RISK_IP_BLOCKED":
                    case "MEMORY_PROBE": return "检测到风险环境，请联系管理员解除";
                    case "PLATFORM": return "当前运行环境不支持加密组件";
                    case "BAD_REQUEST":
                        if (ContainsCjk(detail)) return detail;
                        if (detail.IndexOf("签名", StringComparison.Ordinal) >= 0) return "签名校验失败";
                        return "请求无效，请重试";
                    case "UNAUTHORIZED": return "未授权，请重新登录";
                    case "FORBIDDEN": return "无权限使用此卡密";
                    case "NOT_FOUND": return "资源不存在";
                    case "AUTH_SERVER":
                    case "AUTH_SERVER_NOT_CONFIGURED": return "鉴权服务未配置";
                    case "CLIENT_INIT_FAILED": return "鉴权模块初始化失败";
                    case "NETWORK": return "网络异常，请检查网络后重试";
                }
            }

            if (upper.Contains("NOT SUPPORTED ON THIS PLATFORM") || upper.Contains("PLATFORMNOTSUPPORTED"))
                return "当前运行环境不支持加密组件，请更新客户端";
            if (upper.Contains("CANNOT CONNECT") || upper.Contains("CONNECTION") ||
                upper.Contains("NETWORK") || upper.Contains("TIMED OUT") || upper.Contains("TIMEOUT") ||
                upper.Contains("NAME RESOLUTION") || upper.Contains("SSL") || upper.Contains("TLS") ||
                upper.Contains("UNABLE TO COMPLETE") || upper.Contains("HTTP"))
                return "网络异常，请检查网络后重试";

            if (upper.Contains("CLIENT_OUTDATED")) return "客户端版本过旧，请升级";
            if (upper.Contains("CARD_EXPIRED") || (upper.Contains("EXPIRED") && upper.Contains("CARD")))
                return "卡密已过期";
            if (upper.Contains("CARD_INVALID") || upper.Contains("INVALID"))
                return "卡密无效或已过期";
            if (upper.Contains("REVOKED")) return "卡密已吊销";
            if (upper.Contains("LOCKED") || upper.Contains("DISABLED") || upper.Contains("BANNED"))
                return "卡密已被禁用";
            if (upper.Contains("RISK_IP_BLOCKED") || upper.Contains("MEMORY_PROBE") ||
                upper.Contains("风险环境"))
                return "检测到风险环境，请联系管理员解除";
            if (upper.Contains("INTEGRITY")) return "客户端完整性校验失败";
            if (upper.Contains("PREAUTH")) return "授权校验失败，请重新登录";
            if (upper.Contains("SESSION")) return "会话已过期，请重新登录";
            if (upper.Contains("GAME")) return "游戏配置不匹配";
            if (upper.Contains("CRYPTO") || upper.Contains("解密") || upper.Contains("公钥"))
                return "安全通道异常，请重试";

            // 已是中文详情则直接展示
            if (ContainsCjk(detail)) return detail;
            if (ContainsCjk(noStatus)) return noStatus;

            return "卡密无效或网络错误";
        }

        static bool ContainsCjk(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (var c in s)
            {
                if (c >= 0x4E00 && c <= 0x9FFF) return true;
            }
            return false;
        }
    }
}
