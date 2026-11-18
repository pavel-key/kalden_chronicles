
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PoliticalMapGenerator : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Менеджер игрока. Выбирает государство и размещает корону после генерации карты.")]
    [SerializeField] private PlayerManager playerManager;

    [Header("Map Settings")]
    [Min(1)] public int regionCount = 20;
    [Min(1)] public int maxRegionsPerCountry = 10;
    [Min(1)] public int minRegionsPerCountry = 5;
    [Min(1f)] public float mapWidth = 100f;
    [Min(1f)] public float mapHeight = 100f;
    public int seed = 12345;

    [Header("Voronoi Noise")]
    [Range(32, 512)] public int resolution = 160;
    [Min(0f)] public float noiseStrength = 3f;
    [Min(0.001f)] public float noiseScale = 0.08f;

    [Header("Province Borders")]
    [Min(0.005f)] public float borderThickness = 0.08f;
    [Min(0f)] public float borderHeight = 0.03f;
    public Color borderColor = Color.black;
    public bool drawOuterBorder = true;

    [Header("Cities")]
    [Tooltip("Префаб модели города")]
    public GameObject cityPrefab;
    [Tooltip("Префаб столицы — заменяет обычный город в одной провинции")]
    public GameObject capitalPrefab;

    private HashSet<int> capitalRegionIndices = new HashSet<int>();

    [Header("Ruler Database")]

    [Tooltip("Справочник правителей государств")]
    public RulerDatabase rulerDatabase;

    [Header("Rendering")]
    public Material regionMaterial;
    public string baseMapColorProperty = "_BaseColor";
    public bool addMeshColliders = false;

    [Header("Province Selection")]
    [Tooltip("Разрешить выбор провинций нажатием левой кнопки мыши")]
    public bool enableProvinceSelection = true;
    [Tooltip("Камера, из которой выполняется выбор. Если не назначена, используется Camera.main")]
    public Camera selectionCamera;
    [Tooltip("Материал/шейдер визуального выделения выбранной провинции")]
    public Material selectionMaterial;
    [Tooltip("Подъём слоя выделения над поверхностью провинции для предотвращения z-fighting")]
    [Min(0f)] public float selectionSurfaceOffset = 0.04f;

    [Header("Province Info UI")]
    [Tooltip("UI-панель с информацией о выбранной провинции. Скрывается, когда провинция не выбрана.")]
    public GameObject provinceInfoPanel;

    [Header("Generation")]
    public bool generateOnStart = true;

    [Header("Trade Relations")]
    [Tooltip("Менеджер торговых связей. Если не назначен, будет искаться на этом же объекте.")]
    [SerializeField] private RelationManager relationManager;


    private class RegionData
    {
        public Vector2 center;
        public HashSet<int> neighbors = new HashSet<int>();
        public int countryId = -1;

        // Ресурсы провинции: каждое значение находится в диапазоне 0–255.
        public int woodLevel;
        public int stoneLevel;
        public int ironLevel;
        public int goldLevel;
        public int grainLevel;
        public int moonCrystalLevel;
        public int coalLevel;

        public List<Vector3> vertices = new List<Vector3>();
        public List<Vector2> uvs = new List<Vector2>();
        public List<int> triangles = new List<int>();

        public List<CityCandidate> cityCandidates = new List<CityCandidate>();
    }

    private class CityCandidate
    {
        public Vector2 position;
        public float centerDistance;
        public float borderClearance;
    }

    private class CountryData
    {
        public List<int> regionIds = new List<int>();
        public Color color;
    }

    private ProvinceController selectedProvince;
    private Material fallbackSelectionMaterial;

    /// <summary>Провинция, выбранная в данный момент.</summary>
    public ProvinceController SelectedProvince => ProvinceController.SelectedProvince;

    private void OnEnable()
    {
        ProvinceController.SelectionChanged += HandleProvinceSelectionChanged;
        if (provinceInfoPanel != null)
        {
            ProvinceController.ConfigureInfoUI(provinceInfoPanel);
        }
    }

    private void OnDisable()
    {
        ProvinceController.SelectionChanged -= HandleProvinceSelectionChanged;
        ProvinceController.ConfigureInfoUI(provinceInfoPanel);
    }

    private void HandleProvinceSelectionChanged(ProvinceController province)
    {
        selectedProvince = province;
    }

    private void Start()
    {
        selectedProvince = ProvinceController.SelectedProvince;

        if (generateOnStart)
            GenerateMap();
    }

    private void Update()
    {
        if (!enableProvinceSelection || !Input.GetMouseButtonDown(0))
            return;

        SelectProvinceUnderCursor();
    }

    /// <summary>
    /// Выполняет Raycast из камеры через позицию курсора и выбирает
    /// ближайшую провинцию, попавшую под курсор.
    /// </summary>
    public void SelectProvinceUnderCursor()
    {
        Camera cameraToUse = selectionCamera != null
            ? selectionCamera
            : Camera.main;

        if (cameraToUse == null)
        {
            Debug.LogWarning(
                "PoliticalMapGenerator: не найдена камера для выбора провинций. " +
                "Назначьте Selection Camera или пометьте камеру как MainCamera.",
                this
            );
            return;
        }

        Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        ProvinceController hitProvince = null;

        foreach (RaycastHit hit in hits)
        {
            hitProvince = hit.collider.GetComponentInParent<ProvinceController>();
            if (hitProvince != null)
                break;
        }

        if (hitProvince != null)
            hitProvince.OnSelect();
        else
            ProvinceController.ClearSelection();
    }

    /// <summary>
    /// Устанавливает выбранную провинцию. Можно вызывать из других скриптов.
    /// Передача null снимает выделение.
    /// </summary>
    public void SetSelectedProvince(ProvinceController province)
    {
        if (province != null)
            province.OnSelect();
        else
            ProvinceController.ClearSelection();
    }

    private void CreateSelectionOverlay(
        GameObject provinceObject,
        Mesh provinceMesh)
    {
        GameObject overlayObject = new GameObject("SelectionOverlay");
        overlayObject.transform.SetParent(provinceObject.transform, false);
        overlayObject.transform.localPosition = new Vector3(
            0f, selectionSurfaceOffset, 0f
        );

        overlayObject.AddComponent<MeshFilter>().sharedMesh = provinceMesh;

        MeshRenderer overlayRenderer =
            overlayObject.AddComponent<MeshRenderer>();

        overlayRenderer.sharedMaterial = GetSelectionMaterial();
        overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.enabled = false;
    }

    private Material GetSelectionMaterial()
    {
        if (selectionMaterial != null)
            return selectionMaterial;

        if (fallbackSelectionMaterial != null)
            return fallbackSelectionMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogError(
                "PoliticalMapGenerator: не найден шейдер для выделения провинции.",
                this
            );
            return regionMaterial;
        }

        fallbackSelectionMaterial = new Material(shader)
        {
            name = "Runtime_ProvinceSelection",
            color = new Color(1f, 0.85f, 0.1f, 0.55f)
        };

        // Для стандартных/URP материалов пытаемся включить прозрачный режим.
        if (fallbackSelectionMaterial.HasProperty("_Surface"))
            fallbackSelectionMaterial.SetFloat("_Surface", 1f);
        if (fallbackSelectionMaterial.HasProperty("_Mode"))
            fallbackSelectionMaterial.SetFloat("_Mode", 3f);
        if (fallbackSelectionMaterial.HasProperty("_BaseColor"))
            fallbackSelectionMaterial.SetColor(
                "_BaseColor", new Color(1f, 0.85f, 0.1f, 0.55f)
            );
        if (fallbackSelectionMaterial.HasProperty("_Color"))
            fallbackSelectionMaterial.SetColor(
                "_Color", new Color(1f, 0.85f, 0.1f, 0.55f)
            );

        return fallbackSelectionMaterial;
    }

    [ContextMenu("Generate Political Map")]
    public void GenerateMap()
    {
        // Ищем RelationManager на том же объекте, если ссылка не назначена вручную.
        if (relationManager == null)
            relationManager = GetComponent<RelationManager>();

        ClearMap();

        if (relationManager != null)
            relationManager.SetRandomSeed(unchecked(seed ^ 0x2C9277B5));

        if (regionCount < 1 || resolution < 1)
            return;

        System.Random random = new System.Random();
        RegionData[] regions = new RegionData[regionCount];

        // Генерация центров провинций.
        for (int i = 0; i < regionCount; i++)
        {
            regions[i] = new RegionData
            {
                center = new Vector2(
                    (float)random.NextDouble() * mapWidth,
                    (float)random.NextDouble() * mapHeight
                )
            };
        }

        int[,] ownership = new int[resolution, resolution];

        float cellWidth = mapWidth / resolution;
        float cellHeight = mapHeight / resolution;

        // Построение ячеек Вороного.
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float sampleX = (x + 0.5f) * cellWidth;
                float sampleZ = (z + 0.5f) * cellHeight;

                Vector2 sample = WarpPosition(sampleX, sampleZ);
                int regionIndex = FindNearestRegion(sample, regions);

                ownership[x, z] = regionIndex;

                Vector2 cellCenter = new Vector2(
                    (x + 0.5f) * cellWidth,
                    (z + 0.5f) * cellHeight
                );

                regions[regionIndex].cityCandidates.Add(
                    new CityCandidate
                    {
                        position = cellCenter,
                        centerDistance = (
                            cellCenter - regions[regionIndex].center
                        ).sqrMagnitude
                    }
                );

                AddQuad(
                    regions[regionIndex],
                    x * cellWidth,
                    z * cellHeight,
                    (x + 1) * cellWidth,
                    (z + 1) * cellHeight
                );
            }
        }

        // Рассчитываем отступ каждой ячейки от границы её провинции.
        CalculateCityBorderClearances(
            ownership,
            regions,
            cellWidth,
            cellHeight
        );

        BuildAdjacency(ownership, regions);

        List<CountryData> countries =
            GenerateCountries(regions, random);

        MergeSingleRegionCountries(countries, regions);

        // Ресурсы генерируются отдельным генератором случайных чисел,
        // чтобы их добавление не меняло раскладку провинций и стран.
        GenerateProvinceResources(regions);

        // Обновляем справочник правителей.
        if (rulerDatabase != null)
        {
            rulerDatabase.ClearDatabase();

            for (int i = 0; i < countries.Count; i++)
            {
                CountryData country = countries[i];

                rulerDatabase.AddRuler(
                    i,
                    country.color,
                    country.regionIds.Count,
                    random
                );
            }
            /*
            // Выводим полный справочник правителей в консоль.
            Debug.Log("========== RULER DATABASE ==========");

            foreach (RulerData ruler in rulerDatabase.Rulers)
            {
                Debug.Log(
                    $"Государство ID: {ruler.countryId} | " +
                    $"Титул: {ruler.title} | " +
                    $"Имя: {ruler.rulerName} | " +
                    $"Цвет: {ruler.color} | " +
                    $"Количество владений: {ruler.holdingsCount}"
                );
            }

            Debug.Log(
                $"Всего правителей: {rulerDatabase.Rulers.Count}"
            );

            Debug.Log("====================================");*/
        }
        else
        {
            Debug.LogWarning(
                "RulerDatabase не назначен в PoliticalMapGenerator."
            );
        }

        // Выбираем одну провинцию для столицы.
        // Используется seed генерации карты.
        capitalRegionIndices.Clear();

        foreach (CountryData country in countries)
        {
            if (country.regionIds.Count == 0)
                continue;

            int capitalRegionId = country.regionIds[
                random.Next(country.regionIds.Count)
            ];

            capitalRegionIndices.Add(capitalRegionId);
        }

        // Создание стран и провинций.
        for (int i = 0; i < countries.Count; i++)
        {
            CreateCountryObject(
                i, countries[i], regions
            );
        }

        // Все ProvinceController и города к этому моменту уже созданы.
        // Теперь можно безопасно строить торговые связи между соседями.
        CreateTradeRelations(regions);

        // Рисуем границы после создания заливки,
        // чтобы линии располагались поверх провинций.
        CreateBorders(ownership, cellWidth, cellHeight);

        // Выбираем государство игрока и размещаем корону.
        // К этому моменту все провинции, города и столицы созданы.

        if (playerManager != null)
        {
            playerManager.Initialize(transform);

            // Перемещаем камеру к столице игрока.
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                CameraMovement cameraMovement =
                    mainCamera.GetComponent<CameraMovement>();

                if (cameraMovement != null)
                {
                    cameraMovement.MoveToMyCapital(playerManager);
                }
                else
                {
                    Debug.LogWarning(
                        "PoliticalMapGenerator: на Main Camera " +
                        "не найден CameraMovement."
                    );
                }
            }
        }
        else
        {
            Debug.LogWarning(
                "PoliticalMapGenerator: PlayerManager не назначен. " +
                "Государство игрока не будет выбрано.",
                this
            );
        }

        Debug.Log(
            $"Карта создана. Провинций: {regionCount}, " +
            $"стран: {countries.Count}."
        );
    }


    /// <summary>
    /// Создаёт торговые связи между соседними провинциями разных стран.
    /// Метод вызывается после создания всех провинций и городов.
    /// Каждая пара обрабатывается ровно один раз.
    /// </summary>
    private void CreateTradeRelations(RegionData[] regions)
    {
        if (relationManager == null)
        {
            Debug.LogWarning(
                "PoliticalMapGenerator: RelationManager не назначен. " +
                "Добавьте RelationManager на тот же объект или назначьте ссылку в Inspector.",
                this
            );
            return;
        }

        ProvinceController[] provinceControllers =
            GetComponentsInChildren<ProvinceController>(true);

        Dictionary<int, ProvinceController> provincesById =
            new Dictionary<int, ProvinceController>();

        foreach (ProvinceController province in provinceControllers)
        {
            if (province != null)
                provincesById[province.ProvinceId] = province;
        }

        int checkedPairs = 0;
        int createdRelations = 0;

        for (int provinceId = 0; provinceId < regions.Length; provinceId++)
        {
            if (!provincesById.TryGetValue(provinceId, out ProvinceController province))
                continue;

            foreach (int neighborId in regions[provinceId].neighbors)
            {
                // Условие гарантирует, что A-B и B-A не обрабатываются дважды.
                if (neighborId <= provinceId || neighborId < 0 || neighborId >= regions.Length)
                    continue;

                if (!provincesById.TryGetValue(neighborId, out ProvinceController neighbor))
                    continue;

                checkedPairs++;

                if (relationManager.TryCreateRelation(province, neighbor))
                    createdRelations++;
            }
        }

        Debug.Log(
            $"Торговые связи: проверено соседних пар — {checkedPairs}, " +
            $"создано связей — {createdRelations}.",
            this
        );
    }

    /// <summary>
    /// Назначает каждой провинции семь независимых ресурсов в диапазоне 0–255.
    /// Одинаковый seed карты даёт одинаковое распределение ресурсов.
    /// </summary>
    private void GenerateProvinceResources(RegionData[] regions)
    {
        // Отдельный seed не влияет на случайные решения генерации карты.
        System.Random resourceRandom = new System.Random(unchecked(seed ^ 0x5F3759DF));

        for (int i = 0; i < regions.Length; i++)
        {
            RegionData region = regions[i];

            region.woodLevel = resourceRandom.Next(0, 256);
            region.stoneLevel = resourceRandom.Next(0, 256);
            region.ironLevel = resourceRandom.Next(0, 256);
            region.goldLevel = resourceRandom.Next(0, 256);
            region.grainLevel = resourceRandom.Next(0, 256);
            region.moonCrystalLevel = resourceRandom.Next(0, 256);
            region.coalLevel = resourceRandom.Next(0, 256);
        }
    }


    private void MergeSingleRegionCountries(
        List<CountryData> countries,
        RegionData[] regions)
    {
        bool changed;

        do
        {
            changed = false;

            // Рассматриваем страны, пока их индексы
            // остаются неизменными.
            for (int i = 0; i < countries.Count; i++)
            {
                CountryData source = countries[i];

                // Страна должна состоять ровно из одного региона.
                if (source.regionIds.Count != 1)
                    continue;

                int regionId = source.regionIds[0];
                int bestCountryId = -1;
                int largestSize = 1;

                // Ищем самую большую соседнюю страну.
                foreach (int neighborId in regions[regionId].neighbors)
                {
                    int neighborCountryId =
                        regions[neighborId].countryId;

                    if (neighborCountryId < 0 ||
                        neighborCountryId >= countries.Count ||
                        neighborCountryId == i)
                    {
                        continue;
                    }

                    CountryData neighborCountry =
                        countries[neighborCountryId];

                    if (neighborCountry.regionIds.Count > largestSize)
                    {
                        largestSize = neighborCountry.regionIds.Count;
                        bestCountryId = neighborCountryId;
                    }
                }

                // Подходящего соседа пока нет.
                // Попробуем ещё раз после объединения других стран.
                if (bestCountryId == -1)
                    continue;

                CountryData target = countries[bestCountryId];

                // Передаём регион соседней стране.
                target.regionIds.Add(regionId);
                regions[regionId].countryId = bestCountryId;

                // Исходная страна теперь пуста.
                source.regionIds.Clear();

                changed = true;
            }

        } while (changed);

        // Удаляем опустевшие страны.
        countries.RemoveAll(country =>
            country.regionIds.Count == 0);

        // Перенумеровываем страны, чтобы индексы
        // соответствовали regions[].countryId.
        for (int countryId = 0;
             countryId < countries.Count;
             countryId++)
        {
            foreach (int regionId in countries[countryId].regionIds)
            {
                regions[regionId].countryId = countryId;
            }
        }

        Debug.Log(
            $"Объединение завершено. Осталось стран: {countries.Count}."
        );
    }


    private void BuildAdjacency(
        int[,] ownership,
        RegionData[] regions)
    {
        int width = ownership.GetLength(0);
        int height = ownership.GetLength(1);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int current = ownership[x, z];

                if (x + 1 < width)
                {
                    int right = ownership[x + 1, z];

                    if (current != right)
                    {
                        regions[current].neighbors.Add(right);
                        regions[right].neighbors.Add(current);
                    }
                }

                if (z + 1 < height)
                {
                    int up = ownership[x, z + 1];

                    if (current != up)
                    {
                        regions[current].neighbors.Add(up);
                        regions[up].neighbors.Add(current);
                    }
                }
            }
        }
    }

    private List<CountryData> GenerateCountries(
        RegionData[] regions,
        System.Random random)
    {
        List<CountryData> countries = new List<CountryData>();
        List<int> unassigned = new List<int>();

        for (int i = 0; i < regions.Length; i++)
            unassigned.Add(i);

        Shuffle(unassigned, random);

        int remaining = regions.Length;
        int maxSize = Mathf.Max(minRegionsPerCountry, maxRegionsPerCountry);

        while (remaining > 0)
        {
            int seedRegion = -1;

            foreach (int id in unassigned)
            {
                if (regions[id].countryId == -1)
                {
                    seedRegion = id;
                    break;
                }
            }

            if (seedRegion == -1)
                break;

            int targetSize = random.Next(1, maxSize + 1);
            targetSize = Mathf.Min(targetSize, remaining);

            CountryData country = new CountryData
            {
                color = GenerateCountryColor(random)
            };

            int countryId = countries.Count;

            regions[seedRegion].countryId = countryId;
            country.regionIds.Add(seedRegion);
            remaining--;

            List<int> frontier = new List<int>();
            AddFrontier(seedRegion, regions, frontier);

            // Страна расширяется исключительно через соседей.
            while (country.regionIds.Count < targetSize &&
                   frontier.Count > 0)
            {
                int index = random.Next(frontier.Count);
                int candidate = frontier[index];
                frontier.RemoveAt(index);

                if (regions[candidate].countryId != -1)
                    continue;

                regions[candidate].countryId = countryId;
                country.regionIds.Add(candidate);
                remaining--;

                AddFrontier(candidate, regions, frontier);
            }

            countries.Add(country);
        }

        return countries;
    }

    private Color GenerateCountryColor(System.Random random)
    {
        return Color.HSVToRGB(
            (float)random.NextDouble(),
            0.55f + (float)random.NextDouble() * 0.3f,
            0.75f + (float)random.NextDouble() * 0.2f
        );
    }

    private void AddFrontier(
        int regionIndex,
        RegionData[] regions,
        List<int> frontier)
    {
        foreach (int neighbor in regions[regionIndex].neighbors)
        {
            if (regions[neighbor].countryId == -1 &&
                !frontier.Contains(neighbor))
            {
                frontier.Add(neighbor);
            }
        }
    }

    private void Shuffle<T>(
        List<T> list,
        System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private Vector2 WarpPosition(float x, float z)
    {
        float offsetX = seed * 0.0137f;
        float offsetZ = seed * 0.0271f;

        float noiseX = Mathf.PerlinNoise(
            x * noiseScale + offsetX,
            z * noiseScale + offsetZ
        );

        float noiseZ = Mathf.PerlinNoise(
            x * noiseScale + offsetZ + 100f,
            z * noiseScale + offsetX + 100f
        );

        return new Vector2(
            x + (noiseX - 0.5f) * 2f * noiseStrength,
            z + (noiseZ - 0.5f) * 2f * noiseStrength
        );
    }

    private int FindNearestRegion(
        Vector2 point,
        RegionData[] regions)
    {
        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < regions.Length; i++)
        {
            float distance =
                (point - regions[i].center).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private void AddQuad(
        RegionData region,
        float x0, float z0,
        float x1, float z1)
    {
        int start = region.vertices.Count;

        region.vertices.Add(new Vector3(
            x0 - region.center.x, 0f, z0 - region.center.y));
        region.vertices.Add(new Vector3(
            x0 - region.center.x, 0f, z1 - region.center.y));
        region.vertices.Add(new Vector3(
            x1 - region.center.x, 0f, z1 - region.center.y));
        region.vertices.Add(new Vector3(
            x1 - region.center.x, 0f, z0 - region.center.y));

        region.uvs.Add(new Vector2(x0 / mapWidth, z0 / mapHeight));
        region.uvs.Add(new Vector2(x0 / mapWidth, z1 / mapHeight));
        region.uvs.Add(new Vector2(x1 / mapWidth, z1 / mapHeight));
        region.uvs.Add(new Vector2(x1 / mapWidth, z0 / mapHeight));

        region.triangles.Add(start);
        region.triangles.Add(start + 1);
        region.triangles.Add(start + 2);
        region.triangles.Add(start);
        region.triangles.Add(start + 2);
        region.triangles.Add(start + 3);
    }

    private void CreateCountryObject(
        int countryIndex,
        CountryData country,
        RegionData[] regions)
    {
        GameObject countryObject = new GameObject(
            $"Country_{countryIndex + 1:000}"
        );

        countryObject.transform.SetParent(transform, false);
        countryObject.transform.localPosition = Vector3.zero;

        foreach (int regionIndex in country.regionIds)
        {
            CreateRegionObject(
                countryObject,
                regionIndex,
                regions[regionIndex],
                countryIndex,
                capitalRegionIndices.Contains(regionIndex)
            );
        }
    }

    private void CreateRegionObject(
        GameObject countryObject,
        int index,
        RegionData region,
        int countryId,
        bool isCapital
        )
    {
        GameObject regionObject = new GameObject(
            $"Province_{index + 1:000}"
        );

        regionObject.transform.SetParent(
            countryObject.transform, false
        );

        regionObject.transform.localPosition = new Vector3(
            region.center.x, 0f, region.center.y
        );

        Mesh mesh = new Mesh
        {
            name = $"ProvinceMesh_{index + 1:000}"
        };

        if (region.vertices.Count > 65535)
            mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(region.vertices);
        mesh.SetUVs(0, region.uvs);
        mesh.SetTriangles(region.triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        regionObject.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer meshRenderer =
            regionObject.AddComponent<MeshRenderer>();

        Material material;

        if (regionMaterial != null)
        {
            material = new Material(regionMaterial);
        }
        else
        {
            Shader shader = Shader.Find(
                "Universal Render Pipeline/Lit"
            );

            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogError("Не найден совместимый Shader.");
                Destroy(regionObject);
                Destroy(mesh);
                return;
            }

            material = new Material(shader);
        }

        meshRenderer.sharedMaterial = material;

        ProvinceController provinceController = regionObject.GetComponent<ProvinceController>();

        if (provinceController == null)
        {
            provinceController =
                regionObject.AddComponent<ProvinceController>();
        }

        RulerData assignedRuler = rulerDatabase != null
            ? rulerDatabase.GetRulerByCountryId(countryId)
            : null;

        provinceController.Initialize(
            index,
            countryId,
            assignedRuler,
            regionMaterial,
            baseMapColorProperty
        );

        // Передаём в контроллер все семь ресурсов этой провинции.
        provinceController.SetResources(
            region.woodLevel,
            region.stoneLevel,
            region.ironLevel,
            region.goldLevel,
            region.grainLevel,
            region.moonCrystalLevel,
            region.coalLevel
        );

        // MeshCollider нужен для определения провинции под курсором.
        // addMeshColliders сохраняет совместимость с прежним параметром,
        // но при включённом выборе коллайдер создаётся в любом случае.
        if (addMeshColliders || enableProvinceSelection)
        {
            regionObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        if (enableProvinceSelection)
            CreateSelectionOverlay(regionObject, mesh);


        GameObject selectedPrefab = isCapital
            ? capitalPrefab
            : cityPrefab;

        if (selectedPrefab != null)
        {
            GameObject city = Instantiate(
                selectedPrefab,
                regionObject.transform
            );

            CityController prefabController =
                selectedPrefab.GetComponent<CityController>();

            float requiredBorderDistance = prefabController != null
                ? prefabController.BorderDistance
                : 0f;

            Vector2 selectedCityPosition = FindCityPosition(
                region,
                requiredBorderDistance,
                index
            );

            Vector3 localPosition = new Vector3(
                selectedCityPosition.x - region.center.x,
                0f,
                selectedCityPosition.y - region.center.y
            );

            CityController controller =
                city.GetComponent<CityController>();

            if (controller != null)
            {
                controller.Initialize(
                    localPosition,
                    seed,
                    index,
                    isCapital
                );

                // Привязываем созданный город к провинции.
                provinceController.SetCity(controller);
            }
            else
            {
                // Резервное размещение, если компонент не добавлен.
                Debug.LogWarning(
                    $"На префабе '{selectedPrefab.name}' " +
                    "отсутствует CityController."
                );

                city.transform.localPosition = localPosition;

                city.name = isCapital
                    ? $"Capital_{index + 1:000}"
                    : $"City_{index + 1:000}";
            }
        }
        else if (isCapital)
        {
            Debug.LogWarning(
                "Не назначен Capital Prefab в PoliticalMapGenerator."
            );
        }

    }

    /// <summary>
    /// Рассчитывает приблизительное расстояние от центра каждой ячейки
    /// до ближайшей границы её провинции.
    /// </summary>
    private void CalculateCityBorderClearances(
        int[,] ownership,
        RegionData[] regions,
        float cellWidth,
        float cellHeight)
    {
        int width = ownership.GetLength(0);
        int height = ownership.GetLength(1);

        float[,] distances = new float[width, height];
        float infinity = mapWidth + mapHeight + 1f;

        // Ячейки на границе провинции получают начальное расстояние 0.
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int current = ownership[x, z];

                bool isBoundary =
                    x == 0 || z == 0 ||
                    x == width - 1 || z == height - 1;

                if (!isBoundary)
                {
                    isBoundary =
                        ownership[x - 1, z] != current ||
                        ownership[x + 1, z] != current ||
                        ownership[x, z - 1] != current ||
                        ownership[x, z + 1] != current;
                }

                distances[x, z] = isBoundary ? 0f : infinity;
            }
        }

        float diagonal = Mathf.Sqrt(
            cellWidth * cellWidth + cellHeight * cellHeight
        );

        // Прямой проход.
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = distances[x, z];

                if (x > 0)
                    distance = Mathf.Min(distance, distances[x - 1, z] + cellWidth);

                if (z > 0)
                    distance = Mathf.Min(distance, distances[x, z - 1] + cellHeight);

                if (x > 0 && z > 0)
                    distance = Mathf.Min(distance, distances[x - 1, z - 1] + diagonal);

                if (x < width - 1 && z > 0)
                    distance = Mathf.Min(distance, distances[x + 1, z - 1] + diagonal);

                distances[x, z] = distance;
            }
        }

        // Обратный проход.
        for (int z = height - 1; z >= 0; z--)
        {
            for (int x = width - 1; x >= 0; x--)
            {
                float distance = distances[x, z];

                if (x < width - 1)
                    distance = Mathf.Min(distance, distances[x + 1, z] + cellWidth);

                if (z < height - 1)
                    distance = Mathf.Min(distance, distances[x, z + 1] + cellHeight);

                if (x < width - 1 && z < height - 1)
                    distance = Mathf.Min(distance, distances[x + 1, z + 1] + diagonal);

                if (x > 0 && z < height - 1)
                    distance = Mathf.Min(distance, distances[x - 1, z + 1] + diagonal);

                distances[x, z] = distance;
            }
        }

        // Уменьшаем расстояние на половину диагонали ячейки:
        // так оценка отступа получается консервативной.
        float halfCellDiagonal = diagonal * 0.5f;
        int[] candidateCursors = new int[regions.Length];

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int regionIndex = ownership[x, z];
                int candidateIndex = candidateCursors[regionIndex]++;

                float clearance = Mathf.Max(
                    0f,
                    distances[x, z] - halfCellDiagonal
                );

                regions[regionIndex]
                    .cityCandidates[candidateIndex]
                    .borderClearance = clearance;
            }
        }
    }

    /// <summary>
    /// Выбирает ячейку, удовлетворяющую отступу от границ.
    /// Из подходящих ячеек берётся наиболее близкая к центру провинции.
    /// Если отступ недостижим, выбирается наиболее безопасная ячейка.
    /// </summary>
    private Vector2 FindCityPosition(
        RegionData region,
        float requiredBorderDistance,
        int regionIndex)
    {
        CityCandidate bestCandidate = null;
        CityCandidate bestMeetingRequirement = null;

        foreach (CityCandidate candidate in region.cityCandidates)
        {
            if (bestCandidate == null ||
                candidate.borderClearance > bestCandidate.borderClearance ||
                (Mathf.Approximately(
                    candidate.borderClearance,
                    bestCandidate.borderClearance) &&
                 candidate.centerDistance < bestCandidate.centerDistance))
            {
                bestCandidate = candidate;
            }

            if (candidate.borderClearance + 0.0001f >= requiredBorderDistance)
            {
                if (bestMeetingRequirement == null ||
                    candidate.borderClearance > bestMeetingRequirement.borderClearance ||
                    (Mathf.Approximately(
                        candidate.borderClearance,
                        bestMeetingRequirement.borderClearance) &&
                     candidate.centerDistance < bestMeetingRequirement.centerDistance))
                {
                    bestMeetingRequirement = candidate;
                }
            }
        }

        if (bestMeetingRequirement != null)
            return bestMeetingRequirement.position;

        if (bestCandidate == null)
        {
            Debug.LogWarning(
                $"Для провинции {regionIndex + 1} не найдены ячейки для города."
            );
            return region.center;
        }

        Debug.LogWarning(
            $"Провинция {regionIndex + 1}: невозможно обеспечить отступ " +
            $"{requiredBorderDistance:F2}. Максимально доступный отступ: " +
            $"{bestCandidate.borderClearance:F2}. Город размещён в наиболее " +
            "удалённой от границ точке."
        );

        return bestCandidate.position;
    }

    private void CreateBorders(
        int[,] ownership,
        float cellWidth,
        float cellHeight)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        int width = ownership.GetLength(0);
        int height = ownership.GetLength(1);

        // Границы между регионами.
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int current = ownership[x, z];

                if (x + 1 < width &&
                    current != ownership[x + 1, z])
                {
                    AddBorderQuad(
                        vertices, triangles,
                        new Vector3((x + 1) * cellWidth, borderHeight,
                            z * cellHeight),
                        new Vector3((x + 1) * cellWidth, borderHeight,
                            (z + 1) * cellHeight)
                    );
                }

                if (z + 1 < height &&
                    current != ownership[x, z + 1])
                {
                    AddBorderQuad(
                        vertices, triangles,
                        new Vector3(x * cellWidth, borderHeight,
                            (z + 1) * cellHeight),
                        new Vector3((x + 1) * cellWidth, borderHeight,
                            (z + 1) * cellHeight)
                    );
                }
            }
        }

        // Внешняя рамка карты.
        if (drawOuterBorder)
        {
            AddBorderQuad(vertices, triangles,
                new Vector3(0f, borderHeight, 0f),
                new Vector3(mapWidth, borderHeight, 0f));

            AddBorderQuad(vertices, triangles,
                new Vector3(mapWidth, borderHeight, 0f),
                new Vector3(mapWidth, borderHeight, mapHeight));

            AddBorderQuad(vertices, triangles,
                new Vector3(mapWidth, borderHeight, mapHeight),
                new Vector3(0f, borderHeight, mapHeight));

            AddBorderQuad(vertices, triangles,
                new Vector3(0f, borderHeight, mapHeight),
                new Vector3(0f, borderHeight, 0f));
        }

        GameObject borderObject = new GameObject("ProvinceBorders");
        borderObject.transform.SetParent(transform, false);
        borderObject.transform.localPosition = Vector3.zero;

        Mesh mesh = new Mesh { name = "ProvinceBordersMesh" };

        if (vertices.Count > 65535)
            mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        borderObject.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer renderer =
            borderObject.AddComponent<MeshRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", borderColor);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", borderColor);

        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void AddBorderQuad(
        List<Vector3> vertices,
        List<int> triangles,
        Vector3 start,
        Vector3 end)
    {
        Vector3 direction = end - start;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.000001f)
            return;

        direction.Normalize();

        Vector3 perpendicular = new Vector3(
            -direction.z, 0f, direction.x
        ) * (borderThickness * 0.5f);

        int index = vertices.Count;

        vertices.Add(start - perpendicular);
        vertices.Add(start + perpendicular);
        vertices.Add(end + perpendicular);
        vertices.Add(end - perpendicular);

        triangles.Add(index);
        triangles.Add(index + 1);
        triangles.Add(index + 2);

        triangles.Add(index);
        triangles.Add(index + 2);
        triangles.Add(index + 3);
    }

    [ContextMenu("Clear Political Map")]
    public void ClearMap()
    {
        if (playerManager != null)
        {
            playerManager.ClearPlayerState();
        }

        if (relationManager == null)
            relationManager = GetComponent<RelationManager>();

        if (relationManager != null)
            relationManager.ClearRelations();

        ProvinceController.ClearSelection();
        selectedProvince = null;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void OnDestroy()
    {
        if (fallbackSelectionMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(fallbackSelectionMaterial);
        else
            DestroyImmediate(fallbackSelectionMaterial);
    }
}
