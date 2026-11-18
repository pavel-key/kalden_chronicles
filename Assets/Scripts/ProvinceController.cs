
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Типы ресурсов, доступных в провинции.</summary>
public enum ResourceType
{
    Wood,
    Stone,
    Iron,
    Gold,
    Grain,
    MoonCrystal,
    Coal
}

[RequireComponent(typeof(MeshRenderer))]
public class ProvinceController : MonoBehaviour
{
    [Header("Province Attributes")]

    [SerializeField] private int provinceId;
    [SerializeField] private int countryId;
    [Header("Province Resources")]
    [SerializeField, Range(0, 255)] private int woodLevel;
    [SerializeField, Range(0, 255)] private int stoneLevel;
    [SerializeField, Range(0, 255)] private int ironLevel;
    [SerializeField, Range(0, 255)] private int goldLevel;
    [SerializeField, Range(0, 255)] private int grainLevel;
    [SerializeField, Range(0, 255)] private int moonCrystalLevel;
    [SerializeField, Range(0, 255)] private int coalLevel;
    [SerializeField] private RulerData ruler;

    [SerializeField] private Color provinceColor = Color.white;

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Renderer selectionOverlay;

    // Единственный город, принадлежащий этой провинции.
    private CityController city;

    // Единое состояние выбора и общая UI-панель для всех провинций.
    private static ProvinceController selectedProvince;
    private static GameObject provinceInfoPanel;
    private static Text provinceIdText;
    private static Text rulerNameText;
    private static Image rulerImage;

    // Единое состояние выбора и общая UI-панель для всех ресурсов.
    private static readonly Dictionary<ResourceType, Text> resourceLevelTexts =
    new Dictionary<ResourceType, Text>();

    private static readonly Dictionary<ResourceType, string> resourceTextObjectNames =
        new Dictionary<ResourceType, string>
    {
    { ResourceType.Wood, "woodtext" },
    { ResourceType.Stone, "stonetext" },
    { ResourceType.Iron, "irontext" },
    { ResourceType.Gold, "goldtext" },
    { ResourceType.Grain, "graintext" },
    { ResourceType.MoonCrystal, "mooncrystaltext" },
    { ResourceType.Coal, "coaltext" }
    };

    // Имена дочерних объектов с текстовыми данными провинции.
    private const string ProvinceIdTextObjectName = "ProvinceIdText";
    private const string RulerNameTextObjectName = "RulerNameText";
    private const string RulerImageObjectName = "RulerPortrait";
    private const string ProvinceWoodTextObjectName = "woodtext";

    /// <summary>Событие вызывается при выборе или снятии выбора провинции.</summary>
    public static event Action<ProvinceController> SelectionChanged;

    public static ProvinceController SelectedProvince => selectedProvince;

    /// <summary>
    /// Передаёт контроллеру провинций ссылки на общую UI-панель.
    /// Вызывается один раз из PoliticalMapGenerator.
    /// </summary>
    public static void ConfigureInfoUI(GameObject panel)
    {
        provinceInfoPanel = panel;
        provinceIdText = null;
        rulerNameText = null;
        rulerImage = null;

        resourceLevelTexts.Clear();

        if (panel != null)
        {
            Transform idTextObject = FindChildByName(
                panel.transform,
                ProvinceIdTextObjectName
            );

            if (idTextObject != null)
                provinceIdText = idTextObject.GetComponent<Text>();

            Transform rulerTextObject = FindChildByName(
                panel.transform,
                RulerNameTextObjectName
            );

            if (rulerTextObject != null)
                rulerNameText = rulerTextObject.GetComponent<Text>();

            Transform rulerImageObject = FindChildByName(
                panel.transform,
                RulerImageObjectName
            );

            foreach (KeyValuePair<ResourceType, string> entry
                     in resourceTextObjectNames)
            {
                Transform resourceObject = FindChildByName(
                    panel.transform,
                    entry.Value
                );

                if (resourceObject != null)
                {
                    Text resourceText = resourceObject.GetComponent<Text>();

                    if (resourceText != null)
                    {
                        resourceLevelTexts[entry.Key] = resourceText;
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"ProvinceController: объект '{entry.Value}' " +
                            "не содержит компонент Text.",
                            panel
                        );
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"ProvinceController: не найден объект '{entry.Value}'.",
                        panel
                    );
                }
            }

            if (rulerImageObject != null)
                rulerImage = rulerImageObject.GetComponent<Image>();

            if (provinceIdText == null)
            {
                Debug.LogWarning(
                    $"ProvinceController: не найден объект '{ProvinceIdTextObjectName}' " +
                    "с компонентом UI Text внутри Province Info Panel. " +
                    "Переименуйте нужный текстовый объект в ProvinceIdText.",
                    panel
                );
            }

            if (rulerNameText == null)
            {
                Debug.LogWarning(
                    $"ProvinceController: не найден объект '{RulerNameTextObjectName}' " +
                    "с компонентом UI Text внутри Province Info Panel. " +
                    "Имя правителя не будет отображаться.",
                    panel
                );
            }

            if (rulerImage == null)
            {
                Debug.LogWarning(
                    $"ProvinceController: не найден объект '{RulerImageObjectName}' " +
                    "с компонентом IMAGE внутри Province Info Panel. " +
                    "Портрет правителя не будет отображаться.",
                    panel
                );
            }
        }

