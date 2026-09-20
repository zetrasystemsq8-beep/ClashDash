using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

namespace Zetra.ClashDash
{
    /// <summary>
    /// Runs automatically (no scene setup needed). Applies mobile platform settings once and, whenever a scene
    /// containing a GameManager loads, attaches audio, VFX, screen effects, pause menu and progression tracking.
    /// </summary>
    public static class ClashDashBootstrap
    {
        private static GameManager attachedTo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            AudioListener.volume = ProgressStore.SoundEnabled ? 1f : 0f;

            attachedTo = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;

            GameManager gm = GameManager.Instance;
            if (gm == null || gm == attachedTo) return;

            PlayerController player = FindOne<PlayerController>();
            if (player == null) return;

            attachedTo = gm;

            GameObject root = new GameObject("ClashDashSystems");
            Camera cam = Camera.main;

            root.AddComponent<ArenaAudio>().Bind(gm, player);
            root.AddComponent<ArenaVfx>().Bind(gm, player);

            ScreenFx fx = root.AddComponent<ScreenFx>();
            fx.Bind(gm, player, cam);

            root.AddComponent<PauseMenu>().Bind(gm);

            int xpBefore = ProgressStore.TotalXp;
            int levelBefore = ProgressStore.Level;
            gm.onFinished.AddListener(() => OnArenaFinished(fx, xpBefore, levelBefore));
        }

        private static void OnArenaFinished(ScreenFx fx, int xpBefore, int levelBefore)
        {
            ProgressStore.MarkArenaCompleted(ProgressStore.Arena01);
            ProgressStore.AddRunCompleted();

            int gained = Mathf.Max(0, ProgressStore.TotalXp - xpBefore);
            int level = ProgressStore.Level;

            string text = "+" + gained + " XP";
            if (level > levelBefore) text += "     LEVEL UP  -  LEVEL " + level;
            if (fx != null) fx.ShowToast(text, 4.5f);
        }

        internal static T FindOne<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }

    /// <summary>Small runtime UI toolkit shared by the pause menu, main menu and screen effects (no assets needed).</summary>
    internal static class ClashUi
    {
        public static readonly Color Cyan = new Color(0.45f, 0.92f, 1f, 1f);
        public static readonly Color Gold = new Color(1f, 0.86f, 0.5f, 1f);
        public static readonly Color Ink = new Color(0.02f, 0.08f, 0.18f, 1f);

        private static Font font;
        private static Sprite rounded;
        private static Sprite circle;

        public static Font Font
        {
            get
            {
                if (font == null)
                {
#if UNITY_2022_2_OR_NEWER
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
                }
                return font;
            }
        }

        public static Sprite Rounded
        {
            get
            {
                if (rounded == null) rounded = MakeSprite(64, 18, true);
                return rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null) circle = MakeSprite(128, 64, false);
                return circle;
            }
        }

        private static Sprite MakeSprite(int size, int radius, bool sliced)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);

            Vector4 border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                         Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            return Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        public static Text Label(RectTransform rt, string content, int size, TextAnchor alignment, Color color, bool outline)
        {
            Text text = rt.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            if (outline)
            {
                Outline o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.02f, 0.06f, 0.16f, 0.85f);
                o.effectDistance = new Vector2(3f, -3f);
            }
            return text;
        }

        public static Image Panel(RectTransform rt, Color color, bool roundedCorners)
        {
            Image image = rt.gameObject.AddComponent<Image>();
            if (roundedCorners)
            {
                image.sprite = Rounded;
                image.type = Image.Type.Sliced;
            }
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Button MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 position,
                                        Vector2 size, Color color, UnityAction onClick, int fontSize)
        {
            RectTransform rt = Rect(name, parent, anchor, anchor, anchor, position, size);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = color;

            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            RectTransform labelRt = Stretch("Label", rt);
            Label(labelRt, label, fontSize, TextAnchor.MiddleCenter, Ink, false);

            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }
    }
}
