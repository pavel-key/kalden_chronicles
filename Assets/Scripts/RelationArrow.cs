using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RelationArrow : MonoBehaviour
{
    [Header("Endpoints")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("Curve")]
    [Min(2)][SerializeField] private int segments = 30;
    [Min(0f)][SerializeField] private float arcHeight = 2f;
    [Min(0f)][SerializeField] private float distanceHeightFactor = 0.1f;

    [Header("Appearance")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color color = new Color(0f, 1f, 0.1f, 1f);
    [Min(0.001f)][SerializeField] private float lineWidth = 2f;
    [Min(0.01f)][SerializeField] private float arrowHeadLength = 5.5f;
    [Min(0.01f)][SerializeField] private float arrowHeadWidth = 2f;
    [SerializeField] private bool castShadows;

    [Header("Trade Icons")]
    [SerializeField] private float tradePanelHeightOffset = 0.25f;
    [Min(8f)][SerializeField] private float iconSize = 28f;
    [Min(0f)][SerializeField] private float iconSpacing = 4f;
    [Min(0.001f)][SerializeField] private float canvasScale = 0.04f;
    [SerializeField] private Color panelColor = new Color(0.08f, 0.06f, 0.04f, 0.88f);
    [SerializeField] private Color forwardTint = Color.white;
    [SerializeField] private Color backwardTint = new Color(1f, 0.82f, 0.45f, 1f);

    private LineRenderer curveRenderer;
    private LineRenderer arrowHeadRenderer;
    private Material runtimeMaterial;
    private bool initialized;

    private Canvas tradeCanvas;
    private RectTransform tradePanel;

    public Transform StartPoint => startPoint;
    public Transform EndPoint => endPoint;

    private void Awake()
    {
        EnsureRenderers();
    }

    /// <summary>
    /// Старый API оставлен для совместимости.
    /// </summary>
    public void Initialize(
        Transform from,
        Transform to,
        Color arrowColor,
        float width = 0.12f,
        float height = 2f)
    {
        startPoint = from;
        endPoint = to;

        color = new Color(0f, 1f, 0.1f, 1f);
        lineWidth = Mathf.Max(0.001f, width);
        arcHeight = Mathf.Max(0f, height);

        initialized = startPoint != null && endPoint != null;

        EnsureRenderers();
        ApplyAppearance();
        UpdateArrow();
    }

    /// <summary>
    /// forwardResources — торговля от startPoint к endPoint.
    /// backwardResources — торговля в обратном направлении.
    /// Индексы Sprite[] соответствуют значениям ResourceType.
    /// </summary>
    public void Initialize(
        Transform from,
        Transform to,
        Color arrowColor,
        float width,
        float height,
        IReadOnlyList<ResourceType> forwardResources,
        IReadOnlyList<ResourceType> backwardResources,
        Sprite[] resourceIcons)
    {
        Initialize(from, to, arrowColor, width, height);

        BuildTradePanel(
            forwardResources,
            backwardResources,
            resourceIcons
        );

        UpdateTradePanelTransform();
    }

    public void SetEndpoints(Transform from, Transform to)
    {
        startPoint = from;
        endPoint = to;

        initialized = startPoint != null && endPoint != null;

        UpdateArrow();
    }

    public void SetColor(Color newColor)
    {
        color = newColor;

        EnsureRenderers();
        ApplyAppearance();
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            if (startPoint == null || endPoint == null)
            {
                SetRenderersEnabled(false);

                if (tradeCanvas != null)
                    tradeCanvas.enabled = false;

                return;
            }

            initialized = true;
        }

        UpdateArrow();
    }

    private void EnsureRenderers()
    {
        if (curveRenderer == null)
        {
            curveRenderer = GetComponent<LineRenderer>();

            if (curveRenderer == null)
                curveRenderer = gameObject.AddComponent<LineRenderer>();
        }

        Transform headTransform = transform.Find("ArrowHead");

        if (headTransform == null)
        {
            GameObject headObject = new GameObject("ArrowHead");
            headTransform = headObject.transform;
            headTransform.SetParent(transform, false);
        }

        arrowHeadRenderer = headTransform.GetComponent<LineRenderer>();

        if (arrowHeadRenderer == null)
            arrowHeadRenderer = headTransform.gameObject.AddComponent<LineRenderer>();

        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader != null && runtimeMaterial == null)
            {
                runtimeMaterial = new Material(shader)
                {
                    name = "Runtime_RelationArrow_Material"
                };
            }
        }

        Material materialToUse = lineMaterial != null
            ? lineMaterial
            : runtimeMaterial;

        if (materialToUse != null)
        {
            curveRenderer.sharedMaterial = materialToUse;
            arrowHeadRenderer.sharedMaterial = materialToUse;
        }

        ConfigureRenderer(curveRenderer);
        ConfigureRenderer(arrowHeadRenderer);

        ApplyAppearance();
    }

    private void ConfigureRenderer(LineRenderer renderer)
    {
        renderer.useWorldSpace = true;
        renderer.loop = false;
        renderer.alignment = LineAlignment.View;
        renderer.textureMode = LineTextureMode.Stretch;

        renderer.shadowCastingMode = castShadows
            ? UnityEngine.Rendering.ShadowCastingMode.On
            : UnityEngine.Rendering.ShadowCastingMode.Off;

        renderer.receiveShadows = false;
    }

    private void ApplyAppearance()
    {
        if (curveRenderer == null || arrowHeadRenderer == null)
            return;

        curveRenderer.startColor = color;
        curveRenderer.endColor = color;
        curveRenderer.widthMultiplier = 1f;
        curveRenderer.startWidth = lineWidth;
        curveRenderer.endWidth = lineWidth;

        arrowHeadRenderer.startColor = color;
        arrowHeadRenderer.endColor = color;
        arrowHeadRenderer.widthMultiplier = 1f;
        arrowHeadRenderer.startWidth = lineWidth;
        arrowHeadRenderer.endWidth = lineWidth;
    }

    private void UpdateArrow()
    {
        if (startPoint == null || endPoint == null)
        {
            SetRenderersEnabled(false);

            if (tradeCanvas != null)
                tradeCanvas.enabled = false;

            return;
        }

        EnsureRenderers();

        Vector3 start = startPoint.position;
        Vector3 end = endPoint.position;

        float distance = Vector3.Distance(start, end);

        if (distance < 0.001f)
        {
            SetRenderersEnabled(false);

            if (tradeCanvas != null)
                tradeCanvas.enabled = false;

            return;
        }

        Vector3 midpoint = (start + end) * 0.5f;

        float height = arcHeight + distance * distanceHeightFactor;

        Vector3 control = midpoint + Vector3.up * height;

        int pointCount = Mathf.Max(2, segments) + 1;

        curveRenderer.positionCount = pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);

            curveRenderer.SetPosition(
                i,
                EvaluateQuadraticBezier(start, control, end, t)
            );
        }

        Vector3 tangent = (end - control).normalized;

        Vector3 side = Vector3.Cross(tangent, Vector3.up);

        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.Cross(tangent, Vector3.right);

        side.Normalize();

        float headLength = Mathf.Min(
            arrowHeadLength,
            distance * 0.4f
        );

        Vector3 headBase = end - tangent * headLength;

        Vector3 wingOffset = side * (arrowHeadWidth * 0.5f);

        arrowHeadRenderer.positionCount = 3;

        arrowHeadRenderer.SetPosition(0, headBase + wingOffset);
        arrowHeadRenderer.SetPosition(1, end);
        arrowHeadRenderer.SetPosition(2, headBase - wingOffset);

        SetRenderersEnabled(true);
        ApplyAppearance();

        if (tradeCanvas != null)
        {
            tradeCanvas.enabled = true;

            Vector3 panelPosition =
                EvaluateQuadraticBezier(start, control, end, 0.5f);

            tradeCanvas.transform.position =
                panelPosition + Vector3.up * tradePanelHeightOffset;

            FaceCamera();
        }
    }

    private void BuildTradePanel(
        IReadOnlyList<ResourceType> forwardResources,
        IReadOnlyList<ResourceType> backwardResources,
        Sprite[] resourceIcons)
    {
        DestroyTradePanel();

        if (resourceIcons == null || resourceIcons.Length == 0)
            return;

        bool hasForward = HasVisibleIcons(forwardResources, resourceIcons);
        bool hasBackward = HasVisibleIcons(backwardResources, resourceIcons);

        if (!hasForward && !hasBackward)
            return;

        GameObject canvasObject = new GameObject(
            "TradeIconsCanvas",
            typeof(RectTransform),
            typeof(Canvas)
        );

        canvasObject.transform.SetParent(transform, false);

        tradeCanvas = canvasObject.GetComponent<Canvas>();
        tradeCanvas.renderMode = RenderMode.WorldSpace;
        tradeCanvas.sortingOrder = 20;

        RectTransform canvasRect =
            canvasObject.GetComponent<RectTransform>();

        canvasRect.sizeDelta = new Vector2(320f, 100f);
        canvasRect.localScale = Vector3.one * canvasScale;

        GameObject panelObject = new GameObject(
            "Panel",
            typeof(RectTransform),
            typeof(Image)
        );

        panelObject.transform.SetParent(canvasObject.transform, false);

        tradePanel = panelObject.GetComponent<RectTransform>();

        tradePanel.anchorMin = new Vector2(0.5f, 0.5f);
        tradePanel.anchorMax = new Vector2(0.5f, 0.5f);
        tradePanel.pivot = new Vector2(0.5f, 0.5f);
        tradePanel.anchoredPosition = Vector2.zero;

        tradePanel.sizeDelta = new Vector2(
            320f,
            hasForward && hasBackward ? 76f : 42f
        );

        panelObject.GetComponent<Image>().color = panelColor;

        int rowCount =
            (hasForward ? 1 : 0) +
            (hasBackward ? 1 : 0);

        float firstY = rowCount == 2 ? 18f : 0f;

        if (hasForward)
        {
            CreateResourceRow(
                forwardResources,
                resourceIcons,
                true,
                firstY
            );
        }

        if (hasBackward)
        {
            CreateResourceRow(
                backwardResources,
                resourceIcons,
                false,
                rowCount == 2 ? -18f : 0f
            );
        }
    }

    private bool HasVisibleIcons(
        IReadOnlyList<ResourceType> resources,
        Sprite[] icons)
    {
        if (resources == null)
            return false;

        for (int i = 0; i < resources.Count; i++)
        {
            int index = (int)resources[i];

            if (index >= 0 &&
                index < icons.Length &&
                icons[index] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void CreateResourceRow(
        IReadOnlyList<ResourceType> resources,
        Sprite[] icons,
        bool forward,
        float y)
    {
        List<Sprite> visible = new List<Sprite>();

        if (resources != null)
        {
            for (int i = 0; i < resources.Count; i++)
            {
                int index = (int)resources[i];

                if (index >= 0 &&
                    index < icons.Length &&
                    icons[index] != null)
                {
                    visible.Add(icons[index]);
                }
            }
        }

        if (visible.Count == 0)
            return;

        float itemWidth = iconSize + iconSpacing;

        float totalWidth =
            28f + visible.Count * itemWidth - iconSpacing;

        float startX = -totalWidth * 0.5f;

        GameObject directionObject = new GameObject(
            forward ? "DirectionForward" : "DirectionBackward",
            typeof(RectTransform),
            typeof(Text)
        );

        directionObject.transform.SetParent(tradePanel, false);

        RectTransform directionRect =
            directionObject.GetComponent<RectTransform>();

        directionRect.anchorMin = new Vector2(0.5f, 0.5f);
        directionRect.anchorMax = new Vector2(0.5f, 0.5f);
        directionRect.pivot = new Vector2(0f, 0.5f);

        directionRect.sizeDelta = new Vector2(28f, iconSize + 4f);
        directionRect.anchoredPosition = new Vector2(startX, y);

        Text directionText = directionObject.GetComponent<Text>();

        directionText.text = forward ? "→" : "←";
        directionText.alignment = TextAnchor.MiddleCenter;
        directionText.fontSize = 26;
        directionText.font = GetBuiltinFont();
        directionText.color = forward ? forwardTint : backwardTint;
        directionText.raycastTarget = false;

        float x = startX + 28f;

        for (int i = 0; i < visible.Count; i++)
        {
            GameObject iconObject = new GameObject(
                "ResourceIcon_" + i,
                typeof(RectTransform),
                typeof(Image)
            );

            iconObject.transform.SetParent(tradePanel, false);

            RectTransform iconRect =
                iconObject.GetComponent<RectTransform>();

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);

            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(x, y);

            Image image = iconObject.GetComponent<Image>();

            image.sprite = visible[i];
            image.preserveAspect = true;
            image.color = forward ? forwardTint : backwardTint;
            image.raycastTarget = false;

            x += itemWidth;
        }
    }

    private static Font GetBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return font;
    }

    private void UpdateTradePanelTransform()
    {
        if (tradeCanvas == null)
            return;

        tradeCanvas.transform.localScale =
            Vector3.one * canvasScale;

        FaceCamera();
    }

    private void FaceCamera()
    {
        if (tradeCanvas == null)
            return;

        Camera cameraToFace = Camera.main;

        if (cameraToFace == null)
            return;

        tradeCanvas.transform.rotation =
            cameraToFace.transform.rotation;
    }

    private void DestroyTradePanel()
    {
        if (tradeCanvas != null)
        {
            if (Application.isPlaying)
                Destroy(tradeCanvas.gameObject);
            else
                DestroyImmediate(tradeCanvas.gameObject);
        }

        tradeCanvas = null;
        tradePanel = null;
    }

    private static Vector3 EvaluateQuadraticBezier(
        Vector3 start,
        Vector3 control,
        Vector3 end,
        float t)
    {
        float inverseT = 1f - t;

        return inverseT * inverseT * start
             + 2f * inverseT * t * control
             + t * t * end;
    }

    private void SetRenderersEnabled(bool value)
    {
        if (curveRenderer != null)
            curveRenderer.enabled = value;

        if (arrowHeadRenderer != null)
            arrowHeadRenderer.enabled = value;
    }

    private void OnDestroy()
    {
        DestroyTradePanel();

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }
}