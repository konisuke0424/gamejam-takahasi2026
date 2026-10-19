using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace KaiForest
{
    /// <summary>The player's own options, kept between plays (PlayerPrefs): game volume, screen brightness and mouse sensitivity.</summary>
    public static class GameOptions
    {
        const string VolumeKey = "KaiForest.Volume";
        const string BrightnessKey = "KaiForest.Brightness";
        const string LookKey = "KaiForest.LookSpeed";
        public const float MinBrightness = 0.3f, MaxBrightness = 2f;
        public const float MinLookScale = 0.2f, MaxLookScale = 3f;

        /// <summary>0..1, the whole game's sound (AudioListener.volume).</summary>
        public static float Volume { get; private set; } = 1f;
        /// <summary>Multiplier on the picture's light (1 = as tuned; GameManager turns it into post exposure).</summary>
        public static float Brightness { get; private set; } = 1f;
        /// <summary>Multiplier on PlayerController.lookSensitivity (1 = as tuned).</summary>
        public static float LookScale { get; private set; } = 1f;

        public static void Load()
        {
            SetVolume(PlayerPrefs.GetFloat(VolumeKey, 1f));
            SetBrightness(PlayerPrefs.GetFloat(BrightnessKey, 1f));
            SetLookScale(PlayerPrefs.GetFloat(LookKey, 1f));
        }

        public static void SetBrightness(float v) => Brightness = Mathf.Clamp(v, MinBrightness, MaxBrightness);

        public static void SetVolume(float v)
        {
            Volume = Mathf.Clamp01(v);
            AudioListener.volume = Volume;
        }

        public static void SetLookScale(float v) => LookScale = Mathf.Clamp(v, MinLookScale, MaxLookScale);

        public static void Save()
        {
            PlayerPrefs.SetFloat(VolumeKey, Volume);
            PlayerPrefs.SetFloat(BrightnessKey, Brightness);
            PlayerPrefs.SetFloat(LookKey, LookScale);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// The gear button in the top-right corner and the settings window it opens (game volume, screen
    /// brightness and mouse sensitivity sliders, and the controls). The game is paused while the window
    /// is up; sound keeps playing so the volume can be heard as it changes. Built in code on the game's
    /// UI canvas (GameManager). Full screen is left to the hosting page (unityroom's own button).
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.9f, 0.86f, 0.8f);
        static readonly Color Dim = new Color(0.62f, 0.58f, 0.54f);
        static Texture2D gearTexture;
        static Sprite knobSprite;

        public bool IsOpen { get; private set; }
        /// <summary>Whether the gear is up at all (GameManager: title, opening and play only).</summary>
        public bool ButtonShown { get; set; } = true;

        /// <summary>
        /// The mouse belongs to the settings, not the game: the window is open, or the free cursor is on
        /// the gear (a click there mustn't also start the game, walk, or lock the cursor).
        /// </summary>
        public bool HasMouse
        {
            get
            {
                if (IsOpen || JustClosed) return true;
                if (Cursor.lockState == CursorLockMode.Locked) return false;
                var mouse = GameInput.Mouse;
                return mouse != null && IsOverGear(mouse.position.ReadValue());
            }
        }

        /// <summary>A finger put down here belongs to the settings (the window is open, or it is on the gear).</summary>
        public bool OwnsTouchAt(Vector2 screenPos) => IsOpen || JustClosed || IsOverGear(screenPos);

        /// <summary>
        /// The window closed this frame. A quick click can press and let go within one frame: the
        /// "とじる" button closes the window on the release, and the same press mustn't then count for
        /// the game (start it from the title, walk).
        /// </summary>
        bool JustClosed => Time.frameCount == closedFrame;

        bool IsOverGear(Vector2 screenPos) => ButtonShown && RectTransformUtility.RectangleContainsScreenPoint(gear, screenPos, null);

        Font font;
        RectTransform gear;
        CanvasGroup gearGroup;
        GameObject modal;
        Slider volumeSlider, brightnessSlider, lookSlider;
        Text volumeValue, brightnessValue, lookValue, escHint;
        float timeScaleBeforeOpen = 1f;
        int closedFrame = -1;

        public static SettingsMenu Create(Transform canvas, Font font)
        {
            // uGUI clicks need an event system; the game has none of its own.
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var root = MakeRect(canvas, "Settings", Vector2.zero, Vector2.zero);
            Stretch(root);
            var menu = root.gameObject.AddComponent<SettingsMenu>();
            menu.font = font;
            menu.Build(root);
            return menu;
        }

        void Build(RectTransform root)
        {
            // Gear: top-right corner, over everything.
            gear = MakeRect(root, "Gear", Vector2.zero, new Vector2(84f, 84f));
            gear.anchorMin = gear.anchorMax = gear.pivot = new Vector2(1f, 1f);
            gear.anchoredPosition = new Vector2(-36f, -36f);
            var icon = gear.gameObject.AddComponent<RawImage>();
            if (gearTexture == null) gearTexture = MakeGearTexture(128);
            icon.texture = gearTexture;
            var gearButton = gear.gameObject.AddComponent<Button>();
            Tint(gearButton, icon, new Color(0.82f, 0.78f, 0.72f, 0.8f));
            gearButton.onClick.AddListener(Open);
            gearGroup = gear.gameObject.AddComponent<CanvasGroup>();
            // While the cursor is captured the gear can't be clicked: "Esc" under it says how to get in.
            escHint = Label(root, "Esc", 26, Dim, Vector2.zero, new Vector2(84f, 34f), TextAnchor.MiddleCenter);
            var hr = escHint.rectTransform;
            hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(1f, 1f);
            hr.anchoredPosition = new Vector2(-36f, -124f);

            // The window: a dark veil (a click on it closes) with the panel on top.
            modal = MakeRect(root, "Modal", Vector2.zero, Vector2.zero).gameObject;
            Stretch((RectTransform)modal.transform);
            var veil = MakeRect(modal.transform, "Veil", Vector2.zero, Vector2.zero);
            Stretch(veil);
            veil.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
            var veilButton = veil.gameObject.AddComponent<Button>();
            veilButton.transition = Selectable.Transition.None;
            veilButton.navigation = new Navigation { mode = Navigation.Mode.None };
            veilButton.onClick.AddListener(Close);

            var panel = MakeRect(modal.transform, "Panel", Vector2.zero, new Vector2(1000f, 880f));
            panel.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.062f, 0.056f, 0.96f);
            var frame = panel.gameObject.AddComponent<Outline>();
            frame.effectColor = new Color(0.55f, 0.44f, 0.3f, 0.7f);
            frame.effectDistance = new Vector2(3f, -3f);

            Label(panel, "設定", 56, Ink, new Vector2(0f, 380f), new Vector2(600f, 80f), TextAnchor.MiddleCenter);

            volumeSlider = Row(panel, "ゲームの音", 270f, 0f, 1f, out volumeValue);
            volumeSlider.onValueChanged.AddListener(v => { GameOptions.SetVolume(v); ShowValues(); });

            brightnessSlider = Row(panel, "画面の明るさ", 160f, GameOptions.MinBrightness, GameOptions.MaxBrightness, out brightnessValue);
            brightnessSlider.onValueChanged.AddListener(v => { GameOptions.SetBrightness(Mathf.Round(v * 100f) / 100f); ShowValues(); });

            lookSlider = Row(panel, Application.isMobilePlatform ? "視点の感度" : "マウス感度", 50f, GameOptions.MinLookScale, GameOptions.MaxLookScale, out lookValue);
            lookSlider.onValueChanged.AddListener(v => { GameOptions.SetLookScale(Mathf.Round(v * 10f) / 10f); ShowValues(); });


            // The controls, for mouse and touch alike (the only place they are shown).
            var rule = MakeRect(panel, "Rule", new Vector2(0f, -55f), new Vector2(880f, 2f)).gameObject.AddComponent<Image>();
            rule.color = new Color(0.55f, 0.44f, 0.3f, 0.45f);
            rule.raycastTarget = false;
            Label(panel, "操作", 34, Ink, new Vector2(0f, -100f), new Vector2(600f, 50f), TextAnchor.MiddleCenter);
            string[,] controls =
            {
                { "マウスを動かす／スワイプ", "視点操作" },
                { "クリック中／タップ中", "前へ進む" },
                { "Esc・右上の歯車", "設定" },
            };
            for (int i = 0; i < controls.GetLength(0); i++)
            {
                float y = -150f - i * 44f;
                Label(panel, controls[i, 0], 28, Dim, new Vector2(-190f, y), new Vector2(380f, 44f), TextAnchor.MiddleRight);
                Label(panel, controls[i, 1], 28, Ink, new Vector2(230f, y), new Vector2(380f, 44f), TextAnchor.MiddleLeft);
            }

            var close = MakeRect(panel, "Close", new Vector2(0f, -375f), new Vector2(280f, 84f));
            var closeImage = close.gameObject.AddComponent<Image>();
            var closeButton = close.gameObject.AddComponent<Button>();
            Tint(closeButton, closeImage, new Color(0.24f, 0.2f, 0.17f, 1f));
            closeButton.onClick.AddListener(Close);
            Label(close, "とじる", 36, Ink, Vector2.zero, new Vector2(280f, 84f), TextAnchor.MiddleCenter);

            modal.SetActive(false);
        }

        /// <summary>A labelled slider row: name on the left, the slider, its value on the right.</summary>
        Slider Row(RectTransform panel, string name, float y, float min, float max, out Text value)
        {
            Label(panel, name, 36, Ink, new Vector2(-290f, y), new Vector2(280f, 60f), TextAnchor.MiddleLeft);
            value = Label(panel, "", 34, Ink, new Vector2(370f, y), new Vector2(140f, 60f), TextAnchor.MiddleRight);

            // Fingers need a bigger knob than the mouse does.
            float knob = Application.isMobilePlatform ? 26f : 18f; // radius
            var rt = MakeRect(panel, name, new Vector2(60f, y), new Vector2(420f, knob * 2f + 12f));
            var slider = rt.gameObject.AddComponent<Slider>();
            // The whole row around the bar takes a press or a drag, not only the thin bar and the knob:
            // an invisible area 30 wider on each side and about as tall as the row.
            var hit = MakeRect(rt, "Hit Area", Vector2.zero, Vector2.zero);
            Stretch(hit);
            float pad = (88f - rt.sizeDelta.y) * 0.5f;
            hit.offsetMin = new Vector2(-30f, -pad);
            hit.offsetMax = new Vector2(30f, pad);
            hit.gameObject.AddComponent<Image>().color = Color.clear;
            var track = MakeRect(rt, "Track", Vector2.zero, Vector2.zero);
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.sizeDelta = new Vector2(0f, 10f);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0.22f, 0.2f, 0.18f, 1f);
            // The fill ends under the knob's middle (same insets as uGUI's default slider, scaled to the knob).
            var fillArea = MakeRect(rt, "Fill Area", Vector2.zero, Vector2.zero);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(knob * 0.5f, -5f);
            fillArea.offsetMax = new Vector2(-knob * 1.5f, 5f);
            var fill = MakeRect(fillArea, "Fill", Vector2.zero, new Vector2(knob, 0f));
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.66f, 0.38f, 0.24f, 1f);
            fillImage.raycastTarget = false;
            var handleArea = MakeRect(rt, "Handle Slide Area", Vector2.zero, Vector2.zero);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(knob, 0f);
            handleArea.offsetMax = new Vector2(-knob, 0f);
            var handle = MakeRect(handleArea, "Handle", Vector2.zero, new Vector2(knob * 2f, 0f));
            var handleImage = handle.gameObject.AddComponent<Image>();
            if (knobSprite == null) knobSprite = MakeKnobSprite(64);
            handleImage.sprite = knobSprite;
            handleImage.preserveAspect = true;

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            Tint(slider, handleImage, new Color(0.88f, 0.82f, 0.74f, 1f));
            return slider;
        }

        void Update()
        {
            // The gear stays faint while the cursor is captured for looking round.
            gearGroup.alpha = !ButtonShown ? 0f : IsOpen || Cursor.lockState != CursorLockMode.Locked ? 1f : 0.45f;
            gearGroup.blocksRaycasts = gearGroup.interactable = ButtonShown;
            escHint.enabled = ButtonShown && !IsOpen && Cursor.lockState == CursorLockMode.Locked && !GameInput.TouchUsed;
            // Esc opens and closes the window too. In play the cursor is captured and hidden, so the gear
            // can't be aimed at; and in the editor Esc shows the cursor while Unity still counts it as
            // locked (the gear can't be clicked and the click walks), until Open() frees it.
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !GameManager.IgnoreMouse)
            {
                if (IsOpen) Close();
                else if (ButtonShown) Open();
            }
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            volumeSlider.SetValueWithoutNotify(GameOptions.Volume);
            brightnessSlider.SetValueWithoutNotify(GameOptions.Brightness);
            lookSlider.SetValueWithoutNotify(GameOptions.LookScale);
            ShowValues();
            modal.SetActive(true);
            timeScaleBeforeOpen = Time.timeScale;
            Time.timeScale = 0f;
            if (!GameManager.IgnoreMouse)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>Back to the game. During play the cursor stays free; the next click captures it again (GameManager).</summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            closedFrame = Time.frameCount;
            modal.SetActive(false);
            Time.timeScale = timeScaleBeforeOpen;
            GameOptions.Save();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void OnDestroy()
        {
            if (IsOpen) Time.timeScale = timeScaleBeforeOpen;
        }

        void ShowValues()
        {
            volumeValue.text = $"{Mathf.RoundToInt(GameOptions.Volume * 100f)}%";
            brightnessValue.text = $"{Mathf.RoundToInt(GameOptions.Brightness * 100f)}%";
            lookValue.text = GameOptions.LookScale.ToString("0.0");
        }

        // ------------------------------------------------------------------ building blocks

        static void Tint(Selectable s, Graphic target, Color normal)
        {
            target.color = Color.white;
            s.targetGraphic = target;
            s.navigation = new Navigation { mode = Navigation.Mode.None };
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = normal;
            c.selectedColor = normal; // no lingering highlight after a click
            c.highlightedColor = Color.Lerp(normal, Color.white, 0.45f);
            c.pressedColor = Color.white;
            c.fadeDuration = 0.08f;
            s.colors = c;
        }

        Text Label(Transform parent, string text, int size, Color color, Vector2 pos, Vector2 box, TextAnchor align)
        {
            var t = MakeRect(parent, "Text", pos, box).gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static RectTransform MakeRect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>A white eight-toothed gear with a hole, anti-aliased (tinted by the button).</summary>
        static Texture2D MakeGearTexture(int size)
        {
            var px = new Color32[size * size];
            float c = size * 0.5f, aa = 1.5f / c;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - c, y + 0.5f - c) / c;
                float r = p.magnitude;
                // Teeth: the rim steps out where cos(8θ) is high, with sloped sides.
                float tooth = Mathf.Clamp01((Mathf.Cos(Mathf.Atan2(p.y, p.x) * 8f) - 0.1f) * 3f);
                float outer = Mathf.Lerp(0.68f, 0.94f, tooth);
                float a = Mathf.Clamp01((outer - r) / aa + 0.5f) * Mathf.Clamp01((r - 0.3f) / aa + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SettingsGear", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>A white anti-aliased disc for the slider knobs.</summary>
        static Sprite MakeKnobSprite(int size)
        {
            var px = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - c, y + 0.5f - c).magnitude;
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(c - 1f - d + 0.5f) * 255f));
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SettingsKnob", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
