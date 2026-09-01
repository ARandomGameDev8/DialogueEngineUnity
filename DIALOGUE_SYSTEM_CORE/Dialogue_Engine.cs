using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// ── Enums ─────────────────────────────────────────────────────────────────────
public enum PortraitMode         { None, Single, Dual }      // Single = uni mode, Dual = duel mode
public enum PortraitPlacement    { Inside, OnBorder, Outside, CharacterPanel }
public enum CharacterPanelOrder  { ImageTop, NameTop, ImageLeft, NameLeft }
public enum CharacterPanelSizeMode { Default, Custom, Content }
public enum CharacterImagePanelShape { Rectangle, Rounded, Circle }
public enum PortraitShape        { Circle, Square, Rectangle, Rounded }
public enum ToolbarSlideDirection { Top, Bottom, Left, Right }
public enum PanelSizeMode        { Percent, Pixels }
public enum BackgroundMode       { Colour, Image }
public enum ImageScaleMode       { Stretch, Tile }           // how an image fills its area
public enum TiledAnimDirection   { None, Left, Right, Up, Down }
public enum NamePosition         { Above, Below, Left, Right } // name relative to the portrait image
public enum LetterMode           { Normal, Wave, Zigzag, Staircase, Shake, FadeIn, Bounce }
public enum TextVAnchor           { Top, Center, Bottom }
public enum TextHAnchor           { Left, Center, Right }
public enum PortraitDisplayType  { Figure, Icon }            // Figure = whole image fitted, Icon = image fills the shape
public enum PortraitFillMode     { Fit, FillCrop, Stretch }  // Fit = contain, FillCrop = cover, Stretch = 100%

// ── Tiled / animated image settings (main background, borders, icon borders) ──
[Serializable]
public class TiledImageSettings
{
    public Sprite               sprite;
    [Tooltip("Absolute file path fallback — loaded at runtime if Sprite is empty.")]
    public string               path;
    [Tooltip("Multiply the image's pixels by a colour (white = untouched).")]
    public bool                 tintEnabled = false;
    public Color                tintColour   = Color.white;
    public ImageScaleMode       scaleMode    = ImageScaleMode.Tile;
    public bool                 animate      = false;
    public TiledAnimDirection   animDirection = TiledAnimDirection.Left;
    public float                animSpeed    = 30f;          // world px per second
    public bool                 loop         = true;         // wrap around forever (false = stop at the end)
    [Range(0.1f, 8f)] public float tileScale = 1f;           // multiplier for Tile-mode tile size
}

// ── History entry ─────────────────────────────────────────────────────────────
[Serializable]
public class DialogueHistoryEntry
{
    public string speaker;
    public string text;
}

// ── Unresolved portrait entry ─────────────────────────────────────────────────
[Serializable]
public class UnresolvedPortrait
{
    public string key;
    public Sprite sprite;
    public string path;
}

// ── Dirty script entry ────────────────────────────────────────────────────────
[Serializable]
public class DirtyScriptEntry
{
    public string       path;
    public List<string> unresolvedKeys = new List<string>();
}

// ── Preset data (sidecar .json stored next to the preset .uxml) ───────────────
// The UXML stores all style values. The JSON stores everything else that a
// UXML file cannot hold: sprite/font/panel-settings asset references (by GUID)
// and the animation/behaviour settings. Both are written by "Save As Preset".
[Serializable]
public class DialoguePresetDTO
{
    // Panel
    public string panelSettingsGuid = "";
    public int    panelWidthMode;   public float panelWidthValue = 90f;
    public int    panelHeightMode;  public float panelHeightValue = 30f;
    public float  panelOffsetX, panelOffsetY;
    public int    padLeft = 28, padRight = 28, padTop = 20, padBottom = 20;

    // Background
    public int   backgroundMode;
    public Color backgroundColour;
    public string backgroundSpriteGuid = "";  public int backgroundScaleMode;
    public bool  backgroundAnimate;           public int  backgroundAnimDir;
    public float backgroundAnimSpeed = 30f;   public bool backgroundLoop = true;
    public float backgroundTileScale = 1f;

    // Border
    public float borderWidth = 1f;            public Color borderColour;
    public float borderRadiusTL = 12f, borderRadiusTR = 12f, borderRadiusBL = 12f, borderRadiusBR = 12f;
    public string borderSpriteGuid = "";      public int borderScaleMode;
    public bool  borderAnimate;               public int  borderAnimDir;
    public float borderAnimSpeed = 30f;       public bool borderLoop = true;
    public float borderTileScale = 1f;

    // Speaker name
    public Color  nameColour;                 public int  nameFontSize = 18;
    public bool   nameUppercase = true;       public string nameFontGuid = "";
    public int    namePosition;               public float nameDistance = 6f;
    public int    nameLetterMode;             public float nameLetterAmplitude = 6f;
    public float  nameLetterFrequency = 0.6f; public float nameLetterSpacing = 0f;
    public float  nameLetterPhase;            public float nameLetterAnimationSpeed = 2f;

    // Dialogue text
    public Color  textColour;                 public int  textFontSize = 15;
    public string textFontGuid = "";
    public int    textLetterMode;             public float textLetterAmplitude = 6f;
    public float  textLetterFrequency = 0.6f; public float textLetterSpacing = 0f;
    public float  textLetterPhase;            public float textLetterAnimationSpeed = 2f;

    // Typewriter
    public bool  enableTypewriter = true;     public float typewriterSpeed = 0.03f;
    public float typewriterStartDelay;

    // Portrait
    public bool  showPortrait = true;         public int portraitMode;
    public int   portraitPlacement;           public int portraitShape;
    public int   portraitDisplayType;         public int portraitFillMode;
    public float portraitSize = 96f;          public bool dynamicPortraitSize;
    public float maxPortraitSize = 256f;      public float portraitOffsetX, portraitOffsetY;
    public bool  portraitFlipHorizontal;
    public bool  showPortraitWhenEmpty;
    public Color portraitBorderColour;        public bool  showPortraitBorder = true;
    public float portraitBorderWidth = 1f;    public float portraitBorderRadius = 8f;
    public string portraitBorderSpriteGuid = ""; public int portraitBorderScaleMode;
    public bool  portraitBorderAnimate;       public int  portraitBorderAnimDir;
    public float portraitBorderAnimSpeed = 30f; public bool portraitBorderLoop = true;
    public float portraitBorderTileScale = 1f;
    public float activePortraitOpacity = 1f;  public float inactivePortraitOpacity = 0.4f;
    public Color inactiveTintColour = new Color(0.5f, 0.5f, 0.5f, 1f);

    // Advance hint
    public bool   showAdvanceHint = true;     public string advanceHintText = "SPACE  /  ENTER";
    public Color  hintColour;                 public int hintFontSize = 10;

    // Toolbar
    public bool  showToolbar = true;          public bool showSettingsButton = true;
    public int   toolbarSlideDirection;

    // File-path fallbacks + tints for the image layers
    public string backgroundSpritePath = "";
    public string borderSpritePath = "";
    public string portraitBorderSpritePath = "";
    public bool   backgroundTintEnabled;         public Color backgroundTintColour = Color.white;
    public bool   borderTintEnabled;             public Color borderTintColour = Color.white;
    public bool   portraitBorderTintEnabled;     public Color portraitBorderTintColour = Color.white;

    // Text anchoring (VN-style)
    public int textVAnchor = 1;  // Center
    public int textHAnchor = 0;  // Left

    // Character figure panel
    public int   characterPanelDataVersion;
    public bool  characterPanelShowImagePanel = true;
    public bool  characterPanelShowNamePanel  = true;
    public int   characterPanelOrder;
    public int   characterPanelWidthMode;
    public float characterPanelWidth = 240f;
    public int   characterPanelHeightMode;
    public float characterPanelHeight = 420f;
    public bool  characterPanelShowBackground;
    public bool  characterPanelShowBorder;
    public Color characterPanelBg;
    public Color characterPanelBorderColour;
    public float characterPanelBorderWidth = 1f;
    public float characterPanelRadius = 10f;
    public int   charPanelPadL = 12, charPanelPadR = 12, charPanelPadT = 12, charPanelPadB = 12;
    public float characterPanelSpacing = 8f;
    public Color characterImagePanelBg;
    public int   characterImagePanelShape;
    public bool  characterImagePanelTransparentWithImage = true;
    public bool  characterImagePanelShowBorder = true;
    public Color characterImagePanelBorderColour;
    public float characterImagePanelBorderWidth;
    public float characterImagePanelRadius = 8f;
    public int   charImagePadL = 8, charImagePadR = 8, charImagePadT = 8, charImagePadB = 8;
    public bool  characterNamePanelShowBackground = true;
    public Color characterNamePanelBg;
    public int   characterNamePanelShape;
    public int   characterNamePanelHeightMode;
    public float characterNamePanelHeight = 24f;
    public bool  characterNamePanelShowBorder = true;
    public Color characterNamePanelBorderColour;
    public float characterNamePanelBorderWidth;
    public string characterNameBorderSpriteGuid = "";
    public string characterNameBorderSpritePath = "";
    public int    characterNameBorderScaleMode;
    public bool   characterNameBorderAnimate;
    public int    characterNameBorderAnimDir;
    public float  characterNameBorderAnimSpeed = 30f;
    public bool   characterNameBorderLoop = true;
    public float  characterNameBorderTileScale = 1f;
    public bool   characterNameBorderTintEnabled;
    public Color  characterNameBorderTintColour = Color.white;
    public float characterNamePanelRadius = 8f;
    public int   charNamePadL = 8, charNamePadR = 8, charNamePadT = 6, charNamePadB = 6;

    // Placeholder + interaction
    public bool   useDefaultPortraitPlaceholder = true;
    public string defaultPortraitSpriteGuid = "";
    public string defaultPortraitPath = "";
    public bool   clickToAdvance = true;
}

[DefaultExecutionOrder(-100)]
public class Dialogue_Engine : MonoBehaviour, IDialogueService
{
    public static Dialogue_Engine Instance { get; private set; }
    static readonly Dictionary<Action<string>, List<EventMonitorID>> onEmitFacadeSubscriptionIds =
        new Dictionary<Action<string>, List<EventMonitorID>>();
    public static event Action<string> OnEmit
    {
        add
        {
            if (value == null) return;
            if (Instance == null)
            {
                Debug.LogWarning("Dialogue_Engine.OnEmit subscription was added before the engine instance existed. Subscribe after the engine awakens, or use the explicit client-based Subscribe APIs.");
                return;
            }
            EventMonitorID subscriptionId = Instance.RegisterOnEmitFacade(value);
            if (subscriptionId == null || !subscriptionId.IsValid) return;
            if (!onEmitFacadeSubscriptionIds.TryGetValue(value, out List<EventMonitorID> ids))
            {
                ids = new List<EventMonitorID>();
                onEmitFacadeSubscriptionIds[value] = ids;
            }
            ids.Add(subscriptionId);
        }
        remove
        {
            if (value == null) return;
            if (!onEmitFacadeSubscriptionIds.TryGetValue(value, out List<EventMonitorID> ids) ||
                ids == null || ids.Count == 0) return;
            int last = ids.Count - 1;
            EventMonitorID subscriptionId = ids[last];
            ids.RemoveAt(last);
            if (ids.Count == 0) onEmitFacadeSubscriptionIds.Remove(value);
            if (Instance != null)
                Instance.UnregisterLiveEventSubscription(subscriptionId);
        }
    }
    public static IDialogueService Service { get { return Instance; } }
    public DialogueQueryServer QueryServer { get { return queryServer; } }
    public DialogueLiveSnapshotServer LiveSnapshotServer { get { return liveSnapshotServer; } }
    public DialogueLiveEventServer LiveEventServer { get { return liveEventServer; } }
    public DialoguePriorityLiveEventServer PriorityLiveEventServer { get { return priorityLiveEventServer; } }

    public static DialogueResponse SendRequest(DialogueRequest request)
    {
        return Instance != null
            ? Instance.Send(request)
            : new DialogueResponse
            {
                RequestId = request != null ? request.RequestId : "",
                Code = DialogueResponseCode.NotFound,
                Message = "<error>Dialogue_Engine service is not running.</error>"
            };
    }

    // Preferred coalesced one-shot query overload for Unity objects. The
    // caller identity is derived automatically from the engine's Unity-object
    // key helper, using the modern EntityId path on newer Unity versions.
    public static DialogueResponse SendRequest(UnityEngine.Object caller,
                                               DialogueRequest request)
    {
        return Instance != null
            ? Instance.SendRequestForCaller(caller, request)
            : new DialogueResponse
            {
                RequestId = request != null ? request.RequestId : "",
                Code = DialogueResponseCode.NotFound,
                Message = "<error>Dialogue_Engine service is not running.</error>"
            };
    }

    // Preferred coalesced one-shot query overload for plain C# systems that
    // already own a stable client key.
    public static DialogueResponse SendRequest(string clientId,
                                               DialogueRequest request)
    {
        return Instance != null
            ? Instance.SendRequestForClient(clientId, request)
            : new DialogueResponse
            {
                RequestId = request != null ? request.RequestId : "",
                Code = DialogueResponseCode.NotFound,
                Message = "<error>Dialogue_Engine service is not running.</error>"
            };
    }

    public static SnaphotSubID SubscribeLiveSnapshots(UnityEngine.Object client,
        Action<DialogueLiveSnapshot> callback, string dialoguePathFilter = "",
        bool onlyOnChange = true, float minIntervalSeconds = 0f)
    {
        string clientId;
        return TryResolveClientId(client, "SubscribeLiveSnapshots", out clientId)
            ? SubscribeLiveSnapshots(clientId, callback, dialoguePathFilter,
                onlyOnChange, minIntervalSeconds)
            : null;
    }

    public static SnaphotSubID SubscribeLiveSnapshots(string clientId,
        Action<DialogueLiveSnapshot> callback, string dialoguePathFilter = "",
        bool onlyOnChange = true, float minIntervalSeconds = 0f)
    {
        string resolvedClientId;
        return TryResolveClientId(clientId, "SubscribeLiveSnapshots", out resolvedClientId) && Instance != null
            ? Instance.RegisterLiveSnapshotSubscription(callback, resolvedClientId,
                dialoguePathFilter, onlyOnChange, minIntervalSeconds)
            : null;
    }

    public static EventMonitorID SubscribeLiveEvents(UnityEngine.Object client,
        Action<string> callback, string dialoguePathFilter = "",
        string eventNameFilter = "")
    {
        string clientId;
        return TryResolveClientId(client, "SubscribeLiveEvents", out clientId)
            ? SubscribeLiveEvents(clientId, callback, dialoguePathFilter,
                eventNameFilter)
            : null;
    }

    public static EventMonitorID SubscribeLiveEvents(string clientId,
        Action<string> callback, string dialoguePathFilter = "",
        string eventNameFilter = "")
    {
        string resolvedClientId;
        return TryResolveClientId(clientId, "SubscribeLiveEvents", out resolvedClientId) && Instance != null
            ? Instance.RegisterLiveEventSubscription(callback, resolvedClientId,
                dialoguePathFilter, eventNameFilter)
            : null;
    }

    // Friendly live-event overloads. Lambdas naturally capture any fields or
    // locals from the caller, so gameplay code can pass "any arguments" by
    // closure without expanding the engine API surface.
    public static EventMonitorID Subscribe(UnityEngine.Object client, Action callback)
    {
        return Subscribe(client, "", callback);
    }

    public static EventMonitorID Subscribe(UnityEngine.Object client, string targetEvent,
        Action callback)
    {
        string clientId;
        return TryResolveClientId(client, "Subscribe", out clientId)
            ? Subscribe(clientId, targetEvent, callback)
            : null;
    }

    public static EventMonitorID Subscribe(string clientId, Action callback)
    {
        return Subscribe(clientId, "", callback);
    }

    public static EventMonitorID Subscribe(string clientId, string targetEvent,
        Action callback)
    {
        string resolvedClientId;
        return TryResolveClientId(clientId, "Subscribe", out resolvedClientId) && Instance != null
            ? Instance.RegisterLiveEventSubscription(
                callback != null ? new Action<string>(_ => callback()) : null,
                resolvedClientId, "", targetEvent)
            : null;
    }

    public static EventMonitorID Subscribe(UnityEngine.Object client, Action<string> callback)
    {
        return Subscribe(client, "", callback);
    }

    public static EventMonitorID Subscribe(UnityEngine.Object client, string targetEvent,
        Action<string> callback)
    {
        string clientId;
        return TryResolveClientId(client, "Subscribe", out clientId)
            ? Subscribe(clientId, targetEvent, callback)
            : null;
    }

    public static EventMonitorID Subscribe(string clientId, Action<string> callback)
    {
        return Subscribe(clientId, "", callback);
    }

    public static EventMonitorID Subscribe(string clientId, string targetEvent,
        Action<string> callback)
    {
        string resolvedClientId;
        return TryResolveClientId(clientId, "Subscribe", out resolvedClientId) && Instance != null
            ? Instance.RegisterLiveEventSubscription(callback, resolvedClientId,
                "", targetEvent)
            : null;
    }

    public static PriorityEventMonitorID SubscribePriorityLiveEvents(UnityEngine.Object client,
        Func<string, DialoguePriorityDispatchResult> callback,
        int priority, string dialoguePathFilter = "",
        string eventNameFilter = "")
    {
        string clientId;
        return TryResolveClientId(client, "SubscribePriorityLiveEvents", out clientId)
            ? SubscribePriorityLiveEvents(clientId, callback, priority,
                dialoguePathFilter, eventNameFilter)
            : null;
    }

    public static PriorityEventMonitorID SubscribePriorityLiveEvents(string clientId,
        Func<string, DialoguePriorityDispatchResult> callback,
        int priority, string dialoguePathFilter = "",
        string eventNameFilter = "")
    {
        string resolvedClientId;
        return TryResolveClientId(clientId, "SubscribePriorityLiveEvents", out resolvedClientId) && Instance != null
            ? Instance.RegisterPriorityLiveEventSubscription(callback, priority,
                resolvedClientId, dialoguePathFilter, eventNameFilter)
            : null;
    }

    public static PriorityEventMonitorID Subscribe(UnityEngine.Object client, int priority,
        Func<DialoguePriorityDispatchResult> callback)
    {
        return Subscribe(client, priority, "", callback);
    }

    public static PriorityEventMonitorID Subscribe(UnityEngine.Object client, int priority,
        string targetEvent, Func<DialoguePriorityDispatchResult> callback)
    {
        string clientId;
        return TryResolveClientId(client, "Subscribe", out clientId)
            ? Subscribe(clientId, priority, targetEvent, callback)
            : null;
    }

    public static PriorityEventMonitorID Subscribe(string clientId, int priority,
        Func<DialoguePriorityDispatchResult> callback)
    {
        return Subscribe(clientId, priority, "", callback);
    }

    public static PriorityEventMonitorID Subscribe(string clientId, int priority,
        string targetEvent, Func<DialoguePriorityDispatchResult> callback)
    {
        string resolvedClientId;
        return TryResolveClientId(clientId, "Subscribe", out resolvedClientId) && Instance != null
            ? Instance.RegisterPriorityLiveEventSubscription(
                callback != null
                    ? new Func<string, DialoguePriorityDispatchResult>(
                        _ => callback())
                    : null,
                priority, resolvedClientId, "", targetEvent)
            : null;
    }

    public static PriorityEventMonitorID Subscribe(UnityEngine.Object client, int priority,
        Func<string, DialoguePriorityDispatchResult> callback)
    {
        return Subscribe(client, priority, "", callback);
    }

    public static PriorityEventMonitorID Subscribe(UnityEngine.Object client, int priority,
        string targetEvent, Func<string, DialoguePriorityDispatchResult> callback)
    {
        string clientId;
        return TryResolveClientId(client, "Subscribe", out clientId)
            ? Subscribe(clientId, priority, targetEvent, callback)
            : null;
    }

    public static PriorityEventMonitorID Subscribe(string clientId, int priority,
        Func<string, DialoguePriorityDispatchResult> callback)
    {
        return Subscribe(clientId, priority, "", callback);
    }

    public static PriorityEventMonitorID Subscribe(string clientId, int priority,
        string targetEvent, Func<string, DialoguePriorityDispatchResult> callback)
    {
        string resolvedClientId;
        return TryResolveClientId(clientId, "Subscribe", out resolvedClientId) && Instance != null
            ? Instance.RegisterPriorityLiveEventSubscription(callback,
                priority, resolvedClientId, "", targetEvent)
            : null;
    }

    EventMonitorID RegisterOnEmitFacade(Action<string> callback)
    {
        if (callback == null) return null;
        return RegisterLiveEventSubscription(callback,
            BuildOnEmitFacadeClientId(callback), "", "");
    }

    static string BuildOnEmitFacadeClientId(Action<string> callback)
    {
        if (callback == null) return "";
        object target = callback.Target;
        if (target is UnityEngine.Object unityObject && unityObject != null)
            return "OnEmit:" + GetClientKey(unityObject);
        if (target != null)
            return "OnEmit:" + target.GetType().FullName + ":" + target.GetHashCode();
        return "OnEmit:" + callback.Method.DeclaringType.FullName + ":" + callback.Method.Name;
    }

    static bool TryResolveClientId(UnityEngine.Object client, string apiName,
                                   out string clientId)
    {
        clientId = "";
        if (client == null)
        {
            Debug.LogError("Dialogue_Engine." + apiName + " requires a non-null UnityEngine.Object client.");
            return false;
        }
        clientId = GetClientKey(client);
        return true;
    }

    static bool TryResolveClientId(string clientId, string apiName,
                                   out string resolvedClientId)
    {
        resolvedClientId = clientId != null ? clientId.Trim() : "";
        if (!string.IsNullOrEmpty(resolvedClientId)) return true;
        Debug.LogError("Dialogue_Engine." + apiName + " requires a non-empty string clientId for non-Unity callers.");
        return false;
    }

    public static void UnsubscribeLiveSnapshots(SnaphotSubID subscriptionId)
    {
        if (Instance != null)
            Instance.UnregisterLiveSnapshotSubscription(subscriptionId);
    }

    public static void UnsubscribeLiveEvents(EventMonitorID subscriptionId)
    {
        if (Instance != null)
            Instance.UnregisterLiveEventSubscription(subscriptionId);
    }

    public static void UnsubscribePriorityLiveEvents(PriorityEventMonitorID subscriptionId)
    {
        if (Instance != null)
            Instance.UnregisterPriorityLiveEventSubscription(subscriptionId);
    }

    public static void UnsubscribeAllClientSubscriptions(string clientId)
    {
        if (Instance != null)
            Instance.UnregisterAllClientSubscriptions(clientId);
    }

    // ─── Paths ────────────────────────────────────────────────────────────────
    // The project root is the Unity project folder, e.g. /home/george/GameTut
    public const string PRESETS_PATH = "Assets/Scripts/Dialogue_Presets";
    public const string UXML_PATH    = "Assets/Scripts/Dialogue_Presets/dialogue_generated.uxml";
    public const string GENERATED_FILE_NAME = "dialogue_generated";
    // Play-mode isolation: the runtime only ever instantiates this disposable
    // copy of the current UXML. It is deleted when play mode ends, so nothing
    // the runtime changed can leak back into the source layout.
    public const string RUNTIME_UXML_PATH = "Assets/Scripts/Dialogue_Presets/dialogue_runtime_copy.uxml";

    // True while playing with a visual layout asset: the runtime UXML was built
    // straight from the asset's resolved geometry, so the engine must not
    // restyle the box/portraits from its own approximated fields.
    bool visualLayoutRuntimeActive;

    // ─── Preset ───────────────────────────────────────────────────────────────
    [Header("Preset")]
    [Tooltip("Name of a preset UXML file inside Dialogue_Presets (without extension). Leave empty to use the fields below (generated layout).")]
    public string presetName = "";

    [Header("Visual Layout Asset (Phase 2)")]
    [Tooltip("Optional DialogueLayoutAsset bridge. When enabled, its phase-2 supported fields are applied onto the runtime UI before UXML generation/build.")]
    public bool useVisualLayoutAsset = false;
    public DialogueLayoutAsset visualLayoutAsset;
    [HideInInspector] public DialogueAnchorPreset layoutAssetAnchorPreset = DialogueAnchorPreset.Bottom;
    [HideInInspector] public DialogueCustomAnchorDefinition layoutAssetCustomAnchor = new DialogueCustomAnchorDefinition();

    // ─── Panel ────────────────────────────────────────────────────────────────
    [Header("Panel")]
    public PanelSettings panelSettings;

    [Header("Panel Size & Position")]
    public PanelSizeMode panelWidthMode  = PanelSizeMode.Percent;
    [Range(1f, 100f)] public float panelWidthValue  = 90f;    // % or px depending on mode
    public PanelSizeMode panelHeightMode = PanelSizeMode.Percent;
    [Range(1f, 100f)] public float panelHeightValue = 30f;    // % or px depending on mode
    [Range(-500f, 500f)] public float panelOffsetX = 0f;      // px shift from the anchored position
    [Range(-500f, 500f)] public float panelOffsetY = 0f;
    public RectOffset padding;

    // ─── Background ───────────────────────────────────────────────────────────
    [Header("Background")]
    public BackgroundMode backgroundMode = BackgroundMode.Colour;
    public Color backgroundColour = new Color(0.05f, 0.05f, 0.05f, 0.92f);
    [Tooltip("Image background: repeating (Tile), looping and animating are supported.")]
    public TiledImageSettings backgroundImage = new TiledImageSettings();

    // ─── Border ───────────────────────────────────────────────────────────────
    [Header("Box Border")]
    [Range(0f, 8f)]  public float borderWidth    = 1f;
    public Color borderColour = new Color(1f, 1f, 1f, 1f);
    [Range(0f, 32f)] public float borderRadiusTL = 12f;
    [Range(0f, 32f)] public float borderRadiusTR = 12f;
    [Range(0f, 32f)] public float borderRadiusBL = 12f;
    [Range(0f, 32f)] public float borderRadiusBR = 12f;
    [Tooltip("Image border: drawn inside the border ring only, fully tiled/looped/animated.")]
    public TiledImageSettings borderImage = new TiledImageSettings();

    // ─── Speaker Name ─────────────────────────────────────────────────────────
    [Header("Speaker Name")]
    public Color nameColour = new Color(0.98f, 0.82f, 0.44f, 1f);
    [Range(8, 64)] public int nameFontSize = 18;
    public bool nameUppercase = true;
    public Font nameFont;
    [Header("Name Position (relative to portrait image)")]
    public NamePosition namePosition = NamePosition.Above;
    [Range(0f, 64f)] public float nameDistance = 6f;
    [Header("Name Letter Behaviour")]
    public LetterMode nameLetterMode = LetterMode.Normal;
    [Range(0f, 48f)] public float nameLetterAmplitude = 6f;
    [Range(0.05f, 3f)] public float nameLetterFrequency = 0.6f;
    [Range(-8f, 32f)] public float nameLetterSpacing = 0f;
    [Range(0f, 6.28f)] public float nameLetterPhase = 0f;
    [Range(0.1f, 8f)] public float nameLetterAnimationSpeed = 2f;

    // ─── Dialogue Text ────────────────────────────────────────────────────────
    [Header("Dialogue Text")]
    public Color textColour = new Color(0.93f, 0.93f, 0.93f, 1f);
    [Range(8, 64)] public int textFontSize = 15;
    public Font textFont;
    [Header("Text Anchoring (VN-style)")]
    [Tooltip("Vertical anchor of the text inside the panel. Center = classic VN look.")]
    public TextVAnchor textVAnchor = TextVAnchor.Center;
    public TextHAnchor textHAnchor = TextHAnchor.Left;
    [Header("Letter Behaviour (per-word letter layout)")]
    public LetterMode textLetterMode = LetterMode.Normal;
    [Range(0f, 48f)] public float textLetterAmplitude = 6f;
    [Range(0.05f, 3f)] public float textLetterFrequency = 0.6f;
    [Range(-8f, 32f)] public float textLetterSpacing = 0f;
    [Range(0f, 6.28f)] public float textLetterPhase = 0f;
    [Range(0.1f, 8f)] public float textLetterAnimationSpeed = 2f;

    // ─── Typewriter ───────────────────────────────────────────────────────────
    [Header("Typewriter")]
    public bool enableTypewriter = true;
    [Range(0.005f, 0.1f)] public float typewriterSpeed = 0.03f;
    [Range(0f, 5f)] public float typewriterStartDelay = 0f;