        RefreshInfoUI();
    }

    /// <summary>
    /// Рекурсивно ищет дочерний объект по точному имени.
    /// Это позволяет иметь в панели несколько независимых текстовых полей.
    /// </summary>
    private static Transform FindChildByName(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildByName(parent.GetChild(i), objectName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static void RefreshInfoUI()
    {
        bool hasSelection = selectedProvince != null;

        if (provinceInfoPanel != null)
            provinceInfoPanel.SetActive(hasSelection);

        if (provinceIdText != null)
        {
            provinceIdText.text = hasSelection
                ? $"ID: {selectedProvince.ProvinceId}"
                : string.Empty;
        }

        if (rulerNameText != null)
        {
            RulerData selectedRuler = hasSelection
                ? selectedProvince.Ruler
                : null;

            rulerNameText.text = selectedRuler == null
                ? string.Empty
                : $"Правитель: {GetTitleDisplayName(selectedRuler.title)} {selectedRuler.rulerName}";
        }

        if (rulerImage != null && hasSelection)
        {
            RulerData selectedRuler = hasSelection
            ? selectedProvince.Ruler
            : null;

            rulerImage.sprite = selectedRuler.portrait;
        }

        foreach (KeyValuePair<ResourceType, Text> entry
                 in resourceLevelTexts)
        {
            if (entry.Value == null)
                continue;

            entry.Value.text = hasSelection
                ? selectedProvince.GetResourceLevel(entry.Key).ToString()
                : string.Empty;
        }

    }

    /// <summary>
    /// Возвращает отображаемое русское название титула правителя.
    /// </summary>
    private static string GetTitleDisplayName(RulerTitle title)
    {
        switch (title)
        {
            case RulerTitle.Lord:
                return "Лорд";
            case RulerTitle.Tsar:
                return "Царь";
            case RulerTitle.King:
                return "Король";
            case RulerTitle.Emperor:
                return "Император";
            case RulerTitle.Overlord:
                return "Владыка";
            case RulerTitle.Tyrant:
                return "Тиран";
            default:
                return title.ToString();
        }
    }

    public int ProvinceId => provinceId;
    public int CountryId => countryId;
    public int WoodLevel => woodLevel;
    public int StoneLevel => stoneLevel;
    public int IronLevel => ironLevel;
    public int GoldLevel => goldLevel;
    public int GrainLevel => grainLevel;
    public int MoonCrystalLevel => moonCrystalLevel;
    public int CoalLevel => coalLevel;
    public RulerData Ruler => ruler;
    public Color ProvinceColor => provinceColor;
    public CityController City => city;
    public Transform CityTransform => city != null ? city.transform : null;
    public Vector3 CityPosition => city != null
        ? city.transform.position
        : transform.position;

    /// <summary>Связывает провинцию с единственным городом, созданным внутри неё.</summary>
    /// <summary>
    /// Устанавливает значения всех семи ресурсов.
    /// Все значения автоматически ограничиваются диапазоном 0–255.
    /// </summary>
    public void SetResources(
        int wood,
        int stone,
        int iron,
        int gold,
        int grain,
        int moonCrystal,
        int coal)
    {
        woodLevel = Mathf.Clamp(wood, 0, 255);
        stoneLevel = Mathf.Clamp(stone, 0, 255);
        ironLevel = Mathf.Clamp(iron, 0, 255);
        goldLevel = Mathf.Clamp(gold, 0, 255);
        grainLevel = Mathf.Clamp(grain, 0, 255);
        moonCrystalLevel = Mathf.Clamp(moonCrystal, 0, 255);
        coalLevel = Mathf.Clamp(coal, 0, 255);
    }

    /// <summary>
    /// Возвращает уровень указанного типа ресурса.
    /// </summary>
    public int GetResourceLevel(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return woodLevel;
            case ResourceType.Stone: return stoneLevel;
            case ResourceType.Iron: return ironLevel;
            case ResourceType.Gold: return goldLevel;
            case ResourceType.Grain: return grainLevel;
            case ResourceType.MoonCrystal: return moonCrystalLevel;
            case ResourceType.Coal: return coalLevel;
            default: return 0;
        }
    }

    /// <summary>
    /// Изменяет уровень одного типа ресурса.
    /// </summary>
    public void SetResourceLevel(ResourceType type, int value)
    {
        value = Mathf.Clamp(value, 0, 255);

        switch (type)
        {
            case ResourceType.Wood: woodLevel = value; break;
            case ResourceType.Stone: stoneLevel = value; break;
            case ResourceType.Iron: ironLevel = value; break;
            case ResourceType.Gold: goldLevel = value; break;
            case ResourceType.Grain: grainLevel = value; break;
            case ResourceType.MoonCrystal: moonCrystalLevel = value; break;
            case ResourceType.Coal: coalLevel = value; break;
        }
    }

    public void SetCity(CityController newCity)
    {
        city = newCity;

        if (city == null)
        {
            Debug.LogWarning(
                $"ProvinceController: для провинции {provinceId} передана пустая ссылка на город.",
                this
            );
        }
    }

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Выбирает эту провинцию. Повторный вызов снимает выделение.
    /// </summary>
    public void OnSelect()
    {
        if (selectedProvince == this)
        {
            OnDeselect();
            return;
        }

        if (selectedProvince != null)
            selectedProvince.OnDeselectVisual();

        selectedProvince = this;
        SetSelectionVisible(true);
        RefreshInfoUI();
        SelectionChanged?.Invoke(selectedProvince);

        // Проверяем, принадлежит ли провинция игроку.
        // Если да — открываем экран управления.
        if (ProvinceManagementSceneManager.Instance != null)
        {
            ProvinceManagementSceneManager.Instance.OpenForProvince(this);
        }
    }

    /// <summary>Снимает выделение с этой провинции.</summary>
    public void OnDeselect()
    {
        if (selectedProvince != this)
        {
            OnDeselectVisual();
            return;
        }

        OnDeselectVisual();
        selectedProvince = null;
        RefreshInfoUI();
        SelectionChanged?.Invoke(null);
    }

    public static void ClearSelection()
    {
        if (selectedProvince != null)
            selectedProvince.OnDeselect();
    }

    private void OnDeselectVisual()
    {
        SetSelectionVisible(false);
    }

    private void SetSelectionVisible(bool visible)
    {
        if (selectionOverlay == null)
        {
            Transform overlay = transform.Find("SelectionOverlay");
            if (overlay != null)
                selectionOverlay = overlay.GetComponent<Renderer>();
        }

        if (selectionOverlay != null)
            selectionOverlay.enabled = visible;
    }

    private void OnDestroy()
    {
        if (selectedProvince == this)
        {
            selectedProvince = null;
            RefreshInfoUI();
            SelectionChanged?.Invoke(null);
        }
    }

    /// <summary>
    /// Инициализация провинции при генерации карты.
    /// </summary>
    public void Initialize(
        int newProvinceId,
        int newCountryId,
        RulerData assignedRuler,
        Material baseMaterial,
        string colorProperty = "_BaseColor")
    {
        provinceId = newProvinceId;
        countryId = newCountryId;
        ruler = assignedRuler;

        if (ruler == null)
        {
            Debug.LogError(
                $"Для провинции {provinceId} не найден правитель государства {countryId}.",
                this
            );
            return;
        }

        provinceColor = ruler.color;

        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        // Назначаем общий базовый материал.
        if (baseMaterial != null)
            meshRenderer.sharedMaterial = baseMaterial;

        // Применяем цвет правителя только к этой провинции.
        Material material = meshRenderer.sharedMaterial;

        if (material != null && material.HasProperty(colorProperty))
        {
            int colorId = Shader.PropertyToID(colorProperty);

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(colorId, provinceColor);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
        else
        {
            Debug.LogWarning(
                $"Материал провинции {provinceId} не содержит параметр {colorProperty}.",
                this
            );
        }
    }

    /// <summary>
    /// Смена правителя и обновление цвета провинции.
    /// </summary>
    public void SetRuler(RulerData newRuler)
    {
        if (newRuler == null)
            return;

        ruler = newRuler;
        provinceColor = ruler.color;

        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(
            Shader.PropertyToID("_BaseColor"),
            provinceColor
        );
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}