    // ─── Portrait ─────────────────────────────────────────────────────────────
    [Header("Portrait")]
    public bool              showPortrait       = true;
    public PortraitMode      portraitMode       = PortraitMode.Single;   // uni / duel
    public PortraitPlacement portraitPlacement  = PortraitPlacement.Inside;
    public PortraitShape     portraitShape      = PortraitShape.Rounded;
    [Header("Portrait Display")]
    [Tooltip("Figure = the whole image fitted inside. Icon = a geometric shape filled by the image (cropped).")]
    public PortraitDisplayType portraitDisplayType = PortraitDisplayType.Figure;
    public PortraitFillMode  portraitFillMode   = PortraitFillMode.Fit;
    [Range(48f, 512f)] public float portraitSize = 96f;
    [Tooltip("Fit the portrait box to the image's aspect ratio instead of a fixed square.")]
    public bool dynamicPortraitSize = false;
    [Range(48f, 512f)] public float maxPortraitSize = 256f;
    [Tooltip("Extra offset of the portrait relative to its parent container (px).")]
    [Range(-300f, 300f)] public float portraitOffsetX = 0f;
    [Range(-300f, 300f)] public float portraitOffsetY = 0f;
    [Tooltip("Mirror the portrait image horizontally (e.g. a character figure facing the panel).")]
    public bool portraitFlipHorizontal = false;
    [Tooltip("When there is no image, show an empty framed box instead of hiding the portrait.")]
    public bool showPortraitWhenEmpty = false;

    [Header("Portrait Border")]
    public Color portraitBorderColour = new Color(1f, 1f, 1f, 1f);
    public bool  showPortraitBorder   = true;
    [Range(0f, 8f)]  public float portraitBorderWidth  = 1f;
    [Range(0f, 32f)] public float portraitBorderRadius = 8f;
    [Tooltip("Optional image drawn inside the portrait border ring (tiled/looped/animated).")]
    public TiledImageSettings portraitBorderImage = new TiledImageSettings();

    [Header("Portrait Opacity (duel mode)")]
    [Range(0f, 1f)] public float activePortraitOpacity   = 1f;
    [Range(0f, 1f)] public float inactivePortraitOpacity = 0.4f;
    public Color inactiveTintColour = new Color(0.5f, 0.5f, 0.5f, 1f);

    // ─── Character Figure Panel ───────────────────────────────────────────────
    const int CHARACTER_PANEL_DATA_VERSION = 2;
    [HideInInspector] [SerializeField] int characterPanelDataVersion;
    [Header("Character Panel (figure panel)")]
    [Tooltip("The figure panel sits OUTSIDE the main panel ([figure panel] [main panel]). It is segmented into an image panel and a name panel, both fully customizable.")]
    public bool characterPanelShowImagePanel = true;
    public bool characterPanelShowNamePanel  = true;
    [Tooltip("Default layout: image panel on top, name panel below.")]
    public CharacterPanelOrder characterPanelOrder = CharacterPanelOrder.ImageTop;
    [Tooltip("Default shares all screen width left by the main panel. Custom uses pixels. Content fits the panel's children.")]
    public CharacterPanelSizeMode characterPanelWidthMode = CharacterPanelSizeMode.Default;
    [Range(80f, 800f)] public float characterPanelWidth = 240f;
    [Tooltip("Default is a tall visual-novel figure panel. Custom uses pixels. Content fits the image and name.")]
    public CharacterPanelSizeMode characterPanelHeightMode = CharacterPanelSizeMode.Default;
    [Range(100f, 1000f)] public float characterPanelHeight = 420f;
    [Tooltip("The root is layout-only by default, leaving the Image and Name panels visually separate.")]
    public bool characterPanelShowBackground = false;
    public bool characterPanelShowBorder = false;
    public Color characterPanelBg = new Color(0.07f, 0.07f, 0.08f, 0.9f);
    public Color characterPanelBorderColour = new Color(0.55f, 0.55f, 0.6f, 1f);
    [Range(0f, 8f)]  public float characterPanelBorderWidth = 1f;
    [Range(0f, 32f)] public float characterPanelRadius = 10f;
    // NOTE: RectOffset derives from UnityEngine.Object, so it must NOT be
    // constructed in a field initializer (UnityException: set_left is not
    // allowed from a MonoBehaviour constructor). Defaults are created in
    // Awake instead.
    public RectOffset characterPanelPadding;
    [Range(0f, 32f)] public float characterPanelSpacing = 8f;

    [Header("Character Panel — Image Panel")]
    public CharacterImagePanelShape characterImagePanelShape = CharacterImagePanelShape.Rectangle;
    [Tooltip("Use a transparent image-panel background while a portrait is loaded; the custom background is used for an empty panel.")]
    public bool characterImagePanelTransparentWithImage = true;
    public Color characterImagePanelBg = new Color(0.10f, 0.10f, 0.12f, 1f);
    public bool characterImagePanelShowBorder = true;
    public Color characterImagePanelBorderColour = new Color(0.45f, 0.45f, 0.5f, 1f);
    [Range(0f, 8f)]  public float characterImagePanelBorderWidth = 0f;
    [Range(0f, 32f)] public float characterImagePanelRadius = 8f;
    public RectOffset characterImagePanelPadding;

    [Header("Character Panel — Name Panel")]
    public bool characterNamePanelShowBackground = true;
    public Color characterNamePanelBg = new Color(0.05f, 0.05f, 0.06f, 0.96f);
    public CharacterImagePanelShape characterNamePanelShape = CharacterImagePanelShape.Rounded;
    [Tooltip("Default reserves a clearly visible lower partition. Custom uses pixels. Content hugs the name text.")]
    public CharacterPanelSizeMode characterNamePanelHeightMode = CharacterPanelSizeMode.Default;
    [Range(8f, 300f)] public float characterNamePanelHeight = 24f;
    public bool characterNamePanelShowBorder = true;
    public Color characterNamePanelBorderColour = new Color(0.45f, 0.45f, 0.5f, 1f);
    [Range(0f, 8f)]  public float characterNamePanelBorderWidth = 1f;
    [Range(0f, 256f)] public float characterNamePanelRadius = 8f;
    [Tooltip("Optional image drawn only in the Name Panel border ring. It replaces the colour border.")]
    public TiledImageSettings characterNamePanelBorderImage = new TiledImageSettings();
    public RectOffset characterNamePanelPadding;

    [Header("Default Portrait Placeholder")]
    [Tooltip("When no portrait image is loaded, show a shaded unidentified-character silhouette (or your own sprite / file).")]
    public bool   useDefaultPortraitPlaceholder = true;
    public Sprite defaultPortraitSprite;
    public string defaultPortraitPath = "";

    [Header("Interaction")]
    [Tooltip("Click on the dialogue box to advance (or complete the typewriter).")]
    public bool clickToAdvance = true;

    // ─── Advance Hint ─────────────────────────────────────────────────────────
    [Header("Advance Hint")]
    public bool   showAdvanceHint = true;
    public string advanceHintText = "SPACE  /  ENTER";
    public Color  hintColour      = new Color(1f, 1f, 1f, 0.35f);
    [Range(6, 24)] public int hintFontSize = 10;

    // ─── Toolbar / History / Settings ─────────────────────────────────────────
    [Header("Toolbar & History")]
    public bool                 showToolbar           = true;
    public bool                 showSettingsButton    = true;
    public ToolbarSlideDirection toolbarSlideDirection = ToolbarSlideDirection.Bottom;

    // ─── Unresolved portraits & dirty scripts ─────────────────────────────────
    [Header("Unresolved Portraits")]
    [SerializeField] public List<UnresolvedPortrait> portraits = new List<UnresolvedPortrait>();
    [Header("Dirty Scripts")]
    [Tooltip("Scripts whose last compile produced unresolved portrait placeholders.")]
    [SerializeField] public List<DirtyScriptEntry> dirtyScripts = new List<DirtyScriptEntry>();
    [HideInInspector] [SerializeField] string lastLoadedScript = "";

    // ─── Internals ─────────────────────────────────────────────────────────────
    UIDocument document;

    VisualElement rowContainer;
    VisualElement box;
    VisualElement backgroundLayer;
    VisualElement borderLayer;

    VisualElement insideLeftWrapper,  insideRightWrapper;
    VisualElement insideLeftHost,     insideRightHost;
    VisualElement frameInsideLeft,    frameInsideRight;
    VisualElement portraitInsideLeft, portraitInsideRight;
    VisualElement overlayInsideLeft,  overlayInsideRight;
    VisualElement nameInsideLeft,     nameInsideRight;

    VisualElement outsideLeftWrapper,  outsideRightWrapper;
    VisualElement outsideLeftHost,     outsideRightHost;
    VisualElement frameOutsideLeft,    frameOutsideRight;
    VisualElement portraitOutsideLeft, portraitOutsideRight;
    VisualElement overlayOutsideLeft,  overlayOutsideRight;
    VisualElement nameOutsideLeft,     nameOutsideRight;

    VisualElement borderLeftWrapper,  borderRightWrapper;
    VisualElement borderLeftHost,     borderRightHost;
    VisualElement frameBorderLeft,    frameBorderRight;
    VisualElement portraitBorderLeft, portraitBorderRight;
    VisualElement overlayBorderLeft,  overlayBorderRight;
    VisualElement nameBorderLeft,     nameBorderRight;

    // Character figure panels (outside, segmented into image + name panels)
    VisualElement charLeftWrapper,  charRightWrapper;
    VisualElement charLeftHost, charRightHost;
    VisualElement frameCharLeft, frameCharRight;
    VisualElement portraitCharLeft, portraitCharRight;
    VisualElement overlayCharLeft, overlayCharRight;
    VisualElement nameCharLeft, nameCharRight;
    VisualElement charLeftFigure, charRightFigure;
    VisualElement charLeftImagePanel, charRightImagePanel;
    VisualElement charLeftNamePanel, charRightNamePanel;
    VisualElement charLeftNameBorderOverlay, charRightNameBorderOverlay;

    ScrollView    textScroll;
    Label         dialogueTextLabel;
    Label         advanceHintLabel;
    VisualElement choiceContainer;

    // Visual-layout runtime choice UI: the layout's own choice panel
    // (ChoicePanel / ChoiceSlot{i} / ChoiceLabel{i}), designed in the visual
    // editor. The engine only writes option texts, shows/hides and handles
    // clicks — every rect and style belongs to the layout.
    VisualElement choicePanelRoot;
    readonly List<VisualElement> visualChoiceButtons = new List<VisualElement>();
    readonly List<Label> visualChoiceButtonTexts = new List<Label>();

    VisualElement toolbarPanel;
    VisualElement historyPanel;
    ScrollView    historyContent;
    VisualElement settingsPanel;
    ScrollView    settingsContent;
    Button        toolbarToggleButton, historyButton, settingsButton, rewindButton;
    Button        closeHistoryButton, closeSettingsButton;

    bool toolbarVisible = false;
    bool layoutApplied  = false;

    // ─── Professional polish state ────────────────────────────────────────────
    List<Button>      choiceButtons  = new List<Button>();
    List<OptionToken> choiceOptions  = new List<OptionToken>();
    int               choiceHighlight = -1;

    IVisualElementScheduledItem openTween, hintPulseTask, caretBlinkTask;
    IVisualElementScheduledItem slotTween0, slotTween1;
    float slotCur0 = 1f, slotCur1 = 1f;
    float hintPhase = 0f;
    bool  caretOn   = true;
    string shownText = "";

    // Dual portrait slot ownership
    // ─── Visual-layout runtime cast slots ────────────────────────────────────
    // One per image/name panel pair in the editor layout, INDEXED in layout
    // order: the k-th speaker (order of first appearance) owns the k-th pair.
    // Current speaker at activePortraitOpacity; interrupted speakers greyed
    // (inactivePortraitOpacity + inactiveTintColour, both adjustable).
    public class VisualRuntimeSlot
    {
        public VisualElement wrapper, panel, frame, portrait, name;
        public bool hidePanelWhenEmpty;
        public string owner;
        public float opacity = 1f;
        public IVisualElementScheduledItem tween;
    }
    List<VisualRuntimeSlot> visualRuntimeSlots = new List<VisualRuntimeSlot>();

    string[] slotOwner = new string[2] { null, null };

    // History — array-backed list for O(1) indexed access
    List<DialogueHistoryEntry> history = new List<DialogueHistoryEntry>();

    // ─── Volatile play-session database / service state ─────────────────────
    public DialogueRuntimeDatabase RuntimeDatabase { get; private set; }
    string currentDialogueId = "";
    string currentDialoguePath = "";
    string currentTextName = "";
    string currentServiceText = "";
    string lastEmittedEvent = "";
    DialogueRuntimeStatus runtimeStatus = DialogueRuntimeStatus.Idle;
    string runtimeDetail = "Not playing";
    [Header("Service Scheduling")]
    [Tooltip("Maximum distinct query clients whose latest queued one-shot request is resolved per frame.")]
    [Min(1)] public int maxAsyncQueryClientsPerFrame = 8;
    [Tooltip("Maximum deferred frames allowed for coalesced SendRequest(this, request) calls before they fail.")]
    [Min(0)] public int maxCoalescedSendRequestRetries = 4;
    readonly DialogueQueryServer queryServer = new DialogueQueryServer();
    readonly DialogueLiveSnapshotServer liveSnapshotServer = new DialogueLiveSnapshotServer();
    readonly DialogueLiveEventServer liveEventServer = new DialogueLiveEventServer();
    readonly DialoguePriorityLiveEventServer priorityLiveEventServer = new DialoguePriorityLiveEventServer();

    // ─── Graph & Traversal State ──────────────────────────────────────────────
    DialogueGraph graph;
    SectionToken  currentSection;
    int           currentIndex;
    Stack<(SectionToken section, int index)> sectionStack = new Stack<(SectionToken section, int index)>();

    bool        isOpen    = false;
    public bool isSuccess = false;
    bool currentDialogueInterruptible;
    bool currentDialogueSaveState;
    CharacterToken currentCharacterToken;
    ChoiceToken currentChoiceToken;
    CharacterToken[] slotTokens = new CharacterToken[2];

    sealed class DialoguePlaybackState
    {
        public DialogueGraph graph;
        public SectionToken currentSection;
        public int currentIndex;
        public List<(SectionToken section, int index)> traversal;
        public string dialogueId, dialoguePath, textName, serviceText;
        public string fullText, shownText, lastEvent, detail;
        public DialogueRuntimeStatus status;
        public List<DialogueHistoryEntry> history;
        public List<UnresolvedPortrait> portraits;
        public string lastLoadedScript;
        public string[] slotOwners;
        public CharacterToken[] slotTokens;
        public CharacterToken currentCharacter;
        public ChoiceToken currentChoice;
        public bool interruptible, saveState;
    }

    readonly Stack<DialoguePlaybackState> suspendedDialogues =
        new Stack<DialoguePlaybackState>();

    public bool IsPlaying { get { return isOpen; } }
    public bool CurrentDialogueInterruptible { get { return currentDialogueInterruptible; } }
    public int SuspendedDialogueCount { get { return suspendedDialogues.Count; } }

    // Typewriter
    Coroutine typewriterCoroutine;
    bool      isTyping        = false;
    string    currentFullText = "";

    // ─── Animated image layers ────────────────────────────────────────────────
    class TilerRuntime
    {
        public VisualElement clip, mover;
        public List<VisualElement> tiles = new List<VisualElement>();
        public TiledImageSettings settings;
        public Vector2 clipSize, tileSize;   // tileSize == clipSize when stretch
        public float   offset;
        public bool    stretch;
        public bool    finished;             // non-loop animation reached the end
        public IVisualElementScheduledItem sched; // owning Every() task — paused on rebuild
        public void Tick(float dt)
        {
            if (finished || settings == null || !settings.animate || clip == null) return;
            offset += settings.animSpeed * dt;
            float range = stretch ? (settings.animDirection == TiledAnimDirection.Left || settings.animDirection == TiledAnimDirection.Right ? clipSize.x : clipSize.y)
                                  : (settings.animDirection == TiledAnimDirection.Left || settings.animDirection == TiledAnimDirection.Right ? tileSize.x : tileSize.y);
            if (range <= 0f) return;
            if (settings.loop) offset %= range;
            else if (offset >= range) { offset = range; finished = true; }
            Apply();
        }
        public void Apply()
        {
            float dx = 0f, dy = 0f;
            float p = offset;
            if (settings.animDirection == TiledAnimDirection.Left)  dx = -p;
            if (settings.animDirection == TiledAnimDirection.Right) dx = p - (stretch ? clipSize.x : tileSize.x);
            if (settings.animDirection == TiledAnimDirection.Up)    dy = -p;
            if (settings.animDirection == TiledAnimDirection.Down)  dy = p - (stretch ? clipSize.y : tileSize.y);
            if (stretch)
            {
                // Two stretched tiles wrap around seamlessly-ish
                for (int i = 0; i < tiles.Count && i < 2; i++)
                {
                    float x = (settings.animDirection == TiledAnimDirection.Left || settings.animDirection == TiledAnimDirection.Right)
                        ? -p + i * clipSize.x : 0f;
                    float y = (settings.animDirection == TiledAnimDirection.Up || settings.animDirection == TiledAnimDirection.Down)
                        ? -p + i * clipSize.y : 0f;
                    tiles[i].style.left = x;
                    tiles[i].style.top  = y;
                }
            }
            else if (mover != null)
            {
                mover.style.translate = new Translate(dx, dy, 0);
            }
        }
    }

    readonly Dictionary<VisualElement, TilerRuntime> tilers = new Dictionary<VisualElement, TilerRuntime>();
    readonly Dictionary<VisualElement, Vector2>      layerSizes = new Dictionary<VisualElement, Vector2>();
    Vector2 lastBoxSize;

    // ──────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        // Intentionally memory-only. A fresh database is created for every Play
        // Mode lifetime and disappears with this engine/scene.
        RuntimeDatabase = new DialogueRuntimeDatabase();
        EnsureCharacterPanelDefaults();
        if (padding == null) padding = new RectOffset(28, 28, 20, 20);
        if (characterPanelPadding      == null) characterPanelPadding      = new RectOffset(12, 12, 12, 12);
        if (characterImagePanelPadding == null) characterImagePanelPadding = new RectOffset(8, 8, 8, 8);
        if (characterNamePanelPadding  == null) characterNamePanelPadding  = new RectOffset(8, 8, 6, 6);
        if (characterNamePanelBorderImage == null) characterNamePanelBorderImage = new TiledImageSettings();
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Every play session starts from a clean dialogue state — no leftover
        // speaker, section, typewriter or history from a previous session.
        ClearDialogueUiRuntimeState();

        Debug.Log("Dialogue_Engine: Awake started.");

        foreach (var old in GetComponents<UIDocument>())
            Destroy(old);

        document = gameObject.AddComponent<UIDocument>();

        if (panelSettings == null)
        { Debug.LogError("Dialogue_Engine: PanelSettings not assigned."); return; }

        document.panelSettings = panelSettings;

        // ── Resolve which UXML to attach: the disposable play-mode copy ────────
        #if UNITY_EDITOR
        var uxml = LoadRuntimeUxmlCopy();
        #else
        // In builds the generated layout must live inside a Resources folder.
        var uxml = Resources.Load<VisualTreeAsset>("Dialogue_Presets/dialogue_generated");
        #endif

        if (uxml == null)
        { Debug.LogError($"Dialogue_Engine: Runtime UXML copy could not be created at {RUNTIME_UXML_PATH}. Build the layout first."); return; }

        document.visualTreeAsset = uxml;

        var docRoot = document.rootVisualElement;
        if (docRoot == null)
        { Debug.LogError("Dialogue_Engine: rootVisualElement is null."); return; }

        // ── Core element refs ─────────────────────────────────────────────────
        rowContainer       = docRoot.Q("RowContainer");
        box                = docRoot.Q("DialogueBox");
        backgroundLayer    = docRoot.Q("BackgroundLayer");
        borderLayer        = docRoot.Q("BorderLayer");
        textScroll         = docRoot.Q<ScrollView>("TextScroll");
        dialogueTextLabel  = docRoot.Q<Label>("DialogueText");
        advanceHintLabel   = docRoot.Q<Label>("AdvanceHint");
        choiceContainer    = docRoot.Q("ChoiceContainer");

        // Visual-layout runtime cast slots: VisualSlot{i}Wrapper /
        // VisualImagePanel{i} / VisualPortrait{i} / VisualName{i}, built by the
        // visual editor — one indexed pair per image/name panel.
        visualRuntimeSlots = new List<VisualRuntimeSlot>();
        for (int i = 0; ; i++)
        {
            VisualElement visualWrapper = docRoot.Q("VisualSlot" + i + "Wrapper");
            if (visualWrapper == null) break;
            VisualElement visualPanel = docRoot.Q("VisualImagePanel" + i);
            visualRuntimeSlots.Add(new VisualRuntimeSlot
            {
                wrapper  = visualWrapper,
                panel    = visualPanel,
                frame    = docRoot.Q("VisualPortraitFrame" + i),
                portrait = docRoot.Q("VisualPortrait" + i),
                name     = docRoot.Q("VisualName" + i),
                hidePanelWhenEmpty = visualPanel != null && visualPanel.ClassListContains("dlg-fig-hide")
            });
        }

        // Choice panel from the visual layout (may be absent — optional).
        choicePanelRoot = docRoot.Q("ChoicePanel");
        visualChoiceButtons.Clear();
        visualChoiceButtonTexts.Clear();
        if (choicePanelRoot != null)
        {
            for (int i = 0; ; i++)
            {
                VisualElement button = docRoot.Q("ChoiceButton" + i);
                if (button == null) break;
                visualChoiceButtons.Add(button);
                visualChoiceButtonTexts.Add(docRoot.Q<Label>("ChoiceButtonText" + i));

                // Registered once per tree; the option list is read at click
                // time and the current-token guard makes stale clicks no-ops.
                int index = i;
                button.pickingMode = PickingMode.Position;
                button.RegisterCallback<ClickEvent>(_ =>
                {
                    if (currentChoiceToken == null) return;
                    if (index < 0 || index >= choiceOptions.Count) return;
                    OnOptionSelected(choiceOptions[index]);
                });
            }
        }

        // Portrait wrappers
        insideLeftWrapper  = docRoot.Q("InsideLeftWrapper");
        insideRightWrapper = docRoot.Q("InsideRightWrapper");
        insideLeftHost     = docRoot.Q("PortraitHostInsideLeft");
        insideRightHost    = docRoot.Q("PortraitHostInsideRight");
        frameInsideLeft    = docRoot.Q("PortraitFrameInsideLeft");
        frameInsideRight   = docRoot.Q("PortraitFrameInsideRight");
        portraitInsideLeft = docRoot.Q("PortraitInsideLeft");
        portraitInsideRight= docRoot.Q("PortraitInsideRight");
        overlayInsideLeft  = docRoot.Q("PortraitBorderOverlayInsideLeft");
        overlayInsideRight = docRoot.Q("PortraitBorderOverlayInsideRight");
        nameInsideLeft     = docRoot.Q("NameInsideLeft");
        nameInsideRight    = docRoot.Q("NameInsideRight");

        outsideLeftWrapper  = docRoot.Q("OutsideLeftWrapper");
        outsideRightWrapper = docRoot.Q("OutsideRightWrapper");
        outsideLeftHost     = docRoot.Q("PortraitHostOutsideLeft");
        outsideRightHost    = docRoot.Q("PortraitHostOutsideRight");
        frameOutsideLeft    = docRoot.Q("PortraitFrameOutsideLeft");
        frameOutsideRight   = docRoot.Q("PortraitFrameOutsideRight");
        portraitOutsideLeft = docRoot.Q("PortraitOutsideLeft");
        portraitOutsideRight= docRoot.Q("PortraitOutsideRight");
        overlayOutsideLeft  = docRoot.Q("PortraitBorderOverlayOutsideLeft");
        overlayOutsideRight = docRoot.Q("PortraitBorderOverlayOutsideRight");
        nameOutsideLeft     = docRoot.Q("NameOutsideLeft");
        nameOutsideRight    = docRoot.Q("NameOutsideRight");

        borderLeftWrapper  = docRoot.Q("BorderLeftWrapper");
        borderRightWrapper = docRoot.Q("BorderRightWrapper");
        borderLeftHost     = docRoot.Q("PortraitHostBorderLeft");
        borderRightHost    = docRoot.Q("PortraitHostBorderRight");
        frameBorderLeft    = docRoot.Q("PortraitFrameBorderLeft");
        frameBorderRight   = docRoot.Q("PortraitFrameBorderRight");
        portraitBorderLeft = docRoot.Q("PortraitBorderLeft");
        portraitBorderRight= docRoot.Q("PortraitBorderRight");
        overlayBorderLeft  = docRoot.Q("PortraitBorderOverlayBorderLeft");
        overlayBorderRight = docRoot.Q("PortraitBorderOverlayBorderRight");
        nameBorderLeft     = docRoot.Q("NameBorderLeft");
        nameBorderRight    = docRoot.Q("NameBorderRight");

        // Character figure panels
        charLeftWrapper   = docRoot.Q("CharacterPanelLeftWrapper");
        charRightWrapper  = docRoot.Q("CharacterPanelRightWrapper");
        charLeftHost   = docRoot.Q("PortraitHostCharLeft");
        charRightHost  = docRoot.Q("PortraitHostCharRight");
        frameCharLeft  = docRoot.Q("PortraitFrameCharLeft");
        frameCharRight = docRoot.Q("PortraitFrameCharRight");
        portraitCharLeft  = docRoot.Q("PortraitCharLeft");
        portraitCharRight = docRoot.Q("PortraitCharRight");
        overlayCharLeft   = docRoot.Q("PortraitBorderOverlayCharLeft");
        overlayCharRight  = docRoot.Q("PortraitBorderOverlayCharRight");
        nameCharLeft      = docRoot.Q("NameCharLeft");
        nameCharRight     = docRoot.Q("NameCharRight");
        charLeftFigure    = docRoot.Q("CharacterFigurePanelLeft");
        charRightFigure   = docRoot.Q("CharacterFigurePanelRight");
        charLeftImagePanel  = docRoot.Q("CharacterImagePanelLeft");
        charRightImagePanel = docRoot.Q("CharacterImagePanelRight");
        charLeftNamePanel   = docRoot.Q("CharacterNamePanelLeft");
        charRightNamePanel  = docRoot.Q("CharacterNamePanelRight");
        charLeftNameBorderOverlay  = docRoot.Q("CharacterNameBorderOverlayLeft");
        charRightNameBorderOverlay = docRoot.Q("CharacterNameBorderOverlayRight");

        // Toolbar / history / settings
        toolbarPanel        = docRoot.Q("ToolbarPanel");
        historyPanel        = docRoot.Q("HistoryPanel");
        historyContent      = docRoot.Q<ScrollView>("HistoryContent");
        settingsPanel       = docRoot.Q("SettingsPanel");
        settingsContent     = docRoot.Q<ScrollView>("SettingsContent");
        toolbarToggleButton = docRoot.Q<Button>("ToolbarToggle");
        historyButton       = docRoot.Q<Button>("HistoryButton");
        settingsButton      = docRoot.Q<Button>("SettingsButton");
        rewindButton        = docRoot.Q<Button>("RewindButton");
        closeHistoryButton  = docRoot.Q<Button>("CloseHistoryButton");
        closeSettingsButton = docRoot.Q<Button>("CloseSettingsButton");

        // Wire buttons
        if (toolbarToggleButton != null) toolbarToggleButton.clicked += ToggleToolbar;
        if (historyButton       != null) historyButton.clicked       += ShowHistory;
        if (settingsButton      != null) settingsButton.clicked      += ShowSettings;
        if (rewindButton        != null) rewindButton.clicked        += OnRewind;
        if (closeHistoryButton  != null) closeHistoryButton.clicked  += HideHistory;
        if (closeSettingsButton != null) closeSettingsButton.clicked += HideSettings;

        if (box == null)               Debug.LogError("Dialogue_Engine: 'DialogueBox' not found in UXML.");
        if (dialogueTextLabel == null) Debug.LogError("Dialogue_Engine: 'DialogueText' not found in UXML.");

        if (box != null) box.style.display = DisplayStyle.None;

        // ── Interaction wiring (keyboard navigation, click-to-advance) ────────
        docRoot.focusable = true;
        docRoot.RegisterCallback<KeyDownEvent>(OnKeyDown);
        if (clickToAdvance && box != null)
            box.RegisterCallback<ClickEvent>(OnBoxClicked);

        // Apply the runtime layout (sizing, image layers, shapes…). Runs once
        // immediately and again after the first layout pass via GeometryChanged.
        ApplyRuntimeLayout();
        if (box != null)
            box.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (!layoutApplied) { layoutApplied = true; ApplyRuntimeLayout(); }
                RebuildDynamicLayers();
            });

        Debug.Log($"Dialogue_Engine: Awake done. box={box != null}");
    }

    void OnDestroy()
    {
        queryServer.Clear();
        liveSnapshotServer.Clear();
        liveEventServer.Clear();
        priorityLiveEventServer.Clear();
        onEmitFacadeSubscriptionIds.Clear();
        if (Instance == this) Instance = null;
    }

    void EnsureCharacterPanelDefaults()
    {
        if (characterPanelDataVersion >= CHARACTER_PANEL_DATA_VERSION) return;

        // Migration for Character Panels serialized before the explicit Name
        // partition existed. New bool fields deserialize as false on existing
        // scene components, which made the panel transparent even though its
        // text remained visible.
        characterPanelShowNamePanel = true;
        characterNamePanelShowBackground = true;
        if (characterNamePanelBg.a <= 0.01f)
            characterNamePanelBg = new Color(0.05f, 0.05f, 0.06f, 0.96f);
        characterNamePanelHeightMode = CharacterPanelSizeMode.Default;
        characterNamePanelHeight = 24f;
        characterNamePanelShowBorder = true;
        if (characterNamePanelBorderWidth <= 0f)
            characterNamePanelBorderWidth = 1f;
        characterPanelDataVersion = CHARACTER_PANEL_DATA_VERSION;
    }

    #if UNITY_EDITOR
    void OnValidate()
    {
        EnsureCharacterPanelDefaults();
        // Border colours are full-opacity by design: translucent borders read
        // as "greyed out" on dark panels. Heal any legacy serialized alpha
        // (old defaults were 0.12 / 0.18) so picked colours show exactly.
        if (borderColour.a < 1f) borderColour.a = 1f;
        if (portraitBorderColour.a < 1f) portraitBorderColour.a = 1f;
        if (!Application.isPlaying)
            ApplyVisualLayoutAssetIfAssigned();
    }
    #endif

    void ApplyVisualLayoutAssetIfAssigned()
    {
        if (!useVisualLayoutAsset || visualLayoutAsset == null) return;
        DialogueVisualLayoutBridge.ApplyToEngine(this, visualLayoutAsset);
    }

    #if UNITY_EDITOR
    // Returns the path of the selected preset (applying its sidecar .json),
    // or null when no preset is selected / the preset file is missing.
    string ResolvePresetPath()
    {
        if (string.IsNullOrEmpty(presetName)) return null;

        string fileName = presetName.Trim();
        if (!fileName.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase))
            fileName += ".uxml";
        string fullPath = Path.Combine(PRESETS_PATH, fileName);

        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"Dialogue_Engine: Preset \"{presetName}\" not found in {PRESETS_PATH} — using the generated layout instead.");
            return null;
        }

        // Apply the sidecar (sprites/fonts/animations) if present.
        string jsonPath = Path.ChangeExtension(fullPath, ".json");
        if (File.Exists(jsonPath))
        {
            try
            {
                var dto = JsonUtility.FromJson<DialoguePresetDTO>(File.ReadAllText(jsonPath));
                if (dto != null) ApplyPreset(dto);
            }
            catch (Exception ex) { Debug.LogWarning($"Dialogue_Engine: Failed to read preset sidecar {jsonPath}: {ex.Message}"); }
        }
        return fullPath;
    }

    /// <summary>
    /// Play-mode UI isolation. The source UXML (generated file or preset) is
    /// never written during play. Instead the engine writes the *current*
    /// layout into a disposable runtime copy and instantiates that copy — the
    /// runtime may change it freely, and the whole file is discarded when
    /// play mode ends (DialogueRuntimeUxmlIsolation deletes it).
    /// </summary>
    /// <summary>
    /// Hook installed by the visual editor assembly (DialogueVisualEditorUxml,
    /// via [InitializeOnLoad]): builds/refreshes the canonical UXML owned by
    /// the visual editor and returns its asset path. The engine deliberately
    /// does NOT reference that class directly — editor scripts may live in an
    /// "Editor" magic folder (a separate assembly the runtime assembly cannot
    /// see), so the dependency is inverted: the editor pushes this hook in.
    /// </summary>
    public static Func<DialogueLayoutAsset, Dialogue_Engine, Vector2, string> EnsureVisualLayoutUxmlBuilt;

    VisualTreeAsset LoadRuntimeUxmlCopy()
    {
        // The visual layout asset wins when assigned, exactly like edit-time.
        ApplyVisualLayoutAssetIfAssigned();

        visualLayoutRuntimeActive = false;
        string contents = null;
        string source = "engine layout";

        // 1) The visual layout asset is an explicit opt-in. The visual editor
        //    OWNS the canonical UXML; the engine never re-derives the layout —
        //    it just takes a byte-for-byte copy of the editor's file.
        if (useVisualLayoutAsset && visualLayoutAsset != null)
        {
            try
            {
                Vector2 canvas = panelSettings != null
                    ? new Vector2(panelSettings.referenceResolution.x, panelSettings.referenceResolution.y)
                    : new Vector2(1920f, 1080f);
                // Single builder, owned by the editor — reached through the
                // hook so this compiles regardless of editor folder layout.
                string canonicalPath = EnsureVisualLayoutUxmlBuilt != null
                    ? EnsureVisualLayoutUxmlBuilt(visualLayoutAsset, this, canvas)
                    : null;
                if (string.IsNullOrEmpty(canonicalPath))
                {
                    Debug.LogError("Dialogue_Engine: the visual editor UXML builder is not installed " +
                                   "(editor scripts failed to compile or were not loaded). Falling back.");
                }
                else
                {
                    contents = File.ReadAllText(canonicalPath);
                    source = "visual editor UXML '" + Path.GetFileName(canonicalPath) + "'";
                    if (ValidateRuntimeUxml(contents, source))
                        visualLayoutRuntimeActive = true;
                    else
                        contents = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Dialogue_Engine: Failed to load the visual editor UXML ({ex.Message}). Falling back.");
            }
        }

        // 2) A saved preset, if one is selected.
        if (contents == null)
        {
            string presetPath = ResolvePresetPath();
            if (presetPath != null)
            {
                try
                {
                    contents = File.ReadAllText(presetPath);
                    source = "preset " + presetPath;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Dialogue_Engine: Failed to read preset '{presetPath}' ({ex.Message}).");
                    contents = null;
                }
                if (contents != null && !ValidateRuntimeUxml(contents, source))
                    contents = null;
            }
        }

        // 3) Classic generator from the inspector fields — always expected to work.
        if (contents == null)
        {
            contents = GenerateUxml(this);
            source = "engine layout";
            ValidateRuntimeUxml(contents, source);
        }

        string dir = Path.GetDirectoryName(RUNTIME_UXML_PATH);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // Drop any stale asset first so the importer can never resurrect a
        // cached older version of the runtime copy.
        if (File.Exists(RUNTIME_UXML_PATH) && !AssetDatabase.DeleteAsset(RUNTIME_UXML_PATH))
        {
            File.Delete(RUNTIME_UXML_PATH);
            string staleMeta = RUNTIME_UXML_PATH + ".meta";
            if (File.Exists(staleMeta)) File.Delete(staleMeta);
        }
        File.WriteAllText(RUNTIME_UXML_PATH, contents);

        // Plain-text twin so the exact generated XML is always inspectable.
        try { File.WriteAllText(RUNTIME_UXML_PATH + ".txt", contents); } catch { }

        AssetDatabase.ImportAsset(RUNTIME_UXML_PATH, ImportAssetOptions.ForceUpdate);
        VisualTreeAsset imported = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RUNTIME_UXML_PATH);
        if (imported == null)
        {
            Debug.LogError($"Dialogue_Engine: runtime UXML copy failed to import (source: {source}). Raw output was dumped to {RUNTIME_UXML_PATH}.txt");
            visualLayoutRuntimeActive = false;
        }
        return imported;
    }

    /// <summary>
    /// Validates the runtime UXML before it ever reaches the AssetDatabase.
    /// On failure the exact parser error plus the offending lines are logged,
    /// the raw output is dumped to '.invalid.txt', and the caller falls back
    /// to a working layout so Play Mode is never left without a UI.
    /// </summary>
    bool ValidateRuntimeUxml(string contents, string source)
    {
        if (string.IsNullOrEmpty(contents))
        {
            Debug.LogError("Dialogue_Engine: runtime UXML from " + source + " is empty.");
            return false;
        }
        try
        {
            System.Xml.Linq.XDocument.Parse(contents);
            return true;
        }
        catch (System.Xml.XmlException ex)
        {
            string dumpPath = RUNTIME_UXML_PATH + ".invalid.txt";
            try { File.WriteAllText(dumpPath, contents); } catch { }
            Debug.LogError(
                "Dialogue_Engine: runtime UXML from " + source + " is not valid XML — " + ex.Message +
                "\nAround the reported line:\n" + DescribeXmlLines(contents, ex.LineNumber) +
                "\nFull raw output dumped to " + dumpPath + " — falling back to a working layout.");
            return false;
        }
    }

    static string DescribeXmlLines(string contents, int lineNumber)
    {
        if (string.IsNullOrEmpty(contents) || lineNumber <= 0) return "(unknown)";
        string[] lines = contents.Split('\n');
        int target = lineNumber - 1;
        var sb = new System.Text.StringBuilder();
        for (int i = Mathf.Max(0, target - 2); i <= Mathf.Min(lines.Length - 1, target + 1); i++)
            sb.Append(i + 1).Append(": ").Append(lines[i].TrimEnd('\r')).Append('\n');
        return sb.ToString();
    }
    #endif

    /// <summary>
    /// Clears every dialogue-UI-carried state (current speaker, section,
    /// traversal stack, typewriter, history, suspended dialogues, portrait
    /// slot ownership). Called when play starts and when play mode ends so no
    /// speaker/section state survives a play session.
    /// Pure state — safe to call when no UI exists (e.g. after exiting play).
    /// </summary>
    public void ClearDialogueUiRuntimeState()
    {
        if (typewriterCoroutine != null)
        {
            // Safe in edit mode (post-play cleanup) as well as during play.
            if (Application.isPlaying && isActiveAndEnabled)
                StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        isTyping = false;
        isOpen = false;
        currentFullText = "";
        shownText = "";
        currentTextName = "";
        currentServiceText = "";
        currentDialogueId = "";
        currentDialoguePath = "";
        lastEmittedEvent = "";
        currentSection = null;
        currentIndex = 0;
        sectionStack.Clear();
        currentCharacterToken = null;
        currentChoiceToken = null;
        currentDialogueInterruptible = false;
        currentDialogueSaveState = false;
        suspendedDialogues.Clear();
        slotOwner[0] = null;
        slotOwner[1] = null;
        slotTokens[0] = null;
        slotTokens[1] = null;
        history.Clear();
        choiceButtons.Clear();
        choiceOptions.Clear();
        choiceHighlight = -1;
        SetRuntimeStatus(DialogueRuntimeStatus.Idle, "Not playing");
    }

    // ─── Preset helpers ────────────────────────────────────────────────────────
    public void ApplyPreset(DialoguePresetDTO d)
    {
        if (d == null) return;

        #if UNITY_EDITOR
        if (!string.IsNullOrEmpty(d.panelSettingsGuid))
            panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(d.panelSettingsGuid));
        #endif

        panelWidthMode = (PanelSizeMode)d.panelWidthMode; panelWidthValue = d.panelWidthValue;
        panelHeightMode = (PanelSizeMode)d.panelHeightMode; panelHeightValue = d.panelHeightValue;
        panelOffsetX = d.panelOffsetX; panelOffsetY = d.panelOffsetY;
        padding = new RectOffset(d.padLeft, d.padRight, d.padTop, d.padBottom);

        backgroundMode = (BackgroundMode)d.backgroundMode;
        backgroundColour = d.backgroundColour;
        ApplyTiledDTO(backgroundImage, d.backgroundSpriteGuid, d.backgroundSpritePath, d.backgroundScaleMode, d.backgroundAnimate, d.backgroundAnimDir, d.backgroundAnimSpeed, d.backgroundLoop, d.backgroundTileScale, d.backgroundTintEnabled, d.backgroundTintColour);

        borderWidth = d.borderWidth; borderColour = d.borderColour;
        borderRadiusTL = d.borderRadiusTL; borderRadiusTR = d.borderRadiusTR;
        borderRadiusBL = d.borderRadiusBL; borderRadiusBR = d.borderRadiusBR;
        ApplyTiledDTO(borderImage, d.borderSpriteGuid, d.borderSpritePath, d.borderScaleMode, d.borderAnimate, d.borderAnimDir, d.borderAnimSpeed, d.borderLoop, d.borderTileScale, d.borderTintEnabled, d.borderTintColour);

        nameColour = d.nameColour; nameFontSize = d.nameFontSize; nameUppercase = d.nameUppercase;
        #if UNITY_EDITOR
        if (!string.IsNullOrEmpty(d.nameFontGuid)) nameFont = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(d.nameFontGuid));
        #endif
        namePosition = (NamePosition)d.namePosition; nameDistance = d.nameDistance;
        nameLetterMode = (LetterMode)d.nameLetterMode; nameLetterAmplitude = d.nameLetterAmplitude;
        nameLetterFrequency = d.nameLetterFrequency; nameLetterSpacing = d.nameLetterSpacing;
        nameLetterPhase = d.nameLetterPhase; nameLetterAnimationSpeed = d.nameLetterAnimationSpeed;

        textColour = d.textColour; textFontSize = d.textFontSize;
        #if UNITY_EDITOR
        if (!string.IsNullOrEmpty(d.textFontGuid)) textFont = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(d.textFontGuid));
        #endif
        textLetterMode = (LetterMode)d.textLetterMode; textLetterAmplitude = d.textLetterAmplitude;
        textLetterFrequency = d.textLetterFrequency; textLetterSpacing = d.textLetterSpacing;
        textLetterPhase = d.textLetterPhase; textLetterAnimationSpeed = d.textLetterAnimationSpeed;
        textVAnchor = (TextVAnchor)d.textVAnchor;
        textHAnchor = (TextHAnchor)d.textHAnchor;

        enableTypewriter = d.enableTypewriter; typewriterSpeed = d.typewriterSpeed;
        typewriterStartDelay = d.typewriterStartDelay;

        showPortrait = d.showPortrait; portraitMode = (PortraitMode)d.portraitMode;
        portraitPlacement = (PortraitPlacement)d.portraitPlacement; portraitShape = (PortraitShape)d.portraitShape;
        portraitDisplayType = (PortraitDisplayType)d.portraitDisplayType; portraitFillMode = (PortraitFillMode)d.portraitFillMode;
        portraitSize = d.portraitSize; dynamicPortraitSize = d.dynamicPortraitSize; maxPortraitSize = d.maxPortraitSize;
        portraitOffsetX = d.portraitOffsetX; portraitOffsetY = d.portraitOffsetY;
        portraitFlipHorizontal = d.portraitFlipHorizontal;
        showPortraitWhenEmpty = d.showPortraitWhenEmpty;
        portraitBorderColour = d.portraitBorderColour; showPortraitBorder = d.showPortraitBorder;
        portraitBorderWidth = d.portraitBorderWidth; portraitBorderRadius = d.portraitBorderRadius;
        ApplyTiledDTO(portraitBorderImage, d.portraitBorderSpriteGuid, d.portraitBorderSpritePath, d.portraitBorderScaleMode, d.portraitBorderAnimate, d.portraitBorderAnimDir, d.portraitBorderAnimSpeed, d.portraitBorderLoop, d.portraitBorderTileScale, d.portraitBorderTintEnabled, d.portraitBorderTintColour);
        activePortraitOpacity = d.activePortraitOpacity; inactivePortraitOpacity = d.inactivePortraitOpacity;
        inactiveTintColour = d.inactiveTintColour;

        showAdvanceHint = d.showAdvanceHint; advanceHintText = d.advanceHintText;
        hintColour = d.hintColour; hintFontSize = d.hintFontSize;

        showToolbar = d.showToolbar; showSettingsButton = d.showSettingsButton;
        toolbarSlideDirection = (ToolbarSlideDirection)d.toolbarSlideDirection;

        characterPanelDataVersion = d.characterPanelDataVersion;
        characterPanelShowImagePanel = d.characterPanelShowImagePanel;
        characterPanelShowNamePanel  = d.characterPanelShowNamePanel;
        characterPanelOrder = (CharacterPanelOrder)d.characterPanelOrder;
        characterPanelWidthMode = (CharacterPanelSizeMode)d.characterPanelWidthMode;
        characterPanelWidth = d.characterPanelWidth;
        characterPanelHeightMode = (CharacterPanelSizeMode)d.characterPanelHeightMode;
        characterPanelHeight = d.characterPanelHeight;
        characterPanelShowBackground = d.characterPanelShowBackground;
        characterPanelShowBorder = d.characterPanelShowBorder;
        characterPanelBg = d.characterPanelBg;
        characterPanelBorderColour = d.characterPanelBorderColour;
        characterPanelBorderWidth = d.characterPanelBorderWidth;
        characterPanelRadius = d.characterPanelRadius;
        characterPanelPadding = new RectOffset(d.charPanelPadL, d.charPanelPadR, d.charPanelPadT, d.charPanelPadB);
        characterPanelSpacing = d.characterPanelSpacing;
        characterImagePanelBg = d.characterImagePanelBg;
        characterImagePanelShape = (CharacterImagePanelShape)d.characterImagePanelShape;
        characterImagePanelTransparentWithImage = d.characterImagePanelTransparentWithImage;
        characterImagePanelShowBorder = d.characterImagePanelShowBorder;
        characterImagePanelBorderColour = d.characterImagePanelBorderColour;
        characterImagePanelBorderWidth = d.characterImagePanelBorderWidth;
        characterImagePanelRadius = d.characterImagePanelRadius;
        characterImagePanelPadding = new RectOffset(d.charImagePadL, d.charImagePadR, d.charImagePadT, d.charImagePadB);
        characterNamePanelShowBackground = d.characterNamePanelShowBackground;
        characterNamePanelBg = d.characterNamePanelBg;
        characterNamePanelShape = (CharacterImagePanelShape)d.characterNamePanelShape;
        characterNamePanelHeightMode = (CharacterPanelSizeMode)d.characterNamePanelHeightMode;
        characterNamePanelHeight = d.characterNamePanelHeight;
        characterNamePanelShowBorder = d.characterNamePanelShowBorder;
        characterNamePanelBorderColour = d.characterNamePanelBorderColour;
        characterNamePanelBorderWidth = d.characterNamePanelBorderWidth;
        characterNamePanelRadius = d.characterNamePanelRadius;
        ApplyTiledDTO(characterNamePanelBorderImage, d.characterNameBorderSpriteGuid, d.characterNameBorderSpritePath,
            d.characterNameBorderScaleMode, d.characterNameBorderAnimate, d.characterNameBorderAnimDir,
            d.characterNameBorderAnimSpeed, d.characterNameBorderLoop, d.characterNameBorderTileScale,
            d.characterNameBorderTintEnabled, d.characterNameBorderTintColour);
        characterNamePanelPadding = new RectOffset(d.charNamePadL, d.charNamePadR, d.charNamePadT, d.charNamePadB);
        EnsureCharacterPanelDefaults();

        useDefaultPortraitPlaceholder = d.useDefaultPortraitPlaceholder;
        defaultPortraitPath = d.defaultPortraitPath;
        #if UNITY_EDITOR
        defaultPortraitSprite = !string.IsNullOrEmpty(d.defaultPortraitSpriteGuid)
            ? AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(d.defaultPortraitSpriteGuid)) : null;
        #endif
        clickToAdvance = d.clickToAdvance;
    }

    static void ApplyTiledDTO(TiledImageSettings t, string guid, string path, int scaleMode, bool animate, int dir, float speed, bool loop, float tileScale, bool tintEnabled, Color tintColour)
    {
        if (t == null) return;
        #if UNITY_EDITOR
        t.sprite = !string.IsNullOrEmpty(guid) ? AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        #endif
        t.path = path;
        t.scaleMode = (ImageScaleMode)scaleMode;
        t.animate = animate; t.animDirection = (TiledAnimDirection)dir;
        t.animSpeed = speed; t.loop = loop; t.tileScale = tileScale;
        t.tintEnabled = tintEnabled; t.tintColour = tintColour;
    }

    public DialoguePresetDTO BuildPresetDTO()
    {
        var d = new DialoguePresetDTO();
        #if UNITY_EDITOR
        d.panelSettingsGuid = panelSettings != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(panelSettings)) : "";
        #endif
        d.panelWidthMode = (int)panelWidthMode; d.panelWidthValue = panelWidthValue;
        d.panelHeightMode = (int)panelHeightMode; d.panelHeightValue = panelHeightValue;
        d.panelOffsetX = panelOffsetX; d.panelOffsetY = panelOffsetY;
        if (padding == null) padding = new RectOffset(28, 28, 20, 20);
        d.padLeft = padding.left; d.padRight = padding.right; d.padTop = padding.top; d.padBottom = padding.bottom;

        d.backgroundMode = (int)backgroundMode; d.backgroundColour = backgroundColour;
        FillTiledDTO(backgroundImage, ref d.backgroundSpriteGuid, ref d.backgroundSpritePath, ref d.backgroundScaleMode, ref d.backgroundAnimate, ref d.backgroundAnimDir, ref d.backgroundAnimSpeed, ref d.backgroundLoop, ref d.backgroundTileScale, ref d.backgroundTintEnabled, ref d.backgroundTintColour);

        d.borderWidth = borderWidth; d.borderColour = borderColour;
        d.borderRadiusTL = borderRadiusTL; d.borderRadiusTR = borderRadiusTR;
        d.borderRadiusBL = borderRadiusBL; d.borderRadiusBR = borderRadiusBR;
        FillTiledDTO(borderImage, ref d.borderSpriteGuid, ref d.borderSpritePath, ref d.borderScaleMode, ref d.borderAnimate, ref d.borderAnimDir, ref d.borderAnimSpeed, ref d.borderLoop, ref d.borderTileScale, ref d.borderTintEnabled, ref d.borderTintColour);

        d.nameColour = nameColour; d.nameFontSize = nameFontSize; d.nameUppercase = nameUppercase;
        #if UNITY_EDITOR
        d.nameFontGuid = nameFont != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(nameFont)) : "";
        #endif
        d.namePosition = (int)namePosition; d.nameDistance = nameDistance;
        d.nameLetterMode = (int)nameLetterMode; d.nameLetterAmplitude = nameLetterAmplitude;
        d.nameLetterFrequency = nameLetterFrequency; d.nameLetterSpacing = nameLetterSpacing;
        d.nameLetterPhase = nameLetterPhase; d.nameLetterAnimationSpeed = nameLetterAnimationSpeed;

        d.textColour = textColour; d.textFontSize = textFontSize;
        #if UNITY_EDITOR
        d.textFontGuid = textFont != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(textFont)) : "";
        #endif
        d.textLetterMode = (int)textLetterMode; d.textLetterAmplitude = textLetterAmplitude;
        d.textLetterFrequency = textLetterFrequency; d.textLetterSpacing = textLetterSpacing;
        d.textLetterPhase = textLetterPhase; d.textLetterAnimationSpeed = textLetterAnimationSpeed;
        d.textVAnchor = (int)textVAnchor;
        d.textHAnchor = (int)textHAnchor;

        d.enableTypewriter = enableTypewriter; d.typewriterSpeed = typewriterSpeed;
        d.typewriterStartDelay = typewriterStartDelay;

        d.showPortrait = showPortrait; d.portraitMode = (int)portraitMode;
        d.portraitPlacement = (int)portraitPlacement; d.portraitShape = (int)portraitShape;
        d.portraitDisplayType = (int)portraitDisplayType; d.portraitFillMode = (int)portraitFillMode;
        d.portraitSize = portraitSize; d.dynamicPortraitSize = dynamicPortraitSize; d.maxPortraitSize = maxPortraitSize;
        d.portraitOffsetX = portraitOffsetX; d.portraitOffsetY = portraitOffsetY;
        d.portraitFlipHorizontal = portraitFlipHorizontal;
        d.showPortraitWhenEmpty = showPortraitWhenEmpty;
        d.portraitBorderColour = portraitBorderColour; d.showPortraitBorder = showPortraitBorder;
        d.portraitBorderWidth = portraitBorderWidth; d.portraitBorderRadius = portraitBorderRadius;
        FillTiledDTO(portraitBorderImage, ref d.portraitBorderSpriteGuid, ref d.portraitBorderSpritePath, ref d.portraitBorderScaleMode, ref d.portraitBorderAnimate, ref d.portraitBorderAnimDir, ref d.portraitBorderAnimSpeed, ref d.portraitBorderLoop, ref d.portraitBorderTileScale, ref d.portraitBorderTintEnabled, ref d.portraitBorderTintColour);
        d.activePortraitOpacity = activePortraitOpacity; d.inactivePortraitOpacity = inactivePortraitOpacity;
        d.inactiveTintColour = inactiveTintColour;

        d.showAdvanceHint = showAdvanceHint; d.advanceHintText = advanceHintText;
        d.hintColour = hintColour; d.hintFontSize = hintFontSize;

        d.showToolbar = showToolbar; d.showSettingsButton = showSettingsButton;
        d.toolbarSlideDirection = (int)toolbarSlideDirection;

        d.characterPanelDataVersion = CHARACTER_PANEL_DATA_VERSION;
        d.characterPanelShowImagePanel = characterPanelShowImagePanel;
        d.characterPanelShowNamePanel  = characterPanelShowNamePanel;
        d.characterPanelOrder = (int)characterPanelOrder;
        d.characterPanelWidthMode = (int)characterPanelWidthMode;
        d.characterPanelWidth = characterPanelWidth;
        d.characterPanelHeightMode = (int)characterPanelHeightMode;
        d.characterPanelHeight = characterPanelHeight;
        d.characterPanelShowBackground = characterPanelShowBackground;
        d.characterPanelShowBorder = characterPanelShowBorder;
        d.characterPanelBg = characterPanelBg;
        d.characterPanelBorderColour = characterPanelBorderColour;
        d.characterPanelBorderWidth = characterPanelBorderWidth;
        d.characterPanelRadius = characterPanelRadius;
        if (characterPanelPadding == null) characterPanelPadding = new RectOffset(12, 12, 12, 12);
        d.charPanelPadL = characterPanelPadding.left; d.charPanelPadR = characterPanelPadding.right;
        d.charPanelPadT = characterPanelPadding.top; d.charPanelPadB = characterPanelPadding.bottom;
        d.characterPanelSpacing = characterPanelSpacing;
        d.characterImagePanelBg = characterImagePanelBg;
        d.characterImagePanelShape = (int)characterImagePanelShape;
        d.characterImagePanelTransparentWithImage = characterImagePanelTransparentWithImage;
        d.characterImagePanelShowBorder = characterImagePanelShowBorder;
        d.characterImagePanelBorderColour = characterImagePanelBorderColour;
        d.characterImagePanelBorderWidth = characterImagePanelBorderWidth;
        d.characterImagePanelRadius = characterImagePanelRadius;
        if (characterImagePanelPadding == null) characterImagePanelPadding = new RectOffset(8, 8, 8, 8);
        d.charImagePadL = characterImagePanelPadding.left; d.charImagePadR = characterImagePanelPadding.right;
        d.charImagePadT = characterImagePanelPadding.top; d.charImagePadB = characterImagePanelPadding.bottom;
        d.characterNamePanelShowBackground = characterNamePanelShowBackground;
        d.characterNamePanelBg = characterNamePanelBg;
        d.characterNamePanelShape = (int)characterNamePanelShape;
        d.characterNamePanelHeightMode = (int)characterNamePanelHeightMode;
        d.characterNamePanelHeight = characterNamePanelHeight;
        d.characterNamePanelShowBorder = characterNamePanelShowBorder;
        d.characterNamePanelBorderColour = characterNamePanelBorderColour;
        d.characterNamePanelBorderWidth = characterNamePanelBorderWidth;
        d.characterNamePanelRadius = characterNamePanelRadius;
        FillTiledDTO(characterNamePanelBorderImage, ref d.characterNameBorderSpriteGuid, ref d.characterNameBorderSpritePath,
            ref d.characterNameBorderScaleMode, ref d.characterNameBorderAnimate, ref d.characterNameBorderAnimDir,
            ref d.characterNameBorderAnimSpeed, ref d.characterNameBorderLoop, ref d.characterNameBorderTileScale,
            ref d.characterNameBorderTintEnabled, ref d.characterNameBorderTintColour);
        if (characterNamePanelPadding == null) characterNamePanelPadding = new RectOffset(8, 8, 6, 6);
        d.charNamePadL = characterNamePanelPadding.left; d.charNamePadR = characterNamePanelPadding.right;
        d.charNamePadT = characterNamePanelPadding.top; d.charNamePadB = characterNamePanelPadding.bottom;

        d.useDefaultPortraitPlaceholder = useDefaultPortraitPlaceholder;
        d.defaultPortraitPath = defaultPortraitPath;
        #if UNITY_EDITOR
        d.defaultPortraitSpriteGuid = defaultPortraitSprite != null
            ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(defaultPortraitSprite)) : "";
        #endif
        d.clickToAdvance = clickToAdvance;
        return d;
    }

    static void FillTiledDTO(TiledImageSettings t, ref string guid, ref string path, ref int scaleMode, ref bool animate, ref int dir, ref float speed, ref bool loop, ref float tileScale, ref bool tintEnabled, ref Color tintColour)
    {
        if (t == null) return;
        #if UNITY_EDITOR
        guid = t.sprite != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(t.sprite)) : "";
        #endif
        path = t.path;
        scaleMode = (int)t.scaleMode; animate = t.animate; dir = (int)t.animDirection;
        speed = t.animSpeed; loop = t.loop; tileScale = t.tileScale;
        tintEnabled = t.tintEnabled; tintColour = t.tintColour;
    }

    // ─── Public API ────────────────────────────────────────────────────────────
    public static bool Play(string path, bool interruptible = false, bool saveState = false)
    {
        if (Instance == null)
        {
            Debug.LogError("Dialogue_Engine: No instance in scene.");
            return false;
        }
        return Instance.TryPlay(path, interruptible, saveState);
    }

    // Legacy instance API. New code and BT nodes should use TryPlay/Play.
    public void Create(string path_input)
    {
        TryPlay(path_input, false, false);
    }

    public bool TryPlay(string path_input, bool interruptible = false, bool saveState = false)
    {
        if (string.IsNullOrEmpty(path_input))
        { Debug.LogError("Dialogue_Engine: Path is null or empty."); return false; }

        var file = new File_S(path_input);
        if (file.get_reader() == null)
        { Debug.LogError($"Dialogue_Engine: Could not open {path_input}"); return false; }

        DialogueGraph compiledGraph = Compiler_S.Compile(file);
        if (compiledGraph == null || compiledGraph.EntryNode == null)
        { Debug.LogError("Dialogue_Engine: Compilation failed or empty graph."); return false; }

        if (isOpen)
        {
            if (!currentDialogueInterruptible)
            {
                Debug.LogWarning($"Dialogue_Engine: Cannot play \"{path_input}\" because \"{currentDialoguePath}\" is not interruptible.");
                return false;
            }

            DialoguePlaybackState saved = currentDialogueSaveState ? CapturePlaybackState() : null;
            SetRuntimeStatus(DialogueRuntimeStatus.Interrupted,
                currentDialogueSaveState ? "Interrupted; state pushed for resume" : "Interrupted; state discarded");
            if (saved != null)
                suspendedDialogues.Push(saved);
            else
                // A non-saving interruption is a genuinely fresh branch. Do
                // not leave older suspended UI/state underneath it to reappear.
                suspendedDialogues.Clear();
            PrepareForReplacement();
        }
        else if (suspendedDialogues.Count > 0)
        {
            // Starting from an idle engine is always a brand-new playback chain.
            suspendedDialogues.Clear();
        }

        graph = compiledGraph;
        currentDialogueInterruptible = interruptible;
        currentDialogueSaveState = interruptible && saveState;

        DialogueScriptRecord scriptRow = RuntimeDatabase.RegisterDialogue(path_input);
        currentDialogueId = scriptRow.DialogueId;
        currentDialoguePath = scriptRow.Path;
        currentTextName = "";
        currentServiceText = "";
        lastEmittedEvent = "";
        SetRuntimeStatus(DialogueRuntimeStatus.Transitioning, "Dialogue started");

        if (path_input != lastLoadedScript)
        {
            portraits.Clear();
            lastLoadedScript = path_input;
        }

        foreach (string key in graph.UnresolvedPortraitKeys)
        {
            if (!portraits.Exists(p => p.key == key))
            {
                portraits.Add(new UnresolvedPortrait { key = key });
                Debug.LogWarning($"Dialogue_Engine: Unresolved portrait \"{key}\" — assign in Inspector.");
            }
        }

        // ── Dirty-list bookkeeping (scripts compiled with unresolved placeholders) ──
        TrackDirtyScript(path_input, graph);

        #if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        #endif

        isSuccess = false;

        // Reset every transient value from the previous DSL. The play-session
        // database remains intact, but active traversal/UI variables never leak
        // into a newly started script. Saved interruptions were captured above
        // and are restored only through RestorePlaybackState().
        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        typewriterCoroutine = null;
        isTyping = false;
        currentFullText = "";
        shownText = "";
        currentCharacterToken = null;
        currentChoiceToken = null;
        choiceButtons.Clear();
        choiceOptions.Clear();
        choiceHighlight = -1;
        if (choiceContainer != null)
        {
            choiceContainer.Clear();
            choiceContainer.style.display = DisplayStyle.None;
        }
        RenderDialogueText("");
        history.Clear();
        slotOwner[0] = null;
        slotOwner[1] = null;
        slotTokens[0] = null;
        slotTokens[1] = null;
        toolbarVisible = false;
        ResetPortraitSlots();
        ApplyRuntimeLayout();
        if (historyPanel != null) historyPanel.style.display = DisplayStyle.None;
        if (toolbarPanel != null) toolbarPanel.style.display = DisplayStyle.None;

        // Stack reset
        sectionStack.Clear();
        currentSection = graph.EntryNode;
        currentIndex   = 0;
        sectionStack.Push((currentSection, 0));

        if (box == null) { Debug.LogError("Dialogue_Engine: box is null."); return false; }
        box.style.display = DisplayStyle.Flex;
        isOpen = true;

        // ── Professional polish: reset, animate in, focus, ambient FX ────────
        choiceButtons.Clear();
        choiceOptions.Clear();
        choiceHighlight = -1;
        if (document != null && document.rootVisualElement != null)
            document.rootVisualElement.Focus();
        PlayOpenAnimation();
        StartHintPulse();

        AdvanceSection();
        return true;
    }

    DialoguePlaybackState CapturePlaybackState()
    {
        var traversal = new List<(SectionToken section, int index)>();
        var stackArray = sectionStack.ToArray(); // top -> bottom
        for (int i = stackArray.Length - 1; i >= 0; i--)
            traversal.Add(stackArray[i]);       // bottom -> top

        return new DialoguePlaybackState
        {
            graph = graph,
            currentSection = currentSection,
            currentIndex = currentIndex,
            traversal = traversal,
            dialogueId = currentDialogueId,
            dialoguePath = currentDialoguePath,
            textName = currentTextName,
            serviceText = currentServiceText,
            fullText = currentFullText,
            shownText = shownText,
            lastEvent = lastEmittedEvent,
            status = runtimeStatus,
            detail = runtimeDetail,
            history = new List<DialogueHistoryEntry>(history),
            portraits = portraits.ConvertAll(p => new UnresolvedPortrait
            {
                key = p.key,
                sprite = p.sprite,
                path = p.path
            }),
            lastLoadedScript = lastLoadedScript,
            slotOwners = new[] { slotOwner[0], slotOwner[1] },
            slotTokens = new[] { slotTokens[0], slotTokens[1] },
            currentCharacter = currentCharacterToken,
            currentChoice = currentChoiceToken,
            interruptible = currentDialogueInterruptible,
            saveState = currentDialogueSaveState
        };
    }

    void PrepareForReplacement()
    {
        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        typewriterCoroutine = null;
        StopCaretBlink();
        StopHintPulse();
        isTyping = false;
        isOpen = false;
        if (choiceContainer != null) choiceContainer.style.display = DisplayStyle.None;
        if (choicePanelRoot != null) choicePanelRoot.style.display = DisplayStyle.None;
        ResetPortraitSlots();
    }

    void RestorePlaybackState(DialoguePlaybackState state)
    {
        if (state == null) return;

        graph = state.graph;
        currentSection = state.currentSection;
        currentIndex = state.currentIndex;
        sectionStack.Clear();
        if (state.traversal != null)
            foreach (var item in state.traversal) sectionStack.Push(item);

        currentDialogueId = state.dialogueId;
        currentDialoguePath = state.dialoguePath;
        currentTextName = state.textName;
        currentServiceText = state.serviceText;
        currentFullText = state.fullText;
        shownText = state.shownText;
        lastEmittedEvent = state.lastEvent;
        currentCharacterToken = state.currentCharacter;
        currentChoiceToken = state.currentChoice;
        currentDialogueInterruptible = state.interruptible;
        currentDialogueSaveState = state.saveState;
        history.Clear();
        if (state.history != null) history.AddRange(state.history);
        portraits.Clear();
        if (state.portraits != null) portraits.AddRange(state.portraits);
        lastLoadedScript = state.lastLoadedScript;

        ResetPortraitSlots();
        for (int i = 0; i < 2; i++)
        {
            slotOwner[i] = state.slotOwners != null ? state.slotOwners[i] : null;
            slotTokens[i] = state.slotTokens != null ? state.slotTokens[i] : null;
            if (slotTokens[i] != null)
            {
                SlotRefs slot = GetSlot(i == 1);
                SetPortraitContent(slot, slotTokens[i]);
                RenderName(slot.name, slotTokens[i].Speaker);
                SetSlotOpacity(slot.portrait, slot.name,
                    slotOwner[i] == currentTextName, i);
            }
        }
        ShowPortraitWrappers();

        // Visual-layout runtime: rebuild the indexed cast slots from the
        // restored dual-slot state, then grey everyone but the current speaker.
        if (visualLayoutRuntimeActive && visualRuntimeSlots.Count > 0)
        {
            for (int i = 0; i < 2; i++)
                if (slotTokens[i] != null)
                    UpdateVisualRuntimeSlots(slotTokens[i], applyOpacity: false);
            ApplyVisualSlotOpacities(currentTextName);
        }

        isOpen = true;
        isSuccess = false;
        if (box != null) box.style.display = DisplayStyle.Flex;
        if (document != null && document.rootVisualElement != null)
            document.rootVisualElement.Focus();

        // A choice is reconstructed as an interactive choice. A partially typed
        // line resumes at line granularity, fully rendered and waiting for input.
        SetRuntimeStatus(DialogueRuntimeStatus.Resumed, "Resumed interrupted dialogue");
        if (currentChoiceToken != null && state.status == DialogueRuntimeStatus.TakingChoice)
        {
            ShowChoices(currentChoiceToken);
        }
        else
        {
            if (choiceContainer != null) choiceContainer.style.display = DisplayStyle.None;
        if (choicePanelRoot != null) choicePanelRoot.style.display = DisplayStyle.None;
            RenderDialogueText(currentFullText ?? "");
            isTyping = false;
            runtimeStatus = DialogueRuntimeStatus.WaitingForInput;
            runtimeDetail = "Resumed; waiting for Enter/Space";
            MarkLiveSnapshotDirty();
        }
        StartHintPulse();
    }

    // ─── Dirty-list tracking ───────────────────────────────────────────────────
    void TrackDirtyScript(string path, DialogueGraph compiledGraph)
    {
        var entry = dirtyScripts.Find(d => d.path == path);
        bool hasWarnings = compiledGraph != null && compiledGraph.UnresolvedPortraitKeys.Count > 0;

        if (hasWarnings)
        {
            if (entry == null)
            {
                entry = new DirtyScriptEntry { path = path };
                dirtyScripts.Add(entry);
            }
            entry.unresolvedKeys.Clear();
            entry.unresolvedKeys.AddRange(compiledGraph.UnresolvedPortraitKeys);
            Debug.LogWarning($"Dialogue_Engine: \"{path}\" marked dirty — {entry.unresolvedKeys.Count} unresolved portrait key(s): {string.Join(", ", entry.unresolvedKeys)}. Assign image sources in the Inspector.");
        }
        else if (entry != null)
        {
            // Clean compile → drop from the dirty list and free its unresolved entries.
            dirtyScripts.Remove(entry);
            portraits.RemoveAll(p => entry.unresolvedKeys.Contains(p.key));
            Debug.Log($"Dialogue_Engine: \"{path}\" compiled with 0 warnings — removed from the dirty list.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PLAY-SESSION DATABASE + IN-PROCESS CLIENT/SERVER API
    // ══════════════════════════════════════════════════════════════════════════
    void SetRuntimeStatus(DialogueRuntimeStatus status, string detail,
                          string emittedEvent = "")
    {
        runtimeStatus = status;
        runtimeDetail = detail ?? "";
        if (!string.IsNullOrEmpty(emittedEvent)) lastEmittedEvent = emittedEvent;
        if (RuntimeDatabase != null && !string.IsNullOrEmpty(currentDialogueId))
        {
            RuntimeDatabase.Record(currentDialogueId, currentTextName,
                currentServiceText, status, emittedEvent, runtimeDetail);
        }
        MarkLiveSnapshotDirty();
    }

    void EmitEvent(EventToken token)
    {
        if (token == null) return;
        string emitted = token.EmittedEvent ?? "";
        SetRuntimeStatus(DialogueRuntimeStatus.EventEmitted,
            "Emitted event " + emitted, emitted);
        Debug.Log($"Dialogue_Engine: @EMIT \"{emitted}\"");
        liveEventServer.Publish(currentDialoguePath, emitted);
        priorityLiveEventServer.Publish(currentDialoguePath, emitted);
    }

    public DialogueLiveSnapshot GetLiveSnapshot()
    {
        return new DialogueLiveSnapshot
        {
            IsPlaying = isOpen,
            DialogueId = currentDialogueId,
            DialoguePath = currentDialoguePath,
            SectionId = currentSection != null ? currentSection.SectionID : "",
            TextName = currentTextName,
            Text = currentServiceText,
            LastEvent = lastEmittedEvent,
            Status = runtimeStatus,
            Detail = runtimeDetail,
            LatestSequence = RuntimeDatabase != null ? RuntimeDatabase.LatestSequence : 0
        };
    }

    public DialogueResponse Send(DialogueRequest request)
    {
        if (request == null)
        {
            return new DialogueResponse
            {
                Code = DialogueResponseCode.InvalidRequest,
                Message = "<error>Request is null.</error>"
            };
        }

        var response = new DialogueResponse { RequestId = request.RequestId };
        if (RuntimeDatabase == null)
        {
            response.Code = DialogueResponseCode.NotFound;
            response.Message = "<error>Play-session database is unavailable.</error>";
            return response;
        }

        string dialogueKey = !string.IsNullOrEmpty(request.DialogueId)
            ? request.DialogueId
            : !string.IsNullOrEmpty(request.DialoguePath)
                ? request.DialoguePath : currentDialogueId;

        switch (request.Type)
        {
            case DialogueRequestType.LiveSnapshot:
                response.Code = DialogueResponseCode.Ok;
                response.Snapshot = GetLiveSnapshot();
                response.Message = response.Snapshot.ToMessage();
                return response;

            case DialogueRequestType.GetDialogue:
                response.Dialogue = RuntimeDatabase.FindDialogue(dialogueKey);
                response.Code = response.Dialogue != null
                    ? DialogueResponseCode.Ok : DialogueResponseCode.NotFound;
                response.Message = response.Dialogue != null
                    ? "<dialogue id=\"" + DialogueMessage.Escape(response.Dialogue.DialogueId) +
                      "\" plays=\"" + response.Dialogue.PlayCount + "\">" +
                      DialogueMessage.Escape(response.Dialogue.Path) + "</dialogue>"
                    : "<error>Dialogue not found.</error>";
                return response;

            case DialogueRequestType.GetEvents:
                response.Events = RuntimeDatabase.QueryEvents(dialogueKey,
                    request.EventName, request.SinceSequence);
                response.Code = DialogueResponseCode.Ok;
                response.Matched = response.Events.Count > 0;
                response.Message = BuildEventsMessage(response.Events);
                return response;

            case DialogueRequestType.HasEvent:
            case DialogueRequestType.WaitForEvent:
                if (string.IsNullOrEmpty(request.EventName))
                {
                    response.Code = DialogueResponseCode.InvalidRequest;
                    response.Message = "<error>EventName is required.</error>";
                    return response;
                }
                response.Events = RuntimeDatabase.QueryEvents(dialogueKey,
                    request.EventName, request.SinceSequence);
                response.Matched = response.Events.Count > 0;
                response.Code = response.Matched
                    ? DialogueResponseCode.Ok : DialogueResponseCode.Pending;
                response.Message = response.Matched
                    ? BuildEventsMessage(response.Events)
                    : "<pending event=\"" + DialogueMessage.Escape(request.EventName) + "\" />";
                return response;

            default:
                response.Code = DialogueResponseCode.InvalidRequest;
                response.Message = "<error>Unsupported request type.</error>";
                return response;
        }
    }

    public void SendAsync(DialogueRequest request,
                          Action<DialogueResponse> completed)
    {
        if (request == null)
        {
            completed?.Invoke(Send(null));
            return;
        }

        string clientId = !string.IsNullOrEmpty(request.ClientId)
            ? request.ClientId
            : request.RequestId;
        queryServer.EnqueueLatest(clientId, request, completed, -1);
    }

    public IEnumerator SendBlocking(DialogueRequest request,
                                    Action<DialogueResponse> completed)
    {
        if (request == null)
        {
            completed?.Invoke(Send(null));
            yield break;
        }

        // "Blocking" means a coroutine wait, never a busy loop: Unity's main
        // thread remains free to render, accept input and advance dialogue.
        float timeout = Mathf.Max(0.01f, request.TimeoutSeconds);
        float started = Time.realtimeSinceStartup;
        DialogueResponse response;
        do
        {
            response = Send(request);
            if (response.Code != DialogueResponseCode.Pending)
            {
                completed?.Invoke(response);
                yield break;
            }
            yield return null;
        }
        while (Time.realtimeSinceStartup - started < timeout);

        response.Code = DialogueResponseCode.Timeout;
        response.Message = "<timeout event=\"" +
            DialogueMessage.Escape(request.EventName) + "\" />";
        completed?.Invoke(response);
    }

    public Coroutine StartBlockingRequest(DialogueRequest request,
                                          Action<DialogueResponse> completed)
    {
        return StartCoroutine(SendBlocking(request, completed));
    }

    public void SendAsyncForClient(string clientId, DialogueRequest request,
                                   Action<DialogueResponse> completed)
    {
        if (request == null)
        {
            completed?.Invoke(Send(null));
            return;
        }
        request.ClientId = clientId ?? "";
        SendAsync(request, completed);
    }


    public DialogueResponse SendRequestForCaller(UnityEngine.Object caller,
                                                 DialogueRequest request)
    {
        if (caller == null)
        {
            return new DialogueResponse
            {
                RequestId = request != null ? request.RequestId : "",
                Code = DialogueResponseCode.InvalidRequest,
                Message = "<error>Caller is null. Use SendRequest(string clientId, request) for non-Unity clients.</error>"
            };
        }
        return SendRequestForClient(GetClientKey(caller), request);
    }

    static string GetClientKey(UnityEngine.Object unityObject)
    {
        if (unityObject == null) return "";
        #if UNITY_6000_5_OR_NEWER
        return unityObject.GetEntityId().ToString();
        #else
        return unityObject.GetInstanceID().ToString();
        #endif
    }

    public DialogueResponse SendRequestForClient(string clientId,
                                                 DialogueRequest request)
    {
        if (request == null)
            return Send(null);

        string resolvedClientId = string.IsNullOrEmpty(clientId)
            ? request.RequestId : clientId;
        DialogueResponse immediate = null;
        queryServer.EnqueueLatest(resolvedClientId, request,
            response => immediate = response,
            Mathf.Max(0, maxCoalescedSendRequestRetries));
        queryServer.Process(Mathf.Max(1, maxAsyncQueryClientsPerFrame),
            Time.frameCount, Send, OnDeferredQueryRequestDropped);

        if (immediate != null)
            return immediate;

        if (queryServer.ContainsPending(resolvedClientId, request.RequestId))
        {
            Debug.Log("Dialogue_Engine: one-shot request pending for client " +
                resolvedClientId + "; it will retry automatically next frame.");
            return new DialogueResponse
            {
                RequestId = request.RequestId,
                Code = DialogueResponseCode.Pending,
                Message = "<pending client=\"" + DialogueMessage.Escape(resolvedClientId) +
                    "\" request=\"" + DialogueMessage.Escape(request.Type.ToString()) +
                    "\" />"
            };
        }

        return new DialogueResponse
        {
            RequestId = request.RequestId,
            Code = DialogueResponseCode.Timeout,
            Message = "<error>One-shot request left the coalesced queue before resolution.</error>"
        };
    }

    void OnDeferredQueryRequestDropped(DialogueRequest request,
                                       DialogueResponse response)
    {
        if (request == null || response == null) return;
        Debug.LogWarning("Dialogue_Engine: deferred one-shot request dropped after retry limit. " +
            "RequestId=" + request.RequestId + ", Type=" + request.Type +
            ", Path=" + request.DialoguePath + ", Event=" + request.EventName);
    }

    public SnaphotSubID RegisterLiveSnapshotSubscription(
        Action<DialogueLiveSnapshot> callback, string clientId = "",
        string dialoguePathFilter = "", bool onlyOnChange = true,
        float minIntervalSeconds = 0f)
    {
        int id = liveSnapshotServer.Subscribe(clientId, dialoguePathFilter,
            callback, onlyOnChange, minIntervalSeconds);
        return id > 0 ? new SnaphotSubID(id) : null;
    }

    public void UnregisterLiveSnapshotSubscription(SnaphotSubID subscriptionId)
    {
        if (subscriptionId == null || !subscriptionId.IsValid)
        {
            Debug.LogError("Dialogue_Engine.UnregisterLiveSnapshotSubscription received an invalid SnaphotSubID.");
            return;
        }
        liveSnapshotServer.Unsubscribe(subscriptionId.Value);
    }

    public EventMonitorID RegisterLiveEventSubscription(Action<string> callback,
        string clientId = "", string dialoguePathFilter = "",
        string eventNameFilter = "")
    {
        int id = liveEventServer.Subscribe(clientId, dialoguePathFilter,
            eventNameFilter, callback);
        return id > 0 ? new EventMonitorID(id) : null;
    }

    public void UnregisterLiveEventSubscription(EventMonitorID subscriptionId)
    {
        if (subscriptionId == null || !subscriptionId.IsValid)
        {
            Debug.LogError("Dialogue_Engine.UnregisterLiveEventSubscription received an invalid EventMonitorID.");
            return;
        }
        liveEventServer.Unsubscribe(subscriptionId.Value);
    }

    public PriorityEventMonitorID RegisterPriorityLiveEventSubscription(
        Func<string, DialoguePriorityDispatchResult> callback,
        int priority, string clientId = "", string dialoguePathFilter = "",
        string eventNameFilter = "")
    {
        int id = priorityLiveEventServer.Subscribe(clientId, priority,
            dialoguePathFilter, eventNameFilter, callback);
        return id > 0 ? new PriorityEventMonitorID(id) : null;
    }

    public void UnregisterPriorityLiveEventSubscription(PriorityEventMonitorID subscriptionId)
    {
        if (subscriptionId == null || !subscriptionId.IsValid)
        {
            Debug.LogError("Dialogue_Engine.UnregisterPriorityLiveEventSubscription received an invalid PriorityEventMonitorID.");
            return;
        }
        priorityLiveEventServer.Unsubscribe(subscriptionId.Value);
    }

    public void UnregisterAllClientSubscriptions(string clientId)
    {
        liveSnapshotServer.UnsubscribeClient(clientId);
        liveEventServer.UnsubscribeClient(clientId);
        priorityLiveEventServer.UnsubscribeClient(clientId);
        queryServer.Cancel(clientId);
    }

    void MarkLiveSnapshotDirty()
    {
        liveSnapshotServer.MarkDirty(GetLiveSnapshot());
    }

    static string BuildEventsMessage(List<DialogueEventRecord> rows)
    {
        int rowCount = DialogueEventMetrics.CountRows(rows);
        int emittedCount = DialogueEventMetrics.CountEmittedEvents(rows);
        var b = new System.Text.StringBuilder("<events rows=\"")
            .Append(rowCount).Append("\" emitted=\"")
            .Append(emittedCount).Append("\">");
        if (rows != null)
        {
            foreach (DialogueEventRecord row in rows)
            {
                b.Append("<event sequence=\"").Append(row.Sequence)
                 .Append("\" dialogue=\"").Append(DialogueMessage.Escape(row.DialogueId))
                 .Append("\" timestamp=\"").Append(row.Timestamp)
                 .Append("\" text-name=\"").Append(DialogueMessage.Escape(row.TextName))
                 .Append("\" status=\"").Append(row.Status).Append("\">")
                 .Append(DialogueMessage.Escape(row.EmittedEvent)).Append("</event>");
            }
        }
        return b.Append("</events>").ToString();
    }

    bool AdvanceOrConfirmKeyPressed()
    {
        #if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.spaceKey.wasPressedThisFrame ||
             keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame))
            return true;
        #endif

        #if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter))
            return true;
        #endif

        return false;
    }

    bool AdvanceKeyPressed()
    {
        #if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.spaceKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame))
            return true;
        #endif

        #if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.KeypadEnter))
            return true;
        #endif

        return false;
    }

    bool SpeedUpKeyHeld()
    {
        #if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.leftCtrlKey.isPressed ||
             keyboard.rightCtrlKey.isPressed))
            return true;
        #endif

        #if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl))
            return true;
        #endif

        return false;
    }

    // ─── Update ────────────────────────────────────────────────────────────────
    void Update()
    {
        // Async one-shot requests are coalesced by client. Each frame resolves
        // only a bounded number of latest requests so monitoring traffic cannot
        // stall the rest of the engine in one update.
        queryServer.Process(Mathf.Max(1, maxAsyncQueryClientsPerFrame),
            Time.frameCount, Send, OnDeferredQueryRequestDropped);
        liveSnapshotServer.PublishDue(Time.realtimeSinceStartup);

        if (!isOpen) return;

        // Choices are mouse-only. Space/Enter must never advance the section
        // behind an unanswered choice or accidentally close the dialogue.
        if (IsChoiceAwaitingSelection() && AdvanceOrConfirmKeyPressed())
            return;

        if (AdvanceKeyPressed())
        {
            SetRuntimeStatus(DialogueRuntimeStatus.Transitioning,
                isTyping ? "Input completed typewriter" : "Enter/Space pressed");
            if (isTyping) { CompleteTextInstantly(); return; }
            AdvanceSection();
        }
    }

    // ─── AdvanceSection (Unchanged) ───────────────────────────────────────────
    void AdvanceSection()
    {
        if (currentSection == null || currentSection.Children == null || currentIndex >= currentSection.Children.Count)
        {
            if (sectionStack.Count > 0) sectionStack.Pop();

            if (sectionStack.Count > 0)
            {
                var parent = sectionStack.Peek();
                currentSection = parent.section;
                currentIndex   = parent.index;
            }
            else
            {
                CloseUI();
            }
            return;
        }

        var element = currentSection.Children[currentIndex];

        if (element is SectionToken childSection)
        {
            if (sectionStack.Count > 0)
            {
                sectionStack.Pop();
                sectionStack.Push((currentSection, currentIndex + 1));
            }
            sectionStack.Push((childSection, 0));
            currentSection = childSection;
            currentIndex   = 0;
            return;
        }

        if (element is EventToken eventToken)
        {
            currentIndex++;
            EmitEvent(eventToken);
            // Events do not wait for input; continue to the next narrative token.
            AdvanceSection();
            return;
        }

        if (element is CharacterToken ct)
        {
            ShowCharacter(ct);
            currentIndex++;
            return;
        }

        if (element is ChoiceToken choice)
        {
            ShowChoices(choice);
            currentIndex++;
            return;
        }

        currentIndex++;
    }

    // ─── ShowCharacter ─────────────────────────────────────────────────────────
    void ShowCharacter(CharacterToken ct)
    {
        currentCharacterToken = ct;
        currentChoiceToken = null;
        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);

        // Update portrait slots and speaker labels dynamically
        UpdatePortraitSlots(ct);

        // Text + typewriter
        currentFullText = ct.Text?.TrimEnd() ?? "";
        currentTextName = string.IsNullOrEmpty(ct.Speaker) ? "NARRATOR" : ct.Speaker;
        currentServiceText = currentFullText;

        // Push to history
        history.Add(new DialogueHistoryEntry { speaker = ct.Speaker, text = currentFullText });

        if (enableTypewriter && !string.IsNullOrEmpty(currentFullText))
        {
            SetRuntimeStatus(DialogueRuntimeStatus.TypingText, "Rendering dialogue text");
            typewriterCoroutine = StartCoroutine(TypeText(currentFullText));
        }
        else
        {
            RenderDialogueText(currentFullText);
            isTyping = false;
            SetRuntimeStatus(DialogueRuntimeStatus.WaitingForInput, "Waiting for Enter/Space");
        }

        if (choiceContainer != null) choiceContainer.style.display = DisplayStyle.None;
        if (choicePanelRoot != null) choicePanelRoot.style.display = DisplayStyle.None;

        Debug.Log($"Dialogue_Engine: [{ct.Speaker}]: {currentFullText}");
    }

    bool IsChoiceAwaitingSelection()
    {
        return currentChoiceToken != null &&
               ((choiceContainer != null && choiceContainer.style.display == DisplayStyle.Flex) ||
                (choicePanelRoot != null && choicePanelRoot.style.display == DisplayStyle.Flex));
    }

    // ─── ShowChoices ───────────────────────────────────────────────────────────
    void ShowChoices(ChoiceToken choice)
    {
        currentChoiceToken = choice;
        currentCharacterToken = null;

        // Visual-layout runtime: the layout's own designed choice panel.
        if (visualLayoutRuntimeActive && choicePanelRoot != null && visualChoiceButtons.Count > 0)
        {
            ShowVisualChoices(choice);
            return;
        }

        if (choiceContainer == null)
        { Debug.LogError("Dialogue_Engine: 'ChoiceContainer' not found in UXML."); return; }

        choiceContainer.Clear();
        choiceContainer.style.display = DisplayStyle.Flex;
        choiceButtons.Clear();
        choiceOptions.Clear();
        choiceHighlight = 0;

        if (choice.Children == null) return;

        foreach (var child in choice.Children)
        {
            if (child is OptionToken option)
            {
                var btn = new Button();
                btn.text = option.OptionText;
                btn.style.marginBottom = 4;
                btn.AddToClassList("dlg-choice-button");
                OptionToken captured = option;
                btn.clicked += () => OnOptionSelected(captured);
                choiceContainer.Add(btn);
                choiceButtons.Add(btn);
                choiceOptions.Add(captured);
            }
        }

        HighlightChoice(0);
        currentTextName = "CHOICE_" + choice.ChoiceIndex;
        currentServiceText = string.Join(" | ", choiceOptions.ConvertAll(o => o.OptionText));
        SetRuntimeStatus(DialogueRuntimeStatus.TakingChoice, "Waiting for a choice");
        Debug.Log($"Dialogue_Engine: Showing {choice.Children.Count} choices.");
    }

    // ─── Visual-layout choice rendering ───────────────────────────────────────
    void ShowVisualChoices(ChoiceToken choice)
    {
        if (choiceContainer != null) choiceContainer.style.display = DisplayStyle.None;
        if (choicePanelRoot != null) choicePanelRoot.style.display = DisplayStyle.None;
        choiceButtons.Clear();
        choiceOptions.Clear();
        choiceHighlight = 0;

        if (choice.Children != null)
        {
            foreach (var child in choice.Children)
                if (child is OptionToken option)
                    choiceOptions.Add(option);
        }

        if (choiceOptions.Count > visualChoiceButtons.Count)
            Debug.LogWarning($"Dialogue_Engine: the layout has {visualChoiceButtons.Count} choice buttons " +
                             $"but this choice has {choiceOptions.Count} options — showing the first " +
                             $"{visualChoiceButtons.Count}. Add button groups/leaves in the visual editor " +
                             "(up to 6 visual choices).");

        int shown = Mathf.Min(choiceOptions.Count, visualChoiceButtons.Count);
        for (int i = 0; i < visualChoiceButtons.Count; i++)
        {
            VisualElement button = visualChoiceButtons[i];
            if (button == null) continue;
            bool active = i < shown;
            button.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (active && i < visualChoiceButtonTexts.Count && visualChoiceButtonTexts[i] != null)
                visualChoiceButtonTexts[i].text = choiceOptions[i].OptionText;
        }

        // Hide button groups whose every button is unused.
        for (int g = 0; ; g++)
        {
            VisualElement group = choicePanelRoot.Q("ChoiceGroup" + g);
            if (group == null) break;
            bool anyVisible = false;
            foreach (VisualElement child in group.Children())
                if (child.style.display == DisplayStyle.Flex) { anyVisible = true; break; }
            group.style.display = anyVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        choicePanelRoot.style.display = DisplayStyle.Flex;

        currentTextName = "CHOICE_" + choice.ChoiceIndex;
        currentServiceText = string.Join(" | ", choiceOptions.ConvertAll(o => o.OptionText));
        SetRuntimeStatus(DialogueRuntimeStatus.TakingChoice, "Waiting for a choice");
        Debug.Log($"Dialogue_Engine: Showing {shown} choices on the visual choice panel.");
    }

    // ─── OnOptionSelected ─────────────────────────────────────────────────────
    void OnOptionSelected(OptionToken option)
    {
        currentChoiceToken = null;
        Debug.Log($"Dialogue_Engine: Option selected \"{option.OptionText}\" -> {option.TargetSectionID}");
        currentTextName = "OPTION_" + option.OptionIndex;
        currentServiceText = option.OptionText;
        SetRuntimeStatus(DialogueRuntimeStatus.ChoiceSelected,
            "Selected option; goto " + option.TargetSectionID);

        if (option.Event != null)
            EmitEvent(option.Event);


        if (choiceContainer != null) choiceContainer.style.display = DisplayStyle.None;
        if (choicePanelRoot != null) choicePanelRoot.style.display = DisplayStyle.None;

        if (!string.IsNullOrEmpty(option.TargetSectionID) &&
            graph.AdjacencyList.TryGetValue(option.TargetSectionID, out SectionToken target))
        {
            sectionStack.Clear();
            currentSection = target;
            currentIndex   = 0;
            sectionStack.Push((currentSection, 0));
            AdvanceSection();
        }
        else CloseUI();
    }

    // ─── Typewriter ────────────────────────────────────────────────────────────
    IEnumerator TypeText(string text)
    {
        isTyping = true;
        StartCaretBlink();
        RenderDialogueText("");
        if (typewriterStartDelay > 0f)
            yield return new WaitForSeconds(typewriterStartDelay);
        for (int i = 0; i <= text.Length; i++)
        {
            RenderDialogueText(text.Substring(0, i));
            // Hold Ctrl to speed through the typewriter.
            float delay = SpeedUpKeyHeld()
                ? typewriterSpeed * 0.12f : typewriterSpeed;
            yield return new WaitForSeconds(delay);
        }
        isTyping = false;
        StopCaretBlink();
        RenderDialogueText(currentFullText);
        SetRuntimeStatus(DialogueRuntimeStatus.WaitingForInput, "Waiting for Enter/Space");
    }

    void CompleteTextInstantly()
    {
        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        isTyping = false;
        StopCaretBlink();
        RenderDialogueText(currentFullText);
        SetRuntimeStatus(DialogueRuntimeStatus.WaitingForInput, "Waiting for Enter/Space");
    }

    // ─── Text rendering (normal label or per-letter behaviour) ────────────────
    void RenderDialogueText(string text)
    {
        shownText = text;
        RenderShownText();
    }

    void RenderShownText()
    {
        if (dialogueTextLabel == null || textScroll == null) return;
        var content = textScroll.contentContainer;

        string display = shownText;
        if (isTyping && caretOn && textLetterMode == LetterMode.Normal)
            display += "▌";   // typewriter caret

        if (textLetterMode == LetterMode.Normal)
        {
            if (dialogueTextLabel.parent != content) { content.Clear(); content.Add(dialogueTextLabel); }
            dialogueTextLabel.text = display;
            dialogueTextLabel.style.color = new StyleColor(textColour);
            dialogueTextLabel.style.fontSize = textFontSize;
            dialogueTextLabel.style.unityFont = textFont != null ? new StyleFont(textFont) : StyleKeyword.Null;
            dialogueTextLabel.style.letterSpacing = new StyleLength(textLetterSpacing);
            dialogueTextLabel.style.whiteSpace = WhiteSpace.Normal;
            dialogueTextLabel.style.display = DisplayStyle.Flex;
            textScroll.mode = ScrollViewMode.Vertical;
        }
        else
        {
            dialogueTextLabel.text = "";
            dialogueTextLabel.style.display = DisplayStyle.None;
            content.Clear();
            BuildLetterRows(content, display, textLetterMode, textLetterAmplitude,
                            textLetterFrequency, textLetterSpacing, textColour, textFontSize, textFont,
                            textLetterPhase, textLetterAnimationSpeed, true);
            textScroll.mode = ScrollViewMode.VerticalAndHorizontal;
        }
        ScrollToBottom();
    }

    void BuildLetterRows(VisualElement container, string text, LetterMode mode,
                         float amplitude, float frequency, float spacing,
                         Color colour, int fontSize, Font font)
    {
        BuildLetterRows(container, text, mode, amplitude, frequency, spacing,
            colour, fontSize, font, 0f, 2f, true);
    }

    void BuildLetterRows(VisualElement container, string text, LetterMode mode,
                         float amplitude, float frequency, float spacing,
                         Color colour, int fontSize, Font font,
                         float phaseOffset, float animSpeed, bool loop)
    {
        if (string.IsNullOrEmpty(text)) return;
        Justify j = textHAnchor == TextHAnchor.Center ? Justify.Center :
                    textHAnchor == TextHAnchor.Right  ? Justify.FlexEnd : Justify.FlexStart;
        string[] lines = text.Split('\n');
        foreach (string line in lines)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.minHeight = fontSize + amplitude * 2f + 4f;
            row.style.width = Length.Percent(100);
            row.style.justifyContent = j;
            int letterIndex = 0;
            foreach (char ch in line)
            {
                if (ch == ' ' || ch == '\t')
                {
                    var space = new Label(" ");
                    space.style.fontSize = fontSize;
                    space.style.width = fontSize * 0.45f + Mathf.Max(0f, spacing);
                    row.Add(space);
                    continue;
                }
                row.Add(MakeLetterLabel(ch.ToString(), letterIndex, mode, amplitude,
                    frequency, spacing, colour, fontSize, font, phaseOffset, animSpeed, loop));
                letterIndex++;
            }
            container.Add(row);
        }
    }

    Label MakeLetterLabel(string letter, int index, LetterMode mode,
                          float amplitude, float frequency, float spacing,
                          Color colour, int fontSize, Font font)
    {
        return MakeLetterLabel(letter, index, mode, amplitude, frequency, spacing,
            colour, fontSize, font, 0f, 2f, true);
    }

    Label MakeLetterLabel(string letter, int index, LetterMode mode,
                          float amplitude, float frequency, float spacing,
                          Color colour, int fontSize, Font font,
                          float phaseOffset, float animSpeed, bool loop)
    {
        float y = 0f;
        switch (mode)
        {
            case LetterMode.Wave:      y = Mathf.Sin(index * frequency + phaseOffset) * amplitude; break;
            case LetterMode.Zigzag:    y = (index % 2 == 0) ? -amplitude : amplitude; break;
            case LetterMode.Staircase: y = index * amplitude; break;
        }
        var lbl = new Label(letter);
        lbl.style.fontSize = fontSize;
        lbl.style.color = new StyleColor(colour);
        if (font != null) lbl.style.unityFont = new StyleFont(font);
        lbl.style.translate = new Translate(0, y, 0);
        lbl.style.marginRight = Mathf.Max(0f, spacing);
        lbl.style.whiteSpace = WhiteSpace.NoWrap;

        // Time-driven letter behaviours. Each letter animates itself and stops
        // as soon as it is detached from the panel (re-render / close / stop).
        if (mode == LetterMode.Shake || mode == LetterMode.Bounce || mode == LetterMode.FadeIn)
        {
            float speed = Mathf.Max(0.05f, animSpeed);
            bool  loops = loop;
            float seed  = index * 0.7f;
            float t0    = Time.unscaledTime;
            IVisualElementScheduledItem item = null;
            item = lbl.schedule.Execute(() =>
            {
                if (lbl.panel == null) { if (item != null) item.Pause(); return; }
                float t = (Time.unscaledTime - t0) * speed;
                switch (mode)
                {
                    case LetterMode.Shake:
                    {
                        float dx = (Mathf.PerlinNoise(t + seed, 0f) * 2f - 1f) * amplitude * 0.5f;
                        float dy = (Mathf.PerlinNoise(0f, t + seed) * 2f - 1f) * amplitude * 0.5f;
                        lbl.style.translate = new Translate(dx, dy, 0);
                        break;
                    }
                    case LetterMode.Bounce:
                    {
                        float hop = Mathf.Abs(Mathf.Sin(t * frequency + phaseOffset + seed)) * amplitude;
                        lbl.style.translate = new Translate(0, -hop, 0);
                        break;
                    }
                    case LetterMode.FadeIn:
                    {
                        float a = Mathf.Clamp01(t / Mathf.Max(0.1f, frequency * 2f));
                        var c = colour; c.a *= a;
                        lbl.style.color = new StyleColor(c);
                        if (!loops && a >= 1f && item != null) item.Pause();
                        break;
                    }
                }
                if (!loops && t > 4f && item != null) item.Pause();
            }).Every(30);
        }

        return lbl;
    }

    void ScrollToBottom()
    {
        if (textScroll == null) return;
        textScroll.schedule.Execute(() =>
        {
            if (textScroll != null)
                textScroll.verticalScroller.value = textScroll.verticalScroller.highValue;
        });
    }

    // ─── Name rendering ────────────────────────────────────────────────────────
    void RenderName(VisualElement nameContainer, string rawName)
    {
        if (nameContainer == null) return;
        string displayName = string.IsNullOrEmpty(rawName) ? "" : (nameUppercase ? rawName.ToUpper() : rawName);
        nameContainer.Clear();

        if (string.IsNullOrEmpty(displayName)) return;

        if (nameLetterMode == LetterMode.Normal)
        {
            var lbl = new Label(displayName);
            lbl.style.color = new StyleColor(nameColour);
            lbl.style.fontSize = nameFontSize;
            if (nameFont != null) lbl.style.unityFont = new StyleFont(nameFont);
            lbl.style.letterSpacing = new StyleLength(nameLetterSpacing);
            lbl.style.whiteSpace = WhiteSpace.NoWrap;
            nameContainer.Add(lbl);
        }
        else
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.minHeight = nameFontSize + nameLetterAmplitude * 2f + 4f;
            int idx = 0;
            foreach (char ch in displayName)
            {
                if (ch == ' ')
                {
                    var space = new Label(" ");
                    space.style.fontSize = nameFontSize;
                    space.style.width = nameFontSize * 0.45f + Mathf.Max(0f, nameLetterSpacing);
                    row.Add(space);
                    continue;
                }
                row.Add(MakeLetterLabel(ch.ToString(), idx, nameLetterMode, nameLetterAmplitude,
                                        nameLetterFrequency, nameLetterSpacing, nameColour, nameFontSize, nameFont,
                                        nameLetterPhase, nameLetterAnimationSpeed, true));
                idx++;
            }
            nameContainer.Add(row);
        }
    }

    // ─── Portrait loader ───────────────────────────────────────────────────────
    // Runtime sprite cache for file-path images (borders, backgrounds, etc.)
    static readonly Dictionary<string, Sprite> fileSpriteCache = new Dictionary<string, Sprite>();

    static Sprite GetFileSprite(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        if (fileSpriteCache.TryGetValue(path, out Sprite cached)) return cached;

        byte[] data = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(data)) return null;

        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        fileSpriteCache[path] = sprite;
        return sprite;
    }

    /// <summary>Sprite for a tiled layer: assigned Sprite first, file path second.</summary>
    static Sprite ResolveTiledSprite(TiledImageSettings s)
    {
        if (s == null) return null;
        if (s.sprite != null) return s.sprite;
        return GetFileSprite(s.path);
    }

    static bool HasTiledImage(TiledImageSettings s)
    {
        return ResolveTiledSprite(s) != null;
    }

    void SetSlotSizes(SlotRefs slot, float w, float h)
    {
        if (slot.frame    != null) { slot.frame.style.width = w;    slot.frame.style.height = h; }
        if (slot.portrait != null) { slot.portrait.style.width = w; slot.portrait.style.height = h; }
    }

    void ApplySlotSizeFromTexture(SlotRefs slot, Texture tex)
    {
        if (tex == null) return;
        // Visual-layout runtime: the portrait element was generated at the exact
        // component rect and must keep filling it — background-size (contain /
        // cover) does the image fitting. Resizing to the texture here would
        // shrink the panel away from the edited geometry.
        if (visualLayoutRuntimeActive) return;
        // Character-panel portraits fill their dedicated image section; the
        // root panel's own Default/Custom/Content modes control its dimensions.
        if (portraitPlacement == PortraitPlacement.CharacterPanel)
        {
            if (!dynamicPortraitSize) return;
            // Figure panels hug the loaded image, capped by the host space.
            ApplyFigureSizeToTexture(slot, tex);
            // Re-apply once the host has been through a layout pass so the
            // real parent size can act as the upper bound.
            SlotRefs captured = slot;
            Texture capturedTex = tex;
            slot.portrait.schedule.Execute(() =>
            {
                if (captured.portrait == null || captured.portrait.panel == null) return;
                ApplyFigureSizeToTexture(captured, capturedTex);
            }).StartingIn(120);
            return;
        }
        float w = portraitSize, h = portraitSize;
        if (portraitShape == PortraitShape.Rectangle) { w = portraitSize * 1.3f; h = portraitSize; }
        if (dynamicPortraitSize)
        {
            float scale = maxPortraitSize / Mathf.Max(tex.width, tex.height);
            w = tex.width * scale;
            h = tex.height * scale;
        }
        SetSlotSizes(slot, w, h);
    }

    /// <summary>
    /// Sizes a character-figure portrait to the image's aspect ratio, never
    /// exceeding the portrait size cap or the host container's resolved size.
    /// </summary>
    void ApplyFigureSizeToTexture(SlotRefs slot, Texture tex)
    {
        if (slot.portrait == null || tex == null || tex.width <= 0 || tex.height <= 0) return;

        float maxWidth = maxPortraitSize;
        float maxHeight = maxPortraitSize;
        if (slot.host != null)
        {
            float hostW = slot.host.resolvedStyle.width;
            float hostH = slot.host.resolvedStyle.height;
            if (!float.IsNaN(hostW) && hostW > 1f) maxWidth = Mathf.Min(maxWidth, hostW);
            if (!float.IsNaN(hostH) && hostH > 1f) maxHeight = Mathf.Min(maxHeight, hostH);
        }

        float scale = Mathf.Min(maxWidth / tex.width, maxHeight / tex.height);
        slot.portrait.style.width = tex.width * scale;
        slot.portrait.style.height = tex.height * scale;
        slot.portrait.style.flexGrow = 0f;
        slot.portrait.style.alignSelf = Align.Center;
    }

    void LoadPortraitFromPath(SlotRefs slot, string path)
    {
        if (slot.portrait == null) return;
        if (!File.Exists(path))
        { Debug.LogWarning($"Dialogue_Engine: Portrait file not found: \"{path}\""); return; }

        byte[]    data = File.ReadAllBytes(path);
        Texture2D tex  = new Texture2D(2, 2);
        if (tex.LoadImage(data))
        {
            ApplySlotSizeFromTexture(slot, tex);

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            slot.portrait.style.backgroundImage = new StyleBackground(sprite);
            ApplyPortraitFillMode(slot.portrait);
        }
        else Debug.LogWarning($"Dialogue_Engine: Failed to load portrait image at \"{path}\"");
    }

    void ApplyPortraitFillMode(VisualElement slot)
    {
        if (slot == null) return;
        switch (portraitFillMode)
        {
            case PortraitFillMode.Fit:       slot.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain); break;
            case PortraitFillMode.FillCrop:  slot.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);   break;
            default:                         slot.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100)); break;
        }
        ApplyPortraitFlip(slot);
    }

    void ApplyPortraitFlip(VisualElement portrait)
    {
        if (portrait == null) return;
        portrait.style.scale = new StyleScale(new Scale(new Vector3(
            portraitFlipHorizontal ? -1f : 1f, 1f, 1f)));
    }

    // ─── CloseUI ───────────────────────────────────────────────────────────────
    void CloseUI()
    {
        if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
        StopCaretBlink();
        StopHintPulse();
        isTyping       = false;
        isOpen         = false;
        isSuccess      = true;
        SetRuntimeStatus(DialogueRuntimeStatus.Completed, "Dialogue completed");

        if (suspendedDialogues.Count > 0)
        {
            if (choiceContainer != null) choiceContainer.style.display = DisplayStyle.None;
        if (choicePanelRoot != null) choicePanelRoot.style.display = DisplayStyle.None;
            RestorePlaybackState(suspendedDialogues.Pop());
            return;
        }

        graph          = null;
        currentSection = null;
        currentIndex   = 0;
        currentCharacterToken = null;
        currentChoiceToken = null;
        currentFullText = "";
        currentServiceText = "";
        currentTextName = "";
        lastEmittedEvent = "";
        currentDialogueInterruptible = false;
        currentDialogueSaveState = false;
        sectionStack.Clear();
        choiceButtons.Clear();
        choiceOptions.Clear();
        choiceHighlight = -1;

        // Visual cleanup happens after a short fade-out.
        System.Action finish = () =>
        {
            ResetPortraitSlots();
            if (historyPanel  != null) historyPanel.style.display  = DisplayStyle.None;
            if (settingsPanel != null) settingsPanel.style.display = DisplayStyle.None;
            if (toolbarPanel  != null) toolbarPanel.style.display  = DisplayStyle.None;
            if (choiceContainer != null)
            {
                choiceContainer.Clear();
                choiceContainer.style.display = DisplayStyle.None;
            }
            if (choicePanelRoot != null) choicePanelRoot.style.display = DisplayStyle.None;
            history.Clear();
            shownText = "";
            RenderDialogueText("");
            if (box != null) box.style.display = DisplayStyle.None;
        };
        PlayCloseAnimation(finish);

        #if UNITY_EDITOR
        if (portraits.Exists(p => p.sprite == null && string.IsNullOrEmpty(p.path)))
        {
            Debug.Log("Dialogue_Engine: The last script had unresolved portrait placeholders — " +
                      "assign image sources in the Inspector (Unresolved Portraits) before the next play.");
            EditorUtility.SetDirty(this);
        }
        #endif

        Debug.Log("Dialogue_Engine: UI closed.");
    }

    // ─── Portrait slot refs ────────────────────────────────────────────────────
    struct SlotRefs
    {
        public VisualElement wrapper, host, frame, portrait, overlay, name;
    }

    SlotRefs GetSlot(bool right)
    {
        switch (portraitPlacement)
        {
            case PortraitPlacement.Outside:
                return new SlotRefs { wrapper = right ? outsideRightWrapper : outsideLeftWrapper,
                                      host    = right ? outsideRightHost    : outsideLeftHost,
                                      frame   = right ? frameOutsideRight   : frameOutsideLeft,
                                      portrait= right ? portraitOutsideRight : portraitOutsideLeft,
                                      overlay = right ? overlayOutsideRight  : overlayOutsideLeft,
                                      name    = right ? nameOutsideRight     : nameOutsideLeft };
            case PortraitPlacement.OnBorder:
                return new SlotRefs { wrapper = right ? borderRightWrapper : borderLeftWrapper,
                                      host    = right ? borderRightHost    : borderLeftHost,
                                      frame   = right ? frameBorderRight   : frameBorderLeft,
                                      portrait= right ? portraitBorderRight : portraitBorderLeft,
                                      overlay = right ? overlayBorderRight  : overlayBorderLeft,
                                      name    = right ? nameBorderRight     : nameBorderLeft };
            case PortraitPlacement.CharacterPanel:
                return new SlotRefs { wrapper = right ? charRightWrapper : charLeftWrapper,
                                      host    = right ? charRightHost    : charLeftHost,
                                      frame   = right ? frameCharRight   : frameCharLeft,
                                      portrait= right ? portraitCharRight : portraitCharLeft,
                                      overlay = right ? overlayCharRight  : overlayCharLeft,
                                      name    = right ? nameCharRight     : nameCharLeft };
            default:
                return new SlotRefs { wrapper = right ? insideRightWrapper : insideLeftWrapper,
                                      host    = right ? insideRightHost    : insideLeftHost,
                                      frame   = right ? frameInsideRight   : frameInsideLeft,
                                      portrait= right ? portraitInsideRight : portraitInsideLeft,
                                      overlay = right ? overlayInsideRight  : overlayInsideLeft,
                                      name    = right ? nameInsideRight     : nameInsideLeft };
        }
    }

    // ─── Dual portrait slot management ────────────────────────────────────────
    void UpdatePortraitSlots(CharacterToken ct)
    {
        // Visual-layout runtime: the layout's own panels ARE the cast slots —
        // the k-th speaker owns the k-th image+name panel pair. This replaces
        // the classic single/dual model entirely (the layout is the explicit
        // opt-in, so engine portrait toggles do not veto it).
        if (visualLayoutRuntimeActive && visualRuntimeSlots.Count > 0)
        {
            UpdateVisualRuntimeSlots(ct);
            return;
        }

        if (!showPortrait || portraitMode == PortraitMode.None)
        {
            HideAllPortraitWrappers();
            return;
        }

        if (portraitMode == PortraitMode.Single)
        {
            var s = GetSlot(false);
            slotOwner[0] = ct.Speaker;
            slotTokens[0] = ct;
            SetPortraitContent(s, ct);
            RenderName(s.name, ct.Speaker);
            SetSlotOpacity(s.portrait, s.name, true, 0);
            ShowPortraitWrappers();
            ApplyNameLayout(GetSlot(false));
            ApplyNameLayout(GetSlot(true));
            return;
        }

        // Dual mode
        int speakerSlot = -1;
        for (int i = 0; i < 2; i++)
            if (slotOwner[i] == ct.Speaker) { speakerSlot = i; break; }

        if (speakerSlot == -1)
        {
            // New speaker — take the previous inactive person's slot. The
            // previously active speaker stays on their slot and becomes the
            // greyed-out inactive one.
            if      (slotOwner[0] == null) speakerSlot = 0;
            else if (slotOwner[1] == null) speakerSlot = 1;
            else                           speakerSlot = 1;

            slotOwner[speakerSlot] = ct.Speaker;
            slotTokens[speakerSlot] = ct;
            var slot = GetSlot(speakerSlot == 1);
            SetPortraitContent(slot, ct);
            RenderName(slot.name, ct.Speaker);
        }

        // The second panel stays hidden until a second speaker exists.
        ShowPortraitWrappers();
        ApplyNameLayout(GetSlot(false));
        ApplyNameLayout(GetSlot(true));

        // Update opacities for both slots (active vs inactive/greyed)
        for (int i = 0; i < 2; i++)
        {
            bool  active = slotOwner[i] == ct.Speaker;
            var   slot   = GetSlot(i == 1);
            SetSlotOpacity(slot.portrait, slot.name, active, i);
        }
    }

    // ─── Visual-layout runtime cast slots ─────────────────────────────────
    void UpdateVisualRuntimeSlots(CharacterToken ct, bool applyOpacity = true)
    {
        if (visualRuntimeSlots.Count == 0) return;
        string speaker = ct.Speaker;

        int slotIndex = -1;
        if (!string.IsNullOrEmpty(speaker))
            for (int i = 0; i < visualRuntimeSlots.Count; i++)
                if (visualRuntimeSlots[i].owner == speaker) { slotIndex = i; break; }
        if (slotIndex == -1)
        {
            // First appearance: take the next free panel. When the cast
            // outgrows the layout, the newest speaker takes the last panel.
            for (int i = 0; i < visualRuntimeSlots.Count; i++)
                if (string.IsNullOrEmpty(visualRuntimeSlots[i].owner)) { slotIndex = i; break; }
            if (slotIndex == -1) slotIndex = visualRuntimeSlots.Count - 1;
            visualRuntimeSlots[slotIndex].owner = speaker;
        }

        VisualRuntimeSlot slot = visualRuntimeSlots[slotIndex];
        SetVisualPortraitContent(slot, ct);
        if (slot.name != null) RenderName(slot.name, speaker);

        // Mirror the first two owners into the classic dual-slot state so
        // save/resume of interrupted dialogues keeps working.
        if (slotIndex < 2) { slotOwner[slotIndex] = speaker; slotTokens[slotIndex] = ct; }

        if (applyOpacity) ApplyVisualSlotOpacities(speaker);
    }

    void ApplyVisualSlotOpacities(string currentSpeaker)
    {
        for (int i = 0; i < visualRuntimeSlots.Count; i++)
        {
            VisualRuntimeSlot v = visualRuntimeSlots[i];
            if (v.wrapper == null) continue;
            bool owned = !string.IsNullOrEmpty(v.owner);
            v.wrapper.style.display = owned ? DisplayStyle.Flex : DisplayStyle.None;
            if (owned) SetVisualSlotOpacity(v, v.owner == currentSpeaker);
        }
    }

    void SetVisualSlotOpacity(VisualRuntimeSlot slot, bool active)
    {
        // Only the image and the name grey out — the panel/frame keep their
        // exact layout paint. Smoothly animated, both levels adjustable.
        float target = active ? activePortraitOpacity : inactivePortraitOpacity;
        Color tint   = active ? Color.white : inactiveTintColour;
        if (slot.tween != null) slot.tween.Pause();
        float from = slot.opacity;
        if (slot.portrait != null)
            slot.portrait.style.unityBackgroundImageTintColor = new StyleColor(tint);
        slot.tween = RunTween(0.22f, t =>
        {
            float v = Mathf.Lerp(from, target, t);
            slot.opacity = v;
            if (slot.portrait != null) slot.portrait.style.opacity = v;
            if (slot.name     != null) slot.name.style.opacity     = v;
        });
    }

    void SetVisualPortraitContent(VisualRuntimeSlot slot, CharacterToken ct)
    {
        if (slot.portrait == null) return;

        // Every token starts from an empty visual slot — no image survives
        // from the previous token. No invented placeholder either: a panel
        // with no image simply stays empty (exact WYSIWYG).
        slot.portrait.style.backgroundImage = new StyleBackground(StyleKeyword.None);

        Sprite sprite = ResolveCharacterSprite(ct);
        if (sprite != null)
        {
            slot.portrait.style.backgroundImage = new StyleBackground(sprite);
            slot.portrait.style.display = DisplayStyle.Flex;
            if (slot.frame != null) slot.frame.style.display = DisplayStyle.Flex;
            if (slot.panel != null) slot.panel.style.display = DisplayStyle.Flex;
        }
        else
        {
            slot.portrait.style.display = DisplayStyle.None;
            if (slot.frame != null) slot.frame.style.display = DisplayStyle.None;
            // "Visible only when an image exists" figure panels hide fully.
            if (slot.hidePanelWhenEmpty && slot.panel != null)
                slot.panel.style.display = DisplayStyle.None;
        }
    }

    Sprite ResolveCharacterSprite(CharacterToken ct)
    {
        if (ct == null || string.IsNullOrEmpty(ct.ImageSource)) return null;
        if (ct.ImageIsUnresolved)
        {
            var entry = portraits.Find(pr => pr.key == ct.ImageSource);
            if (entry != null)
            {
                if (entry.sprite != null) return entry.sprite;
                if (!string.IsNullOrEmpty(entry.path) && File.Exists(entry.path))
                    return GetFileSprite(entry.path);
            }
            return null;
        }
        return File.Exists(ct.ImageSource) ? GetFileSprite(ct.ImageSource) : null;
    }

    void ShowPortraitWrappers()
    {
        bool dual    = portraitMode == PortraitMode.Dual;
        bool leftOn  = slotOwner[0] != null;
        bool rightOn = dual && slotOwner[1] != null;   // second panel appears only when a second person comes in

        if (insideLeftWrapper   != null) insideLeftWrapper.style.display   = portraitPlacement == PortraitPlacement.Inside   && leftOn  ? DisplayStyle.Flex : DisplayStyle.None;
        if (insideRightWrapper  != null) insideRightWrapper.style.display  = portraitPlacement == PortraitPlacement.Inside   && rightOn ? DisplayStyle.Flex : DisplayStyle.None;
        if (outsideLeftWrapper  != null) outsideLeftWrapper.style.display  = portraitPlacement == PortraitPlacement.Outside  && leftOn  ? DisplayStyle.Flex : DisplayStyle.None;
        if (outsideRightWrapper != null) outsideRightWrapper.style.display = portraitPlacement == PortraitPlacement.Outside  && rightOn ? DisplayStyle.Flex : DisplayStyle.None;
        if (borderLeftWrapper   != null) borderLeftWrapper.style.display   = portraitPlacement == PortraitPlacement.OnBorder && leftOn  ? DisplayStyle.Flex : DisplayStyle.None;
        if (borderRightWrapper  != null) borderRightWrapper.style.display  = portraitPlacement == PortraitPlacement.OnBorder && rightOn ? DisplayStyle.Flex : DisplayStyle.None;
        // Character panels are stable root columns around the main panel. In
        // Dual mode reserve both roots immediately, even before speaker two
        // arrives, so the main panel never jumps sideways or changes width.
        if (charLeftWrapper != null)
            charLeftWrapper.style.display = portraitPlacement == PortraitPlacement.CharacterPanel && leftOn
                ? DisplayStyle.Flex : DisplayStyle.None;
        if (charRightWrapper != null)
            charRightWrapper.style.display = portraitPlacement == PortraitPlacement.CharacterPanel && dual
                ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void HideAllPortraitWrappers()
    {
        if (insideLeftWrapper   != null) insideLeftWrapper.style.display   = DisplayStyle.None;
        if (insideRightWrapper  != null) insideRightWrapper.style.display  = DisplayStyle.None;
        if (outsideLeftWrapper  != null) outsideLeftWrapper.style.display  = DisplayStyle.None;
        if (outsideRightWrapper != null) outsideRightWrapper.style.display = DisplayStyle.None;
        if (borderLeftWrapper   != null) borderLeftWrapper.style.display   = DisplayStyle.None;
        if (borderRightWrapper  != null) borderRightWrapper.style.display  = DisplayStyle.None;
        if (charLeftWrapper     != null) charLeftWrapper.style.display     = DisplayStyle.None;
        if (charRightWrapper    != null) charRightWrapper.style.display    = DisplayStyle.None;
    }

    SlotRefs[] GetAllPortraitSlots()
    {
        return new[]
        {
            new SlotRefs { wrapper = insideLeftWrapper, host = insideLeftHost, frame = frameInsideLeft,
                portrait = portraitInsideLeft, overlay = overlayInsideLeft, name = nameInsideLeft },
            new SlotRefs { wrapper = insideRightWrapper, host = insideRightHost, frame = frameInsideRight,
                portrait = portraitInsideRight, overlay = overlayInsideRight, name = nameInsideRight },
            new SlotRefs { wrapper = outsideLeftWrapper, host = outsideLeftHost, frame = frameOutsideLeft,
                portrait = portraitOutsideLeft, overlay = overlayOutsideLeft, name = nameOutsideLeft },
            new SlotRefs { wrapper = outsideRightWrapper, host = outsideRightHost, frame = frameOutsideRight,
                portrait = portraitOutsideRight, overlay = overlayOutsideRight, name = nameOutsideRight },
            new SlotRefs { wrapper = borderLeftWrapper, host = borderLeftHost, frame = frameBorderLeft,
                portrait = portraitBorderLeft, overlay = overlayBorderLeft, name = nameBorderLeft },
            new SlotRefs { wrapper = borderRightWrapper, host = borderRightHost, frame = frameBorderRight,
                portrait = portraitBorderRight, overlay = overlayBorderRight, name = nameBorderRight },
            new SlotRefs { wrapper = charLeftWrapper, host = charLeftHost, frame = frameCharLeft,
                portrait = portraitCharLeft, overlay = overlayCharLeft, name = nameCharLeft },
            new SlotRefs { wrapper = charRightWrapper, host = charRightHost, frame = frameCharRight,
                portrait = portraitCharRight, overlay = overlayCharRight, name = nameCharRight }
        };
    }

    void ResetPortraitSlots()
    {
        // Visual-layout runtime: clear the indexed cast slots — owners, paint
        // and tweens only, NEVER their geometry (the layout owns the rects).
        if (visualRuntimeSlots != null)
        {
            foreach (VisualRuntimeSlot v in visualRuntimeSlots)
            {
                if (v.tween != null) v.tween.Pause();
                v.owner = null;
                v.opacity = 1f;
                if (v.wrapper  != null) v.wrapper.style.display  = DisplayStyle.None;
                if (v.panel    != null) v.panel.style.display    = DisplayStyle.Flex;
                if (v.frame    != null) v.frame.style.display    = DisplayStyle.Flex;
                if (v.portrait != null)
                {
                    v.portrait.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                    v.portrait.style.unityBackgroundImageTintColor = new StyleColor(Color.white);
                    v.portrait.style.opacity = 1f;
                    v.portrait.style.display = DisplayStyle.None;
                }
                if (v.name != null) { v.name.Clear(); v.name.style.opacity = 1f; }
            }
        }

        slotOwner[0] = null;
        slotOwner[1] = null;
        slotTokens[0] = null;
        slotTokens[1] = null;
        slotCur0 = 1f;
        slotCur1 = 1f;
        if (slotTween0 != null) { slotTween0.Pause(); slotTween0 = null; }
        if (slotTween1 != null) { slotTween1.Pause(); slotTween1 = null; }

        // Reset every placement, not only the currently selected placement.
        // This is what makes each independent Play begin from inspector UI
        // defaults even if the previous DSL used another portrait placement.
        foreach (SlotRefs slot in GetAllPortraitSlots())
        {
            if (slot.wrapper != null) slot.wrapper.style.display = DisplayStyle.None;
            if (slot.name != null)
            {
                slot.name.Clear();
                slot.name.style.opacity = 1f;
            }
            if (slot.portrait != null)
            {
                // StyleKeyword.None is a hard inline clear. A default
                // StyleBackground can fall back to a previously resolved style
                // during the same UI Toolkit panel lifetime.
                slot.portrait.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                slot.portrait.style.unityBackgroundImageTintColor = new StyleColor(Color.white);
                slot.portrait.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                slot.portrait.style.opacity = 1f;
                slot.portrait.style.display = DisplayStyle.None;
                slot.portrait.style.scale = new StyleScale(new Scale(Vector3.one));
            }
            bool characterSlot = slot.wrapper == charLeftWrapper || slot.wrapper == charRightWrapper;
            float defaultWidth = portraitShape == PortraitShape.Rectangle
                ? portraitSize * 1.3f : portraitSize;
            // Visual-layout runtime: every wrapper was generated at the exact
            // component rects — only reset visibility/paint, never the sizes.
            bool preserveGeometry = visualLayoutRuntimeActive;
            if (slot.frame != null)
            {
                if (characterSlot)
                {
                    slot.frame.style.width = Length.Percent(100);
                    slot.frame.style.height = Length.Percent(100);
                }
                else if (!preserveGeometry)
                {
                    slot.frame.style.width = defaultWidth;
                    slot.frame.style.height = portraitSize;
                }
                slot.frame.style.display = DisplayStyle.None;
                slot.frame.style.opacity = 1f;
            }
            if (slot.portrait != null && !characterSlot && !preserveGeometry)
            {
                slot.portrait.style.width = defaultWidth;
                slot.portrait.style.height = portraitSize;
            }
            if (slot.host != null)
                slot.host.style.translate = new Translate(portraitOffsetX, portraitOffsetY, 0);
            if (slot.overlay != null)
            {
                if (tilers.TryGetValue(slot.overlay, out TilerRuntime old) && old.sched != null)
                    old.sched.Pause();
                tilers.Remove(slot.overlay);
                layerSizes.Remove(slot.overlay);
                slot.overlay.Clear();
                slot.overlay.style.display = DisplayStyle.None;
            }
        }

        // Character image partitions reserve layout space but have no paint of
        // their own until SetPortraitContent loads a real image.
        foreach (VisualElement imagePanel in new[] { charLeftImagePanel, charRightImagePanel })
        {
            if (imagePanel == null) continue;
            imagePanel.style.backgroundColor = new StyleColor(Color.clear);
            imagePanel.style.borderLeftWidth = 0f;
            imagePanel.style.borderRightWidth = 0f;
            imagePanel.style.borderTopWidth = 0f;
            imagePanel.style.borderBottomWidth = 0f;
        }

        HideAllPortraitWrappers();
        ApplyCharacterPanelDecorations();
    }

    void SetPortraitContent(SlotRefs slot, CharacterToken ct)
    {
        if (slot.portrait == null) return;

        // Every character token starts from an empty visual slot. This prevents
        // a portrait from the previous token or previous DSL surviving when the
        // new token has no image declaration.
        slot.portrait.style.backgroundImage = new StyleBackground(StyleKeyword.None);
        slot.portrait.style.display = DisplayStyle.None;
        if (slot.frame != null) slot.frame.style.display = DisplayStyle.None;
        if (slot.overlay != null) slot.overlay.style.display = DisplayStyle.None;

        bool hasImage = false;
        bool hasSourceImage = false; // excludes generated/default placeholders

        if (!string.IsNullOrEmpty(ct.ImageSource))
        {
            if (ct.ImageIsUnresolved)
            {
                var entry = portraits.Find(p => p.key == ct.ImageSource);
                if (entry != null)
                {
                    if (entry.sprite != null)
                    {
                        slot.portrait.style.backgroundImage = new StyleBackground(entry.sprite);
                        hasImage = true;
                        hasSourceImage = true;
                        ApplyPortraitFillMode(slot.portrait);
                        if (dynamicPortraitSize) ApplySlotSizeFromTexture(slot, entry.sprite.texture);
                    }
                    else if (!string.IsNullOrEmpty(entry.path) && File.Exists(entry.path))
                    {
                        LoadPortraitFromPath(slot, entry.path);
                        hasImage = slot.portrait.style.backgroundImage.value.sprite != null;
                        hasSourceImage = hasImage;
                    }
                }
            }
            else if (File.Exists(ct.ImageSource))
            {
                LoadPortraitFromPath(slot, ct.ImageSource);
                hasImage = slot.portrait.style.backgroundImage.value.sprite != null;
                hasSourceImage = hasImage;
            }
        }

        if (!hasImage)
        {
            slot.portrait.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            Sprite placeholder = null;
            // Character Panel image partitions intentionally remain completely
            // invisible until that character has a real image. Placeholder
            // behavior remains available for the other portrait placements.
            if (portraitPlacement != PortraitPlacement.CharacterPanel &&
                useDefaultPortraitPlaceholder && showPortraitWhenEmpty)
            {
                placeholder = defaultPortraitSprite;
                if (placeholder == null && !string.IsNullOrEmpty(defaultPortraitPath))
                    placeholder = GetFileSprite(defaultPortraitPath);
                if (placeholder == null)
                    placeholder = GetSilhouetteSprite();
            }
            if (placeholder != null)
            {
                slot.portrait.style.backgroundImage = new StyleBackground(placeholder);
                ApplyPortraitFillMode(slot.portrait);
                hasImage = true;
            }
        }

        bool hasBorderImage = HasTiledImage(portraitBorderImage);

        // Character image sections use the custom colour only when there is no
        // user-provided image. A real portrait normally gets a transparent
        // panel so its own silhouette/shape is not boxed by an extra backdrop.
        if (portraitPlacement == PortraitPlacement.CharacterPanel && !visualLayoutRuntimeActive)
        {
            VisualElement imagePanel = slot.wrapper == charRightWrapper
                ? charRightImagePanel : charLeftImagePanel;
            if (imagePanel != null)
            {
                // The image partition keeps its layout space, but paints
                // absolutely nothing until a real image has loaded.
                imagePanel.style.backgroundColor = new StyleColor(Color.clear);
                float imageBorder = hasSourceImage && characterImagePanelShowBorder
                    ? characterImagePanelBorderWidth : 0f;
                imagePanel.style.borderLeftWidth = imageBorder;
                imagePanel.style.borderRightWidth = imageBorder;
                imagePanel.style.borderTopWidth = imageBorder;
                imagePanel.style.borderBottomWidth = imageBorder;
            }
        }

        bool characterPanelImage = portraitPlacement == PortraitPlacement.CharacterPanel;
        bool forceEmpty = !characterPanelImage && showPortraitWhenEmpty;

        // Character Panel image/frame elements only appear for a loaded image.
        slot.portrait.style.display = (hasImage || forceEmpty) ? DisplayStyle.Flex : DisplayStyle.None;

        if (slot.frame != null)
            slot.frame.style.display = (hasImage || forceEmpty || (!characterPanelImage && hasBorderImage))
                ? DisplayStyle.Flex : DisplayStyle.None;

        // Border-image overlay: visible exactly when a border image is loaded
        // (NOT when the portrait image is loaded — this was the bug that made
        // portrait border images never appear).
        if (slot.overlay != null)
            slot.overlay.style.display = hasBorderImage && (!characterPanelImage || hasSourceImage)
                ? DisplayStyle.Flex : DisplayStyle.None;

        ApplyPortraitFrame(slot);
        // ApplyPortraitFrame configures reusable border styling and may make a
        // border overlay visible; enforce the Character Panel's image-gated
        // visibility after that styling pass.
        if (characterPanelImage && !hasSourceImage)
        {
            slot.portrait.style.display = DisplayStyle.None;
            if (slot.frame != null) slot.frame.style.display = DisplayStyle.None;
            if (slot.overlay != null) slot.overlay.style.display = DisplayStyle.None;
        }
    }

    void ApplyPortraitFrame(SlotRefs slot)
    {
        if (slot.frame == null || slot.portrait == null) return;
        // Visual-layout runtime: the frame styling comes straight from the
        // image component definition in the generated UXML (exact per-side
        // borders and per-corner radii); do not overwrite it.
        if (visualLayoutRuntimeActive) return;

        // Radius is derived from the inspector settings (not measured layout)
        // so it is correct even before the element's first layout pass.
        float pw = portraitShape == PortraitShape.Rectangle ? portraitSize * 1.3f : portraitSize;
        float ph = portraitSize;
        float radius = 0f;
        switch (portraitShape)
        {
            case PortraitShape.Circle:  radius = Mathf.Min(pw, ph) * 0.5f; break;
            case PortraitShape.Rounded: radius = portraitBorderRadius; break;
        }

        // ── Frame carries the border so it stays crisp at all times ──────────
        bool hasBorderImage = HasTiledImage(portraitBorderImage);

        if (showPortraitBorder && !hasBorderImage)
        {
            slot.frame.style.borderLeftWidth   = portraitBorderWidth;
            slot.frame.style.borderRightWidth  = portraitBorderWidth;
            slot.frame.style.borderTopWidth    = portraitBorderWidth;
            slot.frame.style.borderBottomWidth = portraitBorderWidth;
            // Fully opaque — translucent portrait borders read as greyed out.
            var bc = new StyleColor(new Color(portraitBorderColour.r, portraitBorderColour.g, portraitBorderColour.b, 1f));
            slot.frame.style.borderLeftColor = bc;
            slot.frame.style.borderRightColor = bc;
            slot.frame.style.borderTopColor = bc;
            slot.frame.style.borderBottomColor = bc;
        }
        else
        {
            // No border colour when a border image is active — the image wins.
            slot.frame.style.borderLeftWidth = 0;
            slot.frame.style.borderRightWidth = 0;
            slot.frame.style.borderTopWidth = 0;
            slot.frame.style.borderBottomWidth = 0;
        }

        slot.frame.style.borderTopLeftRadius     = radius;
        slot.frame.style.borderTopRightRadius    = radius;
        slot.frame.style.borderBottomLeftRadius  = radius;
        slot.frame.style.borderBottomRightRadius = radius;

        // Image clips to the same rounded rect (frame has overflow:hidden).
        slot.portrait.style.borderTopLeftRadius     = radius;
        slot.portrait.style.borderTopRightRadius    = radius;
        slot.portrait.style.borderBottomLeftRadius  = radius;
        slot.portrait.style.borderBottomRightRadius = radius;

        if (slot.overlay != null)
        {
            slot.overlay.style.borderTopLeftRadius     = radius;
            slot.overlay.style.borderTopRightRadius    = radius;
            slot.overlay.style.borderBottomLeftRadius  = radius;
            slot.overlay.style.borderBottomRightRadius = radius;

            slot.overlay.style.display = hasBorderImage ? DisplayStyle.Flex : DisplayStyle.None;

            if (hasBorderImage)
                ScheduleBorderRebuild(slot.overlay, portraitBorderImage, portraitBorderWidth);
            else
            {
                if (tilers.TryGetValue(slot.overlay, out TilerRuntime old) && old.sched != null) old.sched.Pause();
                tilers.Remove(slot.overlay);
                slot.overlay.Clear();
            }
        }

        if (slot.host != null)
            slot.host.style.translate = new Translate(portraitOffsetX, portraitOffsetY, 0);
    }

    /// <summary>
    /// Rebuilds a border-image layer once its element has a valid layout size
    /// (retries a few frames — absolute overlays may not have a size on the
    /// first layout pass).
    /// </summary>
    void ScheduleBorderRebuild(VisualElement clip, TiledImageSettings settings, float thickness, int attempts = 8)
    {
        if (clip == null || settings == null || attempts <= 0) return;
        clip.schedule.Execute(() =>
        {
            if (clip.layout.width <= 0f || clip.layout.height <= 0f)
                ScheduleBorderRebuild(clip, settings, thickness, attempts - 1);
            else
                RebuildBorderImageLayer(clip, settings, thickness);
        });
    }

    void SetSlotOpacity(VisualElement portrait, VisualElement name, bool active, int slotIndex = 0)
    {
        // Only the image and the name grey out — the border frame (and any
        // border image) stays at full strength, so border colours are never
        // "filtered down" by the inactive state. The change is smoothly
        // animated instead of snapping.
        float target = active ? activePortraitOpacity : inactivePortraitOpacity;
        Color tint   = active ? Color.white : inactiveTintColour;

        float from = slotIndex == 0 ? slotCur0 : slotCur1;

        var tween = slotIndex == 0 ? slotTween0 : slotTween1;
        if (tween != null) tween.Pause();

        if (portrait != null) portrait.style.unityBackgroundImageTintColor = new StyleColor(tint);

        int idx = slotIndex;
        tween = RunTween(0.22f, t =>
        {
            float v = Mathf.Lerp(from, target, t);
            if (idx == 0) slotCur0 = v; else slotCur1 = v;
            if (portrait != null) portrait.style.opacity = v;
            if (name     != null) name.style.opacity     = v;
        });

        if (idx == 0) slotTween0 = tween; else slotTween1 = tween;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PROFESSIONAL POLISH — tweens, animations, caret, ambient FX, input
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Simple scheduler-based tween (smoothstep easing).</summary>
    IVisualElementScheduledItem RunTween(float duration, Action<float> step, Action done = null)
    {
        var root = box != null ? box : (document != null ? document.rootVisualElement : null);
        if (root == null || duration <= 0.001f)
        {
            step(1f);
            if (done != null) done();
            return null;
        }

        int steps = Mathf.Max(1, Mathf.RoundToInt(duration / 0.016f));
        int i = 0;
        IVisualElementScheduledItem item = null;
        item = root.schedule.Execute(() =>
        {
            i++;
            float t = Mathf.Clamp01(i / (float)steps);
            step(t * t * (3f - 2f * t));   // smoothstep
            if (t >= 1f)
            {
                if (item != null) item.Pause();
                if (done != null) done();
            }
        }).Every(16);
        return item;
    }

    void PlayOpenAnimation()
    {
        if (rowContainer == null) return;
        if (openTween != null) openTween.Pause();
        rowContainer.style.opacity = 0f;
        rowContainer.style.translate = new Translate(0, 26, 0);
        openTween = RunTween(0.28f, t =>
        {
            if (rowContainer == null) return;
            rowContainer.style.opacity = t;
            rowContainer.style.translate = new Translate(0, 26f * (1f - t), 0);
        });
    }

    void PlayCloseAnimation(Action onDone)
    {
        if (rowContainer == null)
        {
            if (onDone != null) onDone();
            return;
        }
        if (openTween != null) openTween.Pause();
        openTween = RunTween(0.18f, t =>
        {
            if (rowContainer == null) return;
            rowContainer.style.opacity = 1f - t;
            rowContainer.style.translate = new Translate(0, -10f * t, 0);
        }, onDone);
    }

    // ── Typewriter caret blink ────────────────────────────────────────────────
    void StartCaretBlink()
    {
        StopCaretBlink();
        caretOn = true;
        var root = box != null ? box : (document != null ? document.rootVisualElement : null);
        if (root == null) return;
        caretBlinkTask = root.schedule.Execute(() =>
        {
            caretOn = !caretOn;
            if (isTyping) RenderShownText();
            else StopCaretBlink();
        }).Every(400);
    }

    void StopCaretBlink()
    {
        if (caretBlinkTask != null) { caretBlinkTask.Pause(); caretBlinkTask = null; }
    }

    // ── Advance hint pulse ────────────────────────────────────────────────────
    void StartHintPulse()
    {
        StopHintPulse();
        if (!showAdvanceHint || advanceHintLabel == null) return;
        var root = box != null ? box : (document != null ? document.rootVisualElement : null);
        if (root == null) return;
        hintPhase = 0f;
        hintPulseTask = root.schedule.Execute(() =>
        {
            if (advanceHintLabel == null || !isOpen) { StopHintPulse(); return; }
            hintPhase += 0.22f;
            advanceHintLabel.style.opacity = Mathf.Clamp01(0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(hintPhase)));
        }).Every(60);
    }

    void StopHintPulse()
    {
        if (hintPulseTask != null) { hintPulseTask.Pause(); hintPulseTask = null; }
        if (advanceHintLabel != null) advanceHintLabel.style.opacity = 1f;
    }

    // ── Keyboard navigation for choices (Up/Down + Enter) ─────────────────────
    void OnKeyDown(KeyDownEvent evt)
    {
        if (!isOpen || evt == null) return;
        if (choiceContainer != null && choiceContainer.style.display == DisplayStyle.Flex &&
            choiceButtons.Count > 0)
        {
            if (evt.keyCode == KeyCode.DownArrow || evt.keyCode == KeyCode.UpArrow)
            {
                int dir = evt.keyCode == KeyCode.DownArrow ? 1 : -1;
                HighlightChoice((choiceHighlight + dir + choiceButtons.Count) % choiceButtons.Count);
                evt.StopPropagation();
                return;
            }
            if (evt.keyCode == KeyCode.Return ||
                evt.keyCode == KeyCode.KeypadEnter ||
                evt.keyCode == KeyCode.Space)
            {
                // Option selection is intentionally click-only.
                evt.StopPropagation();
                return;
            }
        }
        if (evt.keyCode == KeyCode.Escape && historyPanel != null &&
            historyPanel.style.display == DisplayStyle.Flex)
        {
            HideHistory();
            if (settingsPanel != null) HideSettings();
        }
    }

    void HighlightChoice(int index)
    {
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            if (choiceButtons[i] == null) continue;
            if (i == index) choiceButtons[i].AddToClassList("dlg-choice-selected");
            else            choiceButtons[i].RemoveFromClassList("dlg-choice-selected");
        }
        choiceHighlight = index;
    }

    // ── Click-to-advance (inside the box, ignoring buttons) ───────────────────
    void OnBoxClicked(ClickEvent evt)
    {
        if (!isOpen || evt == null) return;

        // Only clicks inside the box advance; interactive children are ignored.
        var t = evt.target as VisualElement;
        bool insideBox = false;
        while (t != null)
        {
            if (t == box) { insideBox = true; break; }
            if (t is Button) return;                       // choices, toolbar…
            if (t == choiceContainer || t == historyPanel ||
                t == settingsPanel || t == toolbarPanel) return;
            t = t.parent;
        }
        if (!insideBox) return;

        SetRuntimeStatus(DialogueRuntimeStatus.Transitioning,
            isTyping ? "Click completed typewriter" : "Dialogue panel clicked");
        if (isTyping) CompleteTextInstantly();
        else AdvanceSection();
    }

    // ── Default portrait placeholder: shaded unidentified-character silhouette ─
    static Sprite silhouetteSprite;

    public static Sprite GetSilhouetteSprite()
    {
        if (silhouetteSprite != null) return silhouetteSprite;

        int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color[s * s];
        Color body  = new Color(0.16f, 0.16f, 0.18f, 1f);
        Color clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s;
                float v = (y + 0.5f) / s;
                bool inside = false;

                // Head
                float dx = u - 0.5f, dy = v - 0.30f;
                if (dx * dx + dy * dy <= 0.14f * 0.14f) inside = true;

                // Torso (shoulders → base)
                if (!inside && v >= 0.52f && v <= 0.95f)
                {
                    float t = (v - 0.52f) / 0.43f;
                    float half = Mathf.Lerp(0.20f, 0.34f, t);
                    if (Mathf.Abs(u - 0.5f) <= half) inside = true;
                }

                px[y * s + x] = inside ? body : clear;
            }

        tex.SetPixels(px);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        silhouetteSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        return silhouetteSprite;
    }

    // ─── Name layout (position + distance relative to the portrait) ───────────
    void ApplyNameLayout(SlotRefs slot)
    {
        // Visual-layout runtime: the name element is absolutely positioned at
        // the name-panel component rect; the flex re-flow here would destroy it.
        if (visualLayoutRuntimeActive) return;
        if (slot.wrapper == null || slot.host == null || slot.name == null) return;

        // Character figure panels manage their own structure (image panel +
        // name panel); the free-floating name layout does not apply there.
        if (portraitPlacement == PortraitPlacement.CharacterPanel) return;

        bool horizontal = namePosition == NamePosition.Left || namePosition == NamePosition.Right;
        slot.wrapper.style.flexDirection = horizontal ? FlexDirection.Row : FlexDirection.Column;

        slot.name.style.marginTop = 0; slot.name.style.marginBottom = 0;
        slot.name.style.marginLeft = 0; slot.name.style.marginRight = 0;

        switch (namePosition)
        {
            case NamePosition.Left:  slot.name.style.marginRight = nameDistance; break;
            case NamePosition.Right: slot.name.style.marginLeft  = nameDistance; break;
            case NamePosition.Above: slot.name.style.marginBottom = nameDistance; break;
            case NamePosition.Below: slot.name.style.marginTop   = nameDistance; break;
        }

        bool nameFirst = namePosition == NamePosition.Left || namePosition == NamePosition.Above;
        slot.wrapper.Clear();
        if (nameFirst) { slot.wrapper.Add(slot.name); slot.wrapper.Add(slot.host); }
        else           { slot.wrapper.Add(slot.host); slot.wrapper.Add(slot.name); }
    }

    // ─── Toolbar ───────────────────────────────────────────────────────────────
    void ToggleToolbar()
    {
        toolbarVisible = !toolbarVisible;
        if (toolbarPanel != null)
            toolbarPanel.style.display = toolbarVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void OnRewind()
    {
        // Simple rewind for now: bring the history overlay back up.
        ShowHistory();
    }

    // ─── History ───────────────────────────────────────────────────────────────
    void ShowHistory()
    {
        if (historyPanel == null || historyContent == null) return;
        historyContent.Clear();

        for (int i = 0; i < history.Count; i++)
        {
            var e = history[i];

            var entryBox  = new VisualElement();
            entryBox.AddToClassList("dlg-history-entry");
            entryBox.style.marginBottom = 8;

            var speakerLbl = new Label(nameUppercase ? e.speaker.ToUpper() : e.speaker);
            speakerLbl.style.color    = new StyleColor(nameColour);
            speakerLbl.style.fontSize = 13;
            speakerLbl.style.unityFontStyleAndWeight = FontStyle.Bold;

            var textLbl = new Label(e.text);
            textLbl.style.color      = new StyleColor(new Color(0.75f, 0.75f, 0.75f, 1f));
            textLbl.style.fontSize   = 12;
            textLbl.style.whiteSpace = WhiteSpace.Normal;

            entryBox.Add(speakerLbl);
            entryBox.Add(textLbl);
            historyContent.Add(entryBox);
        }

        historyPanel.style.display = DisplayStyle.Flex;
    }

    void HideHistory()
    {
        if (historyPanel != null) historyPanel.style.display = DisplayStyle.None;
    }

    // ─── Settings overlay ──────────────────────────────────────────────────────
    void ShowSettings()
    {
        if (settingsPanel == null || settingsContent == null) return;
        settingsContent.Clear();

        AddSetting("Portrait", $"{portraitMode} · {portraitPlacement} · {portraitShape} · {portraitSize}px");
        AddSetting("Portrait display", $"{portraitDisplayType} / {portraitFillMode} / dynamic={dynamicPortraitSize}");
        AddSetting("Name position", $"{namePosition} ({nameDistance}px)");
        AddSetting("Name letters", $"{nameLetterMode}");
        AddSetting("Text letters", $"{textLetterMode}");
        AddSetting("Background", $"{backgroundMode}" + (backgroundMode == BackgroundMode.Image && backgroundImage != null && backgroundImage.sprite != null ? $" ({backgroundImage.scaleMode})" : ""));
        AddSetting("Border", $"{borderWidth}px" + (borderImage != null && borderImage.sprite != null ? " + image" : ""));
        AddSetting("Typewriter", enableTypewriter ? $"on ({typewriterSpeed}s)" : "off");
        AddSetting("Toolbar", $"{toolbarSlideDirection}");

        settingsPanel.style.display = DisplayStyle.Flex;
    }

    void AddSetting(string key, string value)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginBottom = 6;

        var keyLbl = new Label(key + ":");
        keyLbl.style.color = new StyleColor(nameColour);
        keyLbl.style.fontSize = 13;
        keyLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
        keyLbl.style.minWidth = 140;

        var valLbl = new Label(value);
        valLbl.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f, 1f));
        valLbl.style.fontSize = 13;
        valLbl.style.whiteSpace = WhiteSpace.Normal;

        row.Add(keyLbl);
        row.Add(valLbl);
        settingsContent.Add(row);
    }

    void HideSettings()
    {
        if (settingsPanel != null) settingsPanel.style.display = DisplayStyle.None;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RUNTIME LAYOUT — sizes, image layers, shapes, positions
    // ══════════════════════════════════════════════════════════════════════════
    void ApplyVisualRuntimeTextAnchoring()
    {
        if (textScroll != null)
        {
            var cc = textScroll.contentContainer;
            if (cc != null)
            {
                // flexGrow makes the content container at least viewport-sized,
                // so justify-content can center short text and scroll long text.
                cc.style.flexGrow = textVAnchor == TextVAnchor.Top ? 0f : 1f;
                cc.style.justifyContent =
                    textVAnchor == TextVAnchor.Center ? Justify.Center :
                    textVAnchor == TextVAnchor.Bottom ? Justify.FlexEnd : Justify.FlexStart;
            }
        }
        if (dialogueTextLabel != null)
            dialogueTextLabel.style.unityTextAlign =
                textHAnchor == TextHAnchor.Left   ? TextAnchor.MiddleLeft :
                textHAnchor == TextHAnchor.Center ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight;
    }

    void ApplyRuntimeLayout()
    {
        if (box == null) return;

        if (visualLayoutRuntimeActive)
        {
            // The visual-layout runtime UXML already carries the exact panel
            // geometry and styling from the editor; restyle only the parts the
            // generator does not own (text anchoring, hint, toolbar).
            ApplyVisualRuntimeTextAnchoring();
            if (advanceHintLabel != null)
            {
                advanceHintLabel.style.display = showAdvanceHint ? DisplayStyle.Flex : DisplayStyle.None;
                advanceHintLabel.text = advanceHintText;
                advanceHintLabel.style.color = new StyleColor(hintColour);
                advanceHintLabel.style.fontSize = hintFontSize;
            }
            if (toolbarToggleButton != null) toolbarToggleButton.style.display = showToolbar ? DisplayStyle.Flex : DisplayStyle.None;
            if (settingsButton != null) settingsButton.style.display = showSettingsButton ? DisplayStyle.Flex : DisplayStyle.None;
            if (toolbarPanel != null)
                toolbarPanel.style.display = showToolbar && toolbarVisible ? DisplayStyle.Flex : DisplayStyle.None;
            return;
        }

        // ── Panel size & position ─────────────────────────────────────────────
        if (rowContainer != null)
        {
            rowContainer.style.width  = Length.Percent(100);
            rowContainer.style.height = Length.Percent(100);
            rowContainer.style.translate = new Translate(panelOffsetX, panelOffsetY, 0);
            rowContainer.style.justifyContent = ResolveLayoutAnchorJustify();
            rowContainer.style.alignItems = ResolveLayoutAnchorAlign();
        }

        box.style.width  = panelWidthMode == PanelSizeMode.Percent ? new StyleLength(Length.Percent(panelWidthValue)) : new StyleLength(panelWidthValue);
        box.style.height = panelHeightMode == PanelSizeMode.Percent ? new StyleLength(Length.Percent(panelHeightValue)) : new StyleLength(panelHeightValue);

        if (padding == null) padding = new RectOffset(28, 28, 20, 20);
        box.style.paddingLeft   = padding.left;
        box.style.paddingRight  = padding.right;
        box.style.paddingTop    = padding.top;
        box.style.paddingBottom = padding.bottom;

        float maxRadius = Mathf.Max(borderRadiusTL, borderRadiusTR, borderRadiusBL, borderRadiusBR);
        if (backgroundLayer != null)
        {
            backgroundLayer.style.borderTopLeftRadius     = maxRadius;
            backgroundLayer.style.borderTopRightRadius    = maxRadius;
            backgroundLayer.style.borderBottomLeftRadius  = maxRadius;
            backgroundLayer.style.borderBottomRightRadius = maxRadius;
        }
        if (borderLayer != null)
        {
            borderLayer.style.borderTopLeftRadius     = maxRadius;
            borderLayer.style.borderTopRightRadius    = maxRadius;
            borderLayer.style.borderBottomLeftRadius  = maxRadius;
            borderLayer.style.borderBottomRightRadius = maxRadius;
        }

        // ── Background ────────────────────────────────────────────────────────
        bool bgIsImage = backgroundMode == BackgroundMode.Image && HasTiledImage(backgroundImage);
        if (backgroundLayer != null)
            backgroundLayer.style.display = bgIsImage ? DisplayStyle.Flex : DisplayStyle.None;
        box.style.backgroundColor = bgIsImage ? new StyleColor(Color.clear) : new StyleColor(backgroundColour);

        // ── Border image (wins over border colour) ────────────────────────────
        // The box border paints either its colour or nothing; the image border
        // is a separate root-level layer (BorderLayer) positioned on the ring.
        // Border colours are always drawn fully opaque — translucent borders
        // read as "greyed out" on dark panels.
        bool borderIsImage = HasTiledImage(borderImage) && borderWidth > 0f;
        Color opaqueBorder = new Color(borderColour.r, borderColour.g, borderColour.b, 1f);
        var boxBorderC = new StyleColor(borderIsImage ? Color.clear : opaqueBorder);
        box.style.borderTopColor    = boxBorderC;
        box.style.borderBottomColor = boxBorderC;
        box.style.borderLeftColor   = boxBorderC;
        box.style.borderRightColor  = boxBorderC;
        ApplyBorderLayerPosition();

        // ── Text anchoring (VN-style, default: vertically centered, left-aligned) ──
        ApplyVisualRuntimeTextAnchoring();

        // ── Advance hint ──────────────────────────────────────────────────────
        if (advanceHintLabel != null)
        {
            advanceHintLabel.style.display = showAdvanceHint ? DisplayStyle.Flex : DisplayStyle.None;
            advanceHintLabel.text = advanceHintText;
            advanceHintLabel.style.color = new StyleColor(hintColour);
            advanceHintLabel.style.fontSize = hintFontSize;
        }

        // ── Toolbar visibility ────────────────────────────────────────────────
        if (toolbarToggleButton != null) toolbarToggleButton.style.display = showToolbar ? DisplayStyle.Flex : DisplayStyle.None;
        if (settingsButton       != null) settingsButton.style.display       = showSettingsButton ? DisplayStyle.Flex : DisplayStyle.None;
        if (toolbarPanel != null)
            toolbarPanel.style.display = showToolbar && toolbarVisible ? DisplayStyle.Flex : DisplayStyle.None;

        ApplyCharacterPanelDecorations();

        // ── Portrait frame pass ───────────────────────────────────────────────
        if (showPortrait)
        {
            ApplyPortraitFrame(GetSlot(false));
            ApplyPortraitFrame(GetSlot(true));
        }
    }

    void ApplyCharacterPanelDecorations()
    {
        // Visual-layout runtime: the figure/name partitions carry their exact
        // styles from the asset; skip the engine-default decoration pass.
        if (visualLayoutRuntimeActive) return;
        float outerBorder = characterPanelShowBorder ? characterPanelBorderWidth : 0f;
        foreach (VisualElement figure in new[] { charLeftFigure, charRightFigure })
        {
            if (figure == null) continue;
            figure.style.backgroundColor = new StyleColor(
                characterPanelShowBackground ? characterPanelBg : Color.clear);
            figure.style.borderLeftWidth = outerBorder;
            figure.style.borderRightWidth = outerBorder;
            figure.style.borderTopWidth = outerBorder;
            figure.style.borderBottomWidth = outerBorder;
        }

        ApplyNamePanelDecoration(charLeftNamePanel, charLeftNameBorderOverlay);
        ApplyNamePanelDecoration(charRightNamePanel, charRightNameBorderOverlay);
    }

    void ApplyNamePanelDecoration(VisualElement panel, VisualElement overlay)
    {
        if (panel == null) return;

        float diameter = Mathf.Max(portraitSize, nameFontSize +
            (characterNamePanelPadding != null
                ? characterNamePanelPadding.top + characterNamePanelPadding.bottom : 12f) +
            characterNamePanelBorderWidth * 2f);
        float radius = characterNamePanelShape == CharacterImagePanelShape.Circle
            ? diameter * 0.5f
            : characterNamePanelShape == CharacterImagePanelShape.Rounded
                ? characterNamePanelRadius : 0f;
        panel.style.borderTopLeftRadius = radius;
        panel.style.borderTopRightRadius = radius;
        panel.style.borderBottomLeftRadius = radius;
        panel.style.borderBottomRightRadius = radius;
        panel.style.backgroundColor = new StyleColor(
            characterNamePanelShowBackground ? characterNamePanelBg : Color.clear);

        bool horizontal = characterPanelOrder == CharacterPanelOrder.ImageLeft ||
                          characterPanelOrder == CharacterPanelOrder.NameLeft;
        if (characterNamePanelShape != CharacterImagePanelShape.Circle)
        {
            if (!horizontal && characterNamePanelHeightMode == CharacterPanelSizeMode.Default)
            {
                panel.style.width = Length.Percent(100);
                panel.style.height = Length.Percent(24);
                panel.style.minHeight = Mathf.Max(64f, diameter);
                panel.style.flexShrink = 0f;
            }
            else if (!horizontal && characterNamePanelHeightMode == CharacterPanelSizeMode.Custom)
            {
                panel.style.width = Length.Percent(100);
                panel.style.height = characterNamePanelHeight;
                panel.style.minHeight = diameter;
                panel.style.flexShrink = 0f;
            }
            else if (horizontal && characterNamePanelHeightMode == CharacterPanelSizeMode.Default)
            {
                panel.style.height = Length.Percent(100);
                panel.style.width = Length.Percent(24);
                panel.style.minWidth = 72f;
                panel.style.flexShrink = 0f;
            }
            else if (horizontal && characterNamePanelHeightMode == CharacterPanelSizeMode.Custom)
            {
                panel.style.height = Length.Percent(100);
                panel.style.width = characterNamePanelHeight;
                panel.style.minWidth = 72f;
                panel.style.flexShrink = 0f;
            }
        }

        bool imageBorder = characterNamePanelShowBorder &&
            characterNamePanelBorderWidth > 0f && HasTiledImage(characterNamePanelBorderImage);
        float colourWidth = characterNamePanelShowBorder && !imageBorder
            ? characterNamePanelBorderWidth : 0f;
        panel.style.borderLeftWidth = colourWidth;
        panel.style.borderRightWidth = colourWidth;
        panel.style.borderTopWidth = colourWidth;
        panel.style.borderBottomWidth = colourWidth;
        var borderColour = new StyleColor(new Color(characterNamePanelBorderColour.r,
            characterNamePanelBorderColour.g, characterNamePanelBorderColour.b, 1f));
        panel.style.borderLeftColor = borderColour;
        panel.style.borderRightColor = borderColour;
        panel.style.borderTopColor = borderColour;
        panel.style.borderBottomColor = borderColour;

        if (overlay == null) return;
        overlay.style.borderTopLeftRadius = radius;
        overlay.style.borderTopRightRadius = radius;
        overlay.style.borderBottomLeftRadius = radius;
        overlay.style.borderBottomRightRadius = radius;
        overlay.style.display = imageBorder ? DisplayStyle.Flex : DisplayStyle.None;
        if (imageBorder)
            ScheduleBorderRebuild(overlay, characterNamePanelBorderImage, characterNamePanelBorderWidth);
        else
        {
            if (tilers.TryGetValue(overlay, out TilerRuntime old) && old.sched != null) old.sched.Pause();
            tilers.Remove(overlay);
            overlay.Clear();
        }
    }

    // Re-runs the parts that depend on measured sizes (tiling, shapes, on-border placement).
    void RebuildDynamicLayers()
    {
        if (box == null) return;
        Vector2 size = box.layout.size;
        bool boxChanged = size != lastBoxSize;
        lastBoxSize = size;

        if (backgroundLayer != null && backgroundLayer.style.display == DisplayStyle.Flex)
            RebuildImageLayer(backgroundLayer, backgroundImage, boxChanged);
        ApplyBorderLayerPosition();

        ApplyCharacterPanelDecorations();
        if (showPortrait)
        {
            ApplyPortraitFrame(GetSlot(false));
            ApplyPortraitFrame(GetSlot(true));
        }

        ApplyOnBorderPlacement();
    }

    // ── Border image layer (root-level, aligned to the box's border ring) ────
    void ApplyBorderLayerPosition(int attempts = 20)
    {
        if (borderLayer == null || document == null) return;
        var root = document.rootVisualElement;

        bool borderIsImage = HasTiledImage(borderImage) && borderWidth > 0f;
        if (borderLayer.style.display != (borderIsImage ? DisplayStyle.Flex : DisplayStyle.None))
            borderLayer.style.display = borderIsImage ? DisplayStyle.Flex : DisplayStyle.None;
        if (!borderIsImage || root == null) return;

        if (box == null || box.layout.width <= 0f || box.layout.height <= 0f)
        {
            // Box not laid out yet — retry a few times.
            if (attempts > 0)
                borderLayer.schedule.Execute(() => ApplyBorderLayerPosition(attempts - 1)).ExecuteLater(50);
            return;
        }

        // Cover exactly the box's border ring (world-space → root-local).
        borderLayer.style.left   = box.worldBound.xMin - root.worldBound.xMin;
        borderLayer.style.top    = box.worldBound.yMin - root.worldBound.yMin;
        borderLayer.style.width  = box.layout.width;
        borderLayer.style.height = box.layout.height;

        ScheduleBorderRebuild(borderLayer, borderImage, borderWidth);
    }

    void ApplyOnBorderPlacement()
    {
        if (portraitPlacement != PortraitPlacement.OnBorder || box == null || document == null) return;
        var root = document.rootVisualElement;
        if (root == null || box.layout.width <= 0f || box.layout.height <= 0f) return;

        // On-border = the TOP corners of the main panel:
        // left portrait → top-left corner, right portrait → top-right corner.
        float leftX  = box.worldBound.xMin - root.worldBound.xMin;
        float rightX = box.worldBound.xMax - root.worldBound.xMin;
        float topY   = box.worldBound.yMin - root.worldBound.yMin;

        float half = portraitSize * 0.5f;

        if (borderLeftWrapper != null)
        {
            borderLeftWrapper.style.left = leftX - half + portraitOffsetX;
            borderLeftWrapper.style.top  = topY - half + portraitOffsetY;
        }
        if (borderRightWrapper != null)
        {
            borderRightWrapper.style.left = rightX - half + portraitOffsetX;
            borderRightWrapper.style.top  = topY - half + portraitOffsetY;
        }
    }

    // ─── Generic image layer (tiling / looping / animating) ───────────────────
    static bool SameTilerKey(TilerRuntime tr, TiledImageSettings settings, Sprite sprite)
    {
        if (tr == null || settings == null || tr.settings == null) return false;
        return tr.settings.sprite == sprite
            && tr.settings.tileScale == settings.tileScale
            && tr.settings.scaleMode == settings.scaleMode
            && tr.settings.animate == settings.animate
            && tr.settings.animDirection == settings.animDirection
            && tr.settings.animSpeed == settings.animSpeed
            && tr.settings.loop == settings.loop
            && tr.settings.tintEnabled == settings.tintEnabled
            && tr.settings.tintColour.Equals(settings.tintColour);
    }

    void RebuildImageLayer(VisualElement clip, TiledImageSettings settings, bool sizeChanged)
    {
        if (clip == null || settings == null) return;
        Sprite sprite = ResolveTiledSprite(settings);
        if (sprite == null) return;

        Vector2 size = clip.layout.size;
        if (size.x <= 0f || size.y <= 0f) return;

        if (layerSizes.TryGetValue(clip, out Vector2 cached) && cached == size && !sizeChanged
            && tilers.TryGetValue(clip, out TilerRuntime cachedTiler) && SameTilerKey(cachedTiler, settings, sprite))
            return;
        layerSizes[clip] = size;

        // Stop the previous animation task before rebuilding, otherwise the
        // old scheduled callback would keep ticking the new state (double speed).
        if (tilers.TryGetValue(clip, out TilerRuntime old) && old.sched != null)
            old.sched.Pause();
        tilers.Remove(clip);

        clip.Clear();

        var tiler = new TilerRuntime { clip = clip, settings = settings, clipSize = size };
        Vector2 texel = sprite.textureRect.size * settings.tileScale;

        if (settings.scaleMode == ImageScaleMode.Stretch)
        {
            tiler.stretch = true;
            tiler.tileSize = size;
            int count = settings.animate ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                var t = MakeTile(settings, sprite, size);
                t.style.left = i * size.x;
                t.style.top  = 0;
                clip.Add(t);
                tiler.tiles.Add(t);
            }
        }
        else
        {
            tiler.tileSize = texel;
            tiler.mover = new VisualElement();
            tiler.mover.style.position = Position.Absolute;
            tiler.mover.style.overflow = Overflow.Visible;
            clip.Add(tiler.mover);
            for (float y = -texel.y; y < size.y + texel.y; y += texel.y)
                for (float x = -texel.x; x < size.x + texel.x; x += texel.x)
                {
                    var t = MakeTile(settings, sprite, texel);
                    t.style.left = x;
                    t.style.top  = y;
                    tiler.mover.Add(t);
                }
            tiler.Apply();
        }

        tilers[clip] = tiler;
        tiler.sched = clip.schedule.Execute(() =>
        {
            if (tilers.TryGetValue(clip, out TilerRuntime tr)) tr.Tick(0.016f);
        }).Every(16);
    }

    VisualElement MakeTile(TiledImageSettings settings, Sprite sprite, Vector2 size)
    {
        var t = new VisualElement();
        t.style.position = Position.Absolute;
        t.style.backgroundImage = new StyleBackground(sprite);
        t.style.backgroundSize = new BackgroundSize(size.x, size.y);
        t.style.width  = size.x;
        t.style.height = size.y;
        t.pickingMode = PickingMode.Ignore;
        // Optional colour tint multiplies the image's own pixels.
        t.style.unityBackgroundImageTintColor = new StyleColor(
            settings != null && settings.tintEnabled ? settings.tintColour : Color.white);
        return t;
    }

    // ─── Border ring image layer (strips + corners, clipped to the radius) ────
    void RebuildBorderImageLayer(VisualElement clip, TiledImageSettings settings, float thickness)
    {
        if (clip == null || settings == null || thickness <= 0f) return;
        Sprite sprite = ResolveTiledSprite(settings);
        if (sprite == null) { tilers.Remove(clip); clip.Clear(); return; }

        Vector2 size = clip.layout.size;
        if (size.x <= 0f || size.y <= 0f) return;

        if (layerSizes.TryGetValue(clip, out Vector2 cached) && cached == size
            && tilers.TryGetValue(clip, out TilerRuntime cachedTiler) && SameTilerKey(cachedTiler, settings, sprite))
            return;
        layerSizes[clip] = size;

        if (tilers.TryGetValue(clip, out TilerRuntime oldT) && oldT.sched != null)
            oldT.sched.Pause();
        tilers.Remove(clip);
        clip.Clear();

        float bw = Mathf.Min(thickness, Mathf.Min(size.x, size.y) * 0.5f);
        Vector2 texel = sprite.textureRect.size * settings.tileScale;

        // corners first, then edge strips over them
        AddBorderTile(clip, settings, sprite, new Rect(0, 0, bw, bw));
        AddBorderTile(clip, settings, sprite, new Rect(size.x - bw, 0, bw, bw));
        AddBorderTile(clip, settings, sprite, new Rect(0, size.y - bw, bw, bw));
        AddBorderTile(clip, settings, sprite, new Rect(size.x - bw, size.y - bw, bw, bw));

        AddBorderStrip(clip, settings, sprite, texel, new Rect(bw, 0, size.x - bw * 2f, bw));          // top
        AddBorderStrip(clip, settings, sprite, texel, new Rect(bw, size.y - bw, size.x - bw * 2f, bw)); // bottom
        AddBorderStrip(clip, settings, sprite, texel, new Rect(0, bw, bw, size.y - bw * 2f));          // left
        AddBorderStrip(clip, settings, sprite, texel, new Rect(size.x - bw, bw, bw, size.y - bw * 2f)); // right
    }

    void AddBorderTile(VisualElement clip, TiledImageSettings settings, Sprite sprite, Rect area)
    {
        var strip = new VisualElement();
        strip.style.position = Position.Absolute;
        strip.style.left = area.x;
        strip.style.top  = area.y;
        strip.style.width  = area.width;
        strip.style.height = area.height;
        strip.style.overflow = Overflow.Hidden;

        var t = MakeTile(settings, sprite, area.size);
        t.style.left = 0; t.style.top = 0;
        strip.Add(t);
        clip.Add(strip);

        if (settings.animate)
        {
            var tr = new TilerRuntime { clip = strip, settings = settings, clipSize = area.size, tileSize = area.size, stretch = true };
            tr.tiles.Add(t);
            tilers[strip] = tr;
            tr.sched = strip.schedule.Execute(() =>
            {
                if (tilers.TryGetValue(strip, out TilerRuntime r)) r.Tick(0.016f);
            }).Every(16);
        }
    }

    void AddBorderStrip(VisualElement clip, TiledImageSettings settings, Sprite sprite, Vector2 texel, Rect area)
    {
        var strip = new VisualElement();
        strip.style.position = Position.Absolute;
        strip.style.left = area.x;
        strip.style.top  = area.y;
        strip.style.width  = area.width;
        strip.style.height = area.height;
        strip.style.overflow = Overflow.Hidden;
        clip.Add(strip);

        var tr = new TilerRuntime { clip = strip, settings = settings, clipSize = area.size };
        if (settings.scaleMode == ImageScaleMode.Stretch)
        {
            tr.stretch = true;
            tr.tileSize = area.size;
            int count = settings.animate ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                var t = MakeTile(settings, sprite, area.size);
                t.style.left = i * area.width;
                t.style.top  = 0;
                strip.Add(t);
                tr.tiles.Add(t);
            }
        }
        else
        {
            tr.tileSize = texel;
            tr.mover = new VisualElement();
            tr.mover.style.position = Position.Absolute;
            strip.Add(tr.mover);
            bool horizontal = area.width > area.height;
            if (horizontal)
            {
                for (float x = -texel.x; x < area.width + texel.x; x += texel.x)
                {
                    var t = MakeTile(settings, sprite, texel);
                    t.style.left = x;
                    t.style.top  = (area.height - texel.y) * 0.5f;
                    tr.mover.Add(t);
                }
            }
            else
            {
                for (float y = -texel.y; y < area.height + texel.y; y += texel.y)
                {
                    var t = MakeTile(settings, sprite, texel);
                    t.style.left = (area.width - texel.x) * 0.5f;
                    t.style.top  = y;
                    tr.mover.Add(t);
                }
            }
            tr.Apply();
        }

        tilers[strip] = tr;
        tr.sched = strip.schedule.Execute(() =>
        {
            if (tilers.TryGetValue(strip, out TilerRuntime r)) r.Tick(0.016f);
        }).Every(16);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // UXML GENERATION (editor-time only)
    //
    // This lives HERE, inside the runtime assembly, so this file never has to
    // reference a type from Assets/Editor. Editor scripts (DialogueLayoutBuilder,
    // DialogueEngineEditor, DialoguePreviewWindow) call Dialogue_Engine.GenerateUxml()
    // instead — editor assemblies may reference runtime assemblies, never the
    // other way around.
    // ══════════════════════════════════════════════════════════════════════════
    #if UNITY_EDITOR
    static string Rgba(Color c)
    {
        return $"rgba({Mathf.RoundToInt(c.r * 255)},{Mathf.RoundToInt(c.g * 255)},{Mathf.RoundToInt(c.b * 255)},{Mathf.RoundToInt(c.a * 255)})";
    }

    // Border colours ignore the alpha channel (always 255): translucent
    // borders render greyed-out over dark panels.
    static string RgbaOpaque(Color c)
    {
        return $"rgba({Mathf.RoundToInt(c.r * 255)},{Mathf.RoundToInt(c.g * 255)},{Mathf.RoundToInt(c.b * 255)},255)";
    }

    static string Dim(PanelSizeMode mode, float value)
    {
        return mode == PanelSizeMode.Percent ? $"{value:0.##}%" : $"{value:0.#}px";
    }

    static string FontDef(Font f)
    {
        if (f == null) return "";
        string p = AssetDatabase.GetAssetPath(f);
        if (string.IsNullOrEmpty(p)) return "";
        return $"-unity-font-definition: url(&quot;project://database/{p}&quot;); ";
    }

    // ── Portrait host snippet (host → portrait + border-image overlay) ─────
    static string SlotXml(Dialogue_Engine e, string hostName, string frameName, string portraitName, string overlayName, bool fillContainer = false)
    {
        float portraitW = e.portraitShape == PortraitShape.Rectangle ? e.portraitSize * 1.3f : e.portraitSize;
        float portraitH = e.portraitSize;
        float portraitRadius;
        switch (e.portraitShape)
        {
            case PortraitShape.Circle:  portraitRadius = Mathf.Min(portraitW, portraitH) * 0.5f; break;
            case PortraitShape.Rounded: portraitRadius = e.portraitBorderRadius; break;
            default:                    portraitRadius = 0f; break;
        }
        float  pBorderW = e.showPortraitBorder ? e.portraitBorderWidth : 0f;
        string pBorderC = RgbaOpaque(e.portraitBorderColour);
        bool   pBorderIsImage = e.portraitBorderImage != null && e.portraitBorderImage.sprite != null;

        // The Frame owns the border (colour OR border image — never both), so
        // greying out the inactive slot (image + name opacity) never filters
        // the border down.
        string hostSize = fillContainer ? "width: 100%; height: 100%; flex-grow: 1;" : "";
        string frameSize = fillContainer
            ? $"width: 100%; height: 100%; min-height: {portraitH:0.#}px; flex-grow: 1; display: none;"
            : $"width: {portraitW:0.#}px; height: {portraitH:0.#}px; flex-shrink: 0;";
        return $@"
            <ui:VisualElement name=""{hostName}"" style=""flex-direction: row; align-items: center; justify-content: center; {hostSize}"">
                <ui:VisualElement name=""{frameName}"" style=""{frameSize} overflow: hidden; border-width: {pBorderW:0.#}px; border-color: {(pBorderIsImage ? "rgba(0,0,0,0)" : pBorderC)}; border-top-left-radius: {portraitRadius:0.#}px; border-top-right-radius: {portraitRadius:0.#}px; border-bottom-left-radius: {portraitRadius:0.#}px; border-bottom-right-radius: {portraitRadius:0.#}px;"">
                    <ui:VisualElement name=""{portraitName}"" style=""position: absolute; left: 0; top: 0; right: 0; bottom: 0; background-size: contain; background-repeat: no-repeat; background-position: center; border-top-left-radius: {portraitRadius:0.#}px; border-top-right-radius: {portraitRadius:0.#}px; border-bottom-left-radius: {portraitRadius:0.#}px; border-bottom-right-radius: {portraitRadius:0.#}px;"" />
                    <ui:VisualElement name=""{overlayName}"" style=""position: absolute; left: 0; top: 0; right: 0; bottom: 0; border-top-left-radius: {portraitRadius:0.#}px; border-top-right-radius: {portraitRadius:0.#}px; border-bottom-left-radius: {portraitRadius:0.#}px; border-bottom-right-radius: {portraitRadius:0.#}px; overflow: hidden; picking-mode: Ignore; display: none;"" />
                </ui:VisualElement>
            </ui:VisualElement>";
    }

    // ── Wrapper snippet: name + host, ordered by namePosition ─────────────
    static string WrapXml(Dialogue_Engine e, string wrapperName, string hostXml, string nameName, string extraStyle)
    {
        bool nameFirst = e.namePosition == NamePosition.Left || e.namePosition == NamePosition.Above;
        bool horizontal = e.namePosition == NamePosition.Left || e.namePosition == NamePosition.Right;
        string margin = "";
        switch (e.namePosition)
        {
            case NamePosition.Left:  margin = "margin-right: "  + e.nameDistance.ToString("0.#") + "px;"; break;
            case NamePosition.Right: margin = "margin-left: "   + e.nameDistance.ToString("0.#") + "px;"; break;
            case NamePosition.Above: margin = "margin-bottom: " + e.nameDistance.ToString("0.#") + "px;"; break;
            case NamePosition.Below: margin = "margin-top: "    + e.nameDistance.ToString("0.#") + "px;"; break;
        }
        string name = $@"<ui:VisualElement name=""{nameName}"" style=""{margin}"" />";
        return $@"
        <ui:VisualElement name=""{wrapperName}"" style=""flex-direction: {(horizontal ? "row" : "column")}; align-items: center; justify-content: center; display: none; {extraStyle}"">
            {(nameFirst ? name + "\n        " + hostXml : hostXml + "\n        " + name)}
        </ui:VisualElement>";
    }

    // ── Character figure panel: [image panel] + [name panel], fully custom ────
    static string CharacterPanelXml(Dialogue_Engine e, bool right)
    {
        string side = right ? "Right" : "Left";
        bool horizontal = e.characterPanelOrder == CharacterPanelOrder.ImageLeft ||
                          e.characterPanelOrder == CharacterPanelOrder.NameLeft;
        bool imageFirst = e.characterPanelOrder == CharacterPanelOrder.ImageTop ||
                          e.characterPanelOrder == CharacterPanelOrder.ImageLeft;

        string gap = horizontal
            ? $"margin-left: {e.characterPanelSpacing:0.#}px;"
            : $"margin-top: {e.characterPanelSpacing:0.#}px;";
        string imageGap = imageFirst ? "" : gap;
        string nameGap  = imageFirst ? gap : "";

        // The wrapper is a true sibling root of the main panel. In Default mode
        // every visible character root takes an equal share of the width left
        // after DialogueBox. A practical minimum prevents a 90%-wide dialogue
        // box from crushing each character root into an unusable sliver.
        float defaultMinWidth = Mathf.Max(170f, e.portraitSize +
            (e.characterPanelPadding != null ? e.characterPanelPadding.left + e.characterPanelPadding.right : 24f));
        string wrapperWidth = e.characterPanelWidthMode == CharacterPanelSizeMode.Default
            ? $"flex-grow: 1; flex-basis: 0; min-width: {defaultMinWidth:0.#}px;"
            : e.characterPanelWidthMode == CharacterPanelSizeMode.Custom
                ? $"width: {e.characterPanelWidth:0.#}px; flex-shrink: 0;"
                : "flex-shrink: 0;";
        string figureWidth = e.characterPanelWidthMode == CharacterPanelSizeMode.Content ? "" : "width: 100%;";
        string figureHeight = e.characterPanelHeightMode == CharacterPanelSizeMode.Default
            ? "height: 62%; min-height: 240px;"
            : e.characterPanelHeightMode == CharacterPanelSizeMode.Custom
                ? $"height: {e.characterPanelHeight:0.#}px;"
                : "";

        string pad = e.characterPanelPadding != null
            ? $"padding-top: {e.characterPanelPadding.top}px; padding-bottom: {e.characterPanelPadding.bottom}px; padding-left: {e.characterPanelPadding.left}px; padding-right: {e.characterPanelPadding.right}px;"
            : "padding: 12px;";
        string imgPad = e.characterImagePanelPadding != null
            ? $"padding-top: {e.characterImagePanelPadding.top}px; padding-bottom: {e.characterImagePanelPadding.bottom}px; padding-left: {e.characterImagePanelPadding.left}px; padding-right: {e.characterImagePanelPadding.right}px;"
            : "padding: 8px;";
        string namePad = e.characterNamePanelPadding != null
            ? $"padding-top: {e.characterNamePanelPadding.top}px; padding-bottom: {e.characterNamePanelPadding.bottom}px; padding-left: {e.characterNamePanelPadding.left}px; padding-right: {e.characterNamePanelPadding.right}px;"
            : "padding: 6px 8px;";
        // The image partition is visually absent until runtime loads a real image.
        float imageBorderWidth = 0f;
        float imageRadius = e.characterImagePanelShape == CharacterImagePanelShape.Circle
            ? e.portraitSize * 0.5f
            : e.characterImagePanelShape == CharacterImagePanelShape.Rounded
                ? e.characterImagePanelRadius : 0f;

        string imageSizing;
        if (e.characterImagePanelShape == CharacterImagePanelShape.Circle)
            imageSizing = $"width: {e.portraitSize:0.#}px; height: {e.portraitSize:0.#}px; flex-shrink: 0; align-self: center;";
        else
            imageSizing = horizontal
                ? "height: 100%; flex-grow: 1; min-width: 0;"
                : $"width: 100%; flex-grow: 1; min-height: {e.portraitSize:0.#}px;";
        float nameMinHeight = e.nameFontSize +
            (e.characterNamePanelPadding != null ? e.characterNamePanelPadding.top + e.characterNamePanelPadding.bottom : 12f) +
            e.characterNamePanelBorderWidth * 2f;
        string nameSizing;
        if (horizontal)
        {
            nameSizing = e.characterNamePanelHeightMode == CharacterPanelSizeMode.Custom
                ? $"height: 100%; width: {e.characterNamePanelHeight:0.#}px; flex-shrink: 0;"
                : e.characterNamePanelHeightMode == CharacterPanelSizeMode.Default
                    ? "height: 100%; width: 24%; min-width: 72px; flex-shrink: 0;"
                    : "height: 100%; min-width: 72px; flex-shrink: 0;";
        }
        else
        {
            nameSizing = e.characterNamePanelHeightMode == CharacterPanelSizeMode.Custom
                ? $"width: 100%; height: {e.characterNamePanelHeight:0.#}px; min-height: {nameMinHeight:0.#}px; flex-shrink: 0;"
                : e.characterNamePanelHeightMode == CharacterPanelSizeMode.Default
                    ? $"width: 100%; height: 24%; min-height: {Mathf.Max(64f, nameMinHeight):0.#}px; flex-shrink: 0;"
                    : $"width: 100%; min-height: {nameMinHeight:0.#}px; flex-shrink: 0;";
        }
        float nameRadius = e.characterNamePanelShape == CharacterImagePanelShape.Circle
            ? Mathf.Max(nameMinHeight, e.portraitSize) * 0.5f
            : e.characterNamePanelShape == CharacterImagePanelShape.Rounded
                ? e.characterNamePanelRadius : 0f;
        if (e.characterNamePanelShape == CharacterImagePanelShape.Circle)
        {
            float diameter = Mathf.Max(nameMinHeight, e.portraitSize);
            nameSizing = $"width: {diameter:0.#}px; height: {diameter:0.#}px; flex-shrink: 0; align-self: center;";
        }
        bool nameBorderIsImage = e.characterNamePanelShowBorder && e.characterNamePanelBorderImage != null &&
            (e.characterNamePanelBorderImage.sprite != null || !string.IsNullOrEmpty(e.characterNamePanelBorderImage.path));
        float nameBorderWidth = e.characterNamePanelShowBorder && !nameBorderIsImage
            ? e.characterNamePanelBorderWidth : 0f;
        float outerBorderWidth = e.characterPanelShowBorder ? e.characterPanelBorderWidth : 0f;
        string outerBackground = e.characterPanelShowBackground ? Rgba(e.characterPanelBg) : "rgba(0,0,0,0)";

        string imagePanel = $@"
            <ui:VisualElement name=""CharacterImagePanel{side}"" style=""{(e.characterPanelShowImagePanel ? "" : "display: none; ")}background-color: rgba(0,0,0,0); border-color: {RgbaOpaque(e.characterImagePanelBorderColour)}; border-width: {imageBorderWidth:0.#}px; border-top-left-radius: {imageRadius:0.#}px; border-top-right-radius: {imageRadius:0.#}px; border-bottom-left-radius: {imageRadius:0.#}px; border-bottom-right-radius: {imageRadius:0.#}px; overflow: hidden; {imgPad} {imageSizing} align-items: center; justify-content: center; {imageGap}"">
                {SlotXml(e, $"PortraitHostChar{side}", $"PortraitFrameChar{side}", $"PortraitChar{side}", $"PortraitBorderOverlayChar{side}", true)}
            </ui:VisualElement>";

        string namePanel = $@"
            <ui:VisualElement name=""CharacterNamePanel{side}"" style=""{(e.characterPanelShowNamePanel ? "" : "display: none; ")}background-color: {(e.characterNamePanelShowBackground ? Rgba(e.characterNamePanelBg) : "rgba(0,0,0,0)")}; border-color: {(nameBorderIsImage ? "rgba(0,0,0,0)" : RgbaOpaque(e.characterNamePanelBorderColour))}; border-width: {nameBorderWidth:0.#}px; border-top-left-radius: {nameRadius:0.#}px; border-top-right-radius: {nameRadius:0.#}px; border-bottom-left-radius: {nameRadius:0.#}px; border-bottom-right-radius: {nameRadius:0.#}px; overflow: hidden; {namePad} {nameSizing} align-items: center; justify-content: center; {nameGap}"">
                <ui:VisualElement name=""NameChar{side}"" style=""position: relative;"" />
                <ui:VisualElement name=""CharacterNameBorderOverlay{side}"" style=""position: absolute; left: 0; top: 0; right: 0; bottom: 0; border-top-left-radius: {nameRadius:0.#}px; border-top-right-radius: {nameRadius:0.#}px; border-bottom-left-radius: {nameRadius:0.#}px; border-bottom-right-radius: {nameRadius:0.#}px; overflow: hidden; picking-mode: Ignore; {(nameBorderIsImage ? "" : "display: none;")}"" />
            </ui:VisualElement>";

        return $@"
        <ui:VisualElement name=""CharacterPanel{side}Wrapper"" style=""{wrapperWidth} height: 100%; align-self: stretch; align-items: stretch; justify-content: flex-end; display: none;"">
            <ui:VisualElement name=""CharacterFigurePanel{side}"" style=""{figureWidth} {figureHeight} flex-direction: {(horizontal ? "row" : "column")}; align-items: stretch; justify-content: flex-start; background-color: {outerBackground}; border-color: {RgbaOpaque(e.characterPanelBorderColour)}; border-width: {outerBorderWidth:0.#}px; border-top-left-radius: {e.characterPanelRadius:0.#}px; border-top-right-radius: {e.characterPanelRadius:0.#}px; border-bottom-left-radius: {e.characterPanelRadius:0.#}px; border-bottom-right-radius: {e.characterPanelRadius:0.#}px; overflow: hidden; {pad}"">
                {(imageFirst ? imagePanel + "\n" + namePanel : namePanel + "\n" + imagePanel)}
            </ui:VisualElement>
        </ui:VisualElement>";
    }

    // ── Professional USS styling block (hover/selected states, transitions) ───
    Justify ResolveLayoutAnchorJustify()
    {
        switch (layoutAssetAnchorPreset)
        {
            case DialogueAnchorPreset.TopLeft:
            case DialogueAnchorPreset.Left:
            case DialogueAnchorPreset.BottomLeft:
                return Justify.FlexStart;
            case DialogueAnchorPreset.TopRight:
            case DialogueAnchorPreset.Right:
            case DialogueAnchorPreset.BottomRight:
                return Justify.FlexEnd;
            case DialogueAnchorPreset.Custom:
                return ResolveCustomAnchorJustify(layoutAssetCustomAnchor);
            default:
                return Justify.Center;
        }
    }

    Align ResolveLayoutAnchorAlign()
    {
        switch (layoutAssetAnchorPreset)
        {
            case DialogueAnchorPreset.TopLeft:
            case DialogueAnchorPreset.Top:
            case DialogueAnchorPreset.TopRight:
                return Align.FlexStart;
            case DialogueAnchorPreset.BottomLeft:
            case DialogueAnchorPreset.Bottom:
            case DialogueAnchorPreset.BottomRight:
                return Align.FlexEnd;
            case DialogueAnchorPreset.Custom:
                return ResolveCustomAnchorAlign(layoutAssetCustomAnchor);
            default:
                return Align.Center;
        }
    }

    static Justify ResolveCustomAnchorJustify(DialogueCustomAnchorDefinition custom)
    {
        if (custom == null) return Justify.Center;
        switch (custom.HorizontalReference)
        {
            case DialogueAnchorReferenceEdge.Left: return Justify.FlexStart;
            case DialogueAnchorReferenceEdge.Right: return Justify.FlexEnd;
            default: return Justify.Center;
        }
    }

    static Align ResolveCustomAnchorAlign(DialogueCustomAnchorDefinition custom)
    {
        if (custom == null) return Align.Center;
        switch (custom.VerticalReference)
        {
            case DialogueAnchorReferenceEdge.Top: return Align.FlexStart;
            case DialogueAnchorReferenceEdge.Bottom: return Align.FlexEnd;
            default: return Align.Center;
        }
    }

    static string ToCssJustify(Justify justify)
    {
        switch (justify)
        {
            case Justify.FlexStart: return "flex-start";
            case Justify.FlexEnd: return "flex-end";
            default: return "center";
        }
    }

    static string ToCssAlign(Align align)
    {
        switch (align)
        {
            case Align.FlexStart: return "flex-start";
            case Align.FlexEnd: return "flex-end";
            default: return "center";
        }
    }

    static string BuildUss(Dialogue_Engine e)
    {
        Color hover = Color.Lerp(e.backgroundColour, new Color(0.30f, 0.55f, 1f), 0.30f);
        Color selected = Color.Lerp(e.backgroundColour, new Color(0.30f, 0.55f, 1f), 0.45f);
        return $@"
.dlg-choice-button {{
    background-color: {Rgba(Color.Lerp(e.backgroundColour, Color.white, 0.07f))};
    border-width: 1px;
    border-color: {RgbaOpaque(Color.Lerp(e.borderColour, Color.white, 0.35f))};
    border-radius: 8px;
    padding-top: 8px; padding-bottom: 8px;
    padding-left: 14px; padding-right: 14px;
    -unity-text-align: middle-left;
    transition-property: background-color, border-color;
    transition-duration: 0.12s;
}}
.dlg-choice-button:hover {{
    background-color: {Rgba(hover)};
    border-color: {RgbaOpaque(new Color(0.55f, 0.75f, 1f))};
}}
.dlg-choice-selected {{
    background-color: {Rgba(selected)};
    border-color: {RgbaOpaque(new Color(0.65f, 0.82f, 1f))};
}}
.dlg-toolbar-button, .dlg-close-button {{
    background-color: {Rgba(Color.Lerp(e.backgroundColour, Color.white, 0.05f))};
    border-width: 1px;
    border-color: {RgbaOpaque(Color.Lerp(e.borderColour, Color.white, 0.25f))};
    border-radius: 6px;
    padding-top: 4px; padding-bottom: 4px;
    padding-left: 10px; padding-right: 10px;
    margin-left: 4px; margin-right: 4px;
    transition-property: background-color, border-color;
    transition-duration: 0.12s;
}}
.dlg-toolbar-button:hover, .dlg-close-button:hover {{
    background-color: {Rgba(hover)};
    border-color: {RgbaOpaque(new Color(0.55f, 0.75f, 1f))};
}}
.dlg-history-entry {{
    padding-bottom: 6px;
    margin-bottom: 6px;
    border-bottom-width: 1px;
    border-bottom-color: {Rgba(new Color(1f, 1f, 1f, 0.08f))};
}}";
    }

    public static string GenerateUxml(Dialogue_Engine e)
    {
        e.EnsureCharacterPanelDefaults();
        if (e.padding == null) e.padding = new RectOffset(28, 28, 20, 20);

        string panelW = Dim(e.panelWidthMode, e.panelWidthValue);
        string panelH = Dim(e.panelHeightMode, e.panelHeightValue);

        float boxMaxRadius = Mathf.Max(e.borderRadiusTL, e.borderRadiusTR, e.borderRadiusBL, e.borderRadiusBR);

        bool bgIsImage = e.backgroundMode == BackgroundMode.Image &&
                         e.backgroundImage != null && e.backgroundImage.sprite != null;
        bool borderIsImage = e.borderImage != null && e.borderImage.sprite != null && e.borderWidth > 0f;

        string textFontDef = FontDef(e.textFont);

        // Text anchoring (VN-style) → USS values
        string textColumnJustify = e.textVAnchor == TextVAnchor.Top    ? "flex-start" :
                                   e.textVAnchor == TextVAnchor.Bottom ? "flex-end" : "center";
        string textAlign = e.textHAnchor == TextHAnchor.Center ? "middle-center" :
                           e.textHAnchor == TextHAnchor.Right  ? "middle-right" : "middle-left";

        string tbPosition, tbFlex;
        float toolbarRightInset = 10f;
        // In dual Character Panel mode the right root owns the screen edge.
        // Keep Menu/toolbar on the main-panel side instead of covering Name.
        if (e.portraitPlacement == PortraitPlacement.CharacterPanel && e.portraitMode == PortraitMode.Dual)
        {
            float rootWidth = e.characterPanelWidthMode == CharacterPanelSizeMode.Custom
                ? e.characterPanelWidth
                : Mathf.Max(170f, e.portraitSize +
                    (e.characterPanelPadding != null ? e.characterPanelPadding.left + e.characterPanelPadding.right : 24f));
            toolbarRightInset += rootWidth;
        }
        switch (e.toolbarSlideDirection)
        {
            case ToolbarSlideDirection.Top:   tbPosition = $"top: 10px; right: {toolbarRightInset:0.#}px;";   tbFlex = "flex-direction: row;";    break;
            case ToolbarSlideDirection.Left:  tbPosition = "left: 10px; top: 10px;";                         tbFlex = "flex-direction: column;"; break;
            case ToolbarSlideDirection.Right: tbPosition = $"right: {toolbarRightInset:0.#}px; top: 10px;";  tbFlex = "flex-direction: column;"; break;
            default:                          tbPosition = $"bottom: 10px; right: {toolbarRightInset:0.#}px;"; tbFlex = "flex-direction: row;"; break;
        }

        string boxHAlign = e.portraitPlacement == PortraitPlacement.OnBorder
            ? "margin-left: auto; margin-right: auto;"
            : "";
        // Character roots have a minimum usable width; only in this placement
        // may the main box yield enough room to honor that minimum.
        string boxFlexShrink = e.portraitPlacement == PortraitPlacement.CharacterPanel ? "1" : "0";

        string insideLeft = WrapXml(e, "InsideLeftWrapper",
            SlotXml(e, "PortraitHostInsideLeft", "PortraitFrameInsideLeft", "PortraitInsideLeft", "PortraitBorderOverlayInsideLeft"),
            "NameInsideLeft", "margin-left: 10px; margin-right: 10px;");
        string insideRight = WrapXml(e, "InsideRightWrapper",
            SlotXml(e, "PortraitHostInsideRight", "PortraitFrameInsideRight", "PortraitInsideRight", "PortraitBorderOverlayInsideRight"),
            "NameInsideRight", "margin-left: 10px; margin-right: 10px;");
        // Outside = centered in the leftover space beside the panel (align-self
        // overrides the row's flex-end so it grows symmetrically).
        string outsideLeft = WrapXml(e, "OutsideLeftWrapper",
            SlotXml(e, "PortraitHostOutsideLeft", "PortraitFrameOutsideLeft", "PortraitOutsideLeft", "PortraitBorderOverlayOutsideLeft"),
            "NameOutsideLeft", "align-self: center; margin-left: 10px; margin-right: 10px;");
        string outsideRight = WrapXml(e, "OutsideRightWrapper",
            SlotXml(e, "PortraitHostOutsideRight", "PortraitFrameOutsideRight", "PortraitOutsideRight", "PortraitBorderOverlayOutsideRight"),
            "NameOutsideRight", "align-self: center; margin-left: 10px; margin-right: 10px;");
        string borderLeft = WrapXml(e, "BorderLeftWrapper",
            SlotXml(e, "PortraitHostBorderLeft", "PortraitFrameBorderLeft", "PortraitBorderLeft", "PortraitBorderOverlayBorderLeft"),
            "NameBorderLeft", "position: absolute;");
        string borderRight = WrapXml(e, "BorderRightWrapper",
            SlotXml(e, "PortraitHostBorderRight", "PortraitFrameBorderRight", "PortraitBorderRight", "PortraitBorderOverlayBorderRight"),
            "NameBorderRight", "position: absolute;");
        string charLeft  = CharacterPanelXml(e, false);
        string charRight = CharacterPanelXml(e, true);
        string uss = BuildUss(e);

        string rowJustify = ToCssJustify(e.ResolveLayoutAnchorJustify());
        string rowAlign = ToCssAlign(e.ResolveLayoutAnchorAlign());

        string uxml = $@"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:uie=""UnityEditor.UIElements"" xsi=""http://www.w3.org/2001/XMLSchema-instance"" engine=""UnityEngine.UIElements"" editor=""UnityEditor.UIElements"" noNamespaceSchemaLocation=""../../UIElementsSchema/UIElements.xsd"" editor-extension-mode=""False"">
    <ui:VisualElement name=""Root"" style=""width: 100%; height: 100%; justify-content: flex-start; align-items: stretch;"">
        <ui:VisualElement name=""RowContainer"" style=""flex-direction: row; justify-content: {rowJustify}; align-items: {rowAlign}; width: 100%; height: 100%; translate: {e.panelOffsetX:0.#}px {e.panelOffsetY:0.#}px;"">

            <!-- Outside Left Portrait -->
{outsideLeft}

            <!-- Character Figure Panel (left) -->
{charLeft}

            <!-- Main Dialogue Box -->
            <ui:VisualElement name=""DialogueBox"" style=""flex-direction: row; flex-shrink: {boxFlexShrink}; width: {panelW}; height: {panelH}; overflow: hidden; background-color: {(bgIsImage ? "rgba(0,0,0,0)" : Rgba(e.backgroundColour))}; border-color: {(borderIsImage ? "rgba(0,0,0,0)" : RgbaOpaque(e.borderColour))}; border-width: {e.borderWidth:0.#}px; border-top-left-radius: {e.borderRadiusTL:0.#}px; border-top-right-radius: {e.borderRadiusTR:0.#}px; border-bottom-left-radius: {e.borderRadiusBL:0.#}px; border-bottom-right-radius: {e.borderRadiusBR:0.#}px; padding-top: {e.padding.top}px; padding-bottom: {e.padding.bottom}px; padding-left: {e.padding.left}px; padding-right: {e.padding.right}px; {boxHAlign}"">

                <ui:VisualElement name=""BackgroundLayer"" style=""position: absolute; left: 0; top: 0; right: 0; bottom: 0; overflow: hidden; border-top-left-radius: {boxMaxRadius:0.#}px; border-top-right-radius: {boxMaxRadius:0.#}px; border-bottom-left-radius: {boxMaxRadius:0.#}px; border-bottom-right-radius: {boxMaxRadius:0.#}px; picking-mode: Ignore; {(bgIsImage ? "" : "display: none;")}"" />

                <!-- Inside Left Portrait -->
{insideLeft}

                <!-- Text & Choices -->
                <ui:VisualElement name=""TextColumn"" style=""flex-grow: 1; flex-direction: column; justify-content: {textColumnJustify};"">
                    <ui:ScrollView name=""TextScroll"" mode=""Vertical"" style=""flex-grow: 1; background-color: rgba(0,0,0,0);"">
                        <ui:Label name=""DialogueText"" style=""{textFontDef}color: {Rgba(e.textColour)}; font-size: {e.textFontSize}px; white-space: normal; letter-spacing: {e.textLetterSpacing:0.#}px; -unity-text-align: {textAlign};"" />
                    </ui:ScrollView>
                    <ui:VisualElement name=""ChoiceContainer"" style=""margin-top: 10px; display: none;"" />
                    <ui:Label name=""AdvanceHint"" text=""{e.advanceHintText}"" style=""align-self: flex-end; color: {Rgba(e.hintColour)}; font-size: {e.hintFontSize}px; {(e.showAdvanceHint ? "" : "display: none;")}"" />
                </ui:VisualElement>

                <!-- Inside Right Portrait -->
{insideRight}

            </ui:VisualElement>

            <!-- Character Figure Panel (right) -->
{charRight}

            <!-- Outside Right Portrait -->
{outsideRight}

        </ui:VisualElement>

        <!-- Border Image Layer — root-level, positioned by the engine exactly
             on the box's border ring. It carries NO border colour of its own,
             so the colour border and the image border can never stack. -->
        <ui:VisualElement name=""BorderLayer"" style=""position: absolute; overflow: hidden; border-top-left-radius: {boxMaxRadius:0.#}px; border-top-right-radius: {boxMaxRadius:0.#}px; border-bottom-left-radius: {boxMaxRadius:0.#}px; border-bottom-right-radius: {boxMaxRadius:0.#}px; picking-mode: Ignore; {(borderIsImage ? "" : "display: none;")}"" />

        <!-- On-Border Portraits (positioned by the engine at runtime) -->
{borderLeft}
{borderRight}

        <!-- History Panel Overlay -->
        <ui:VisualElement name=""HistoryPanel"" style=""position: absolute; left: 10%; top: 15%; width: 80%; height: 70%; background-color: rgba(20, 20, 20, 0.95); border-radius: 8px; padding: 20px; display: none;"">
            <ui:ScrollView name=""HistoryContent"" style=""flex-grow: 1;"" />
            <ui:Button name=""CloseHistoryButton"" class=""dlg-close-button"" text=""Close History"" style=""margin-top: 10px;"" />
        </ui:VisualElement>

        <!-- Settings Panel Overlay -->
        <ui:VisualElement name=""SettingsPanel"" style=""position: absolute; left: 10%; top: 15%; width: 80%; height: 70%; background-color: rgba(20, 20, 20, 0.95); border-radius: 8px; padding: 20px; display: none;"">
            <ui:ScrollView name=""SettingsContent"" style=""flex-grow: 1;"" />
            <ui:Button name=""CloseSettingsButton"" class=""dlg-close-button"" text=""Close Settings"" style=""margin-top: 10px;"" />
        </ui:VisualElement>

        <!-- Toolbar Toggle + Panel -->
        <ui:Button name=""ToolbarToggle"" class=""dlg-toolbar-button"" text=""Menu"" style=""position: absolute; {tbPosition} {(e.showToolbar ? "" : "display: none;")}"" />
        <ui:VisualElement name=""ToolbarPanel"" style=""position: absolute; {tbPosition} {tbFlex} display: none;"">
            <ui:Button name=""HistoryButton"" class=""dlg-toolbar-button"" text=""History"" />
            <ui:Button name=""SettingsButton"" class=""dlg-toolbar-button"" text=""Settings"" style=""{(e.showSettingsButton ? "" : "display: none;")}"" />
            <ui:Button name=""RewindButton"" class=""dlg-toolbar-button"" text=""Rewind"" />
        </ui:VisualElement>

    </ui:VisualElement>

    <Style>
{uss}
    </Style>
</ui:UXML>";

        return uxml;
    }
    #endif
}
