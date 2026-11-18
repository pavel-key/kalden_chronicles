using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TradeConnectionData
{
    public ProvinceController ProvinceA { get; private set; }
    public ProvinceController ProvinceB { get; private set; }

    public List<ResourceType> ResourcesAtoB { get; private set; }
    public List<ResourceType> ResourcesBtoA { get; private set; }

    public bool IsMutual =>
        ResourcesAtoB.Count > 0 &&
        ResourcesBtoA.Count > 0;

    public TradeConnectionData(
        ProvinceController provinceA,
        ProvinceController provinceB,
        List<ResourceType> resourcesAtoB,
        List<ResourceType> resourcesBtoA)
    {
        ProvinceA = provinceA;
        ProvinceB = provinceB;

        ResourcesAtoB = new List<ResourceType>(resourcesAtoB);
        ResourcesBtoA = new List<ResourceType>(resourcesBtoA);
    }
}

[DisallowMultipleComponent]
public class RelationManager : MonoBehaviour
{
    [Header("Trade Rules")]
    [Range(0f, 1f)]
    [SerializeField] private float relationProbability = 0.25f;

    [SerializeField, Range(0, 255)]
    private int surplusThreshold = 200;

    [Header("Arrow Appearance")]
    [SerializeField]
    private Color arrowColor =
        new Color(0f, 1f, 0.1f, 1f);

    [Min(0.001f)]
    [SerializeField] private float lineWidth = 2f;

    [Min(0f)]
    [SerializeField] private float arcHeight = 2f;

    [Header("Resource Icons")]
    [Tooltip("Порядок: Wood, Stone, Iron, Gold, Grain, MoonCrystal, Coal.")]
    [SerializeField] private Sprite[] resourceIcons = new Sprite[7];

    [Header("References")]
    [Tooltip("Необязательный родитель для объектов стрелок.")]
    [SerializeField] private Transform arrowsRoot;

    private readonly List<RelationArrow> arrows =
        new List<RelationArrow>();

    private readonly List<TradeConnectionData> connections =
        new List<TradeConnectionData>();

    private readonly HashSet<ulong> processedPairs =
        new HashSet<ulong>();

    private System.Random random;

    public int ArrowCount => arrows.Count;
    public float RelationProbability => relationProbability;
    public int SurplusThreshold => surplusThreshold;

    public IReadOnlyList<TradeConnectionData> Connections =>
        connections;

    public void ClearRelations()
    {
        for (int i = arrows.Count - 1; i >= 0; i--)
        {
            if (arrows[i] == null)
                continue;

            GameObject arrowObject = arrows[i].gameObject;

            if (Application.isPlaying)
                Destroy(arrowObject);
            else
                DestroyImmediate(arrowObject);
        }

        arrows.Clear();
        connections.Clear();
        processedPairs.Clear();
    }

    public bool TryCreateRelation(
        ProvinceController from,
        ProvinceController to)
    {
        if (from == null || to == null || from == to)
            return false;

        if (from.CountryId == to.CountryId)
            return false;

        if (from.CityTransform == null || to.CityTransform == null)
            return false;

        ulong pairKey = MakePairKey(
            from.GetInstanceID(),
            to.GetInstanceID()
        );

        if (!processedPairs.Add(pairKey))
            return false;

        List<ResourceType> fromSurplus =
            GetSurplusResources(from);

        List<ResourceType> toSurplus =
            GetSurplusResources(to);

        if (fromSurplus.Count == 0 &&
            toSurplus.Count == 0)
        {
            return false;
        }

        RemoveCommonResources(
            fromSurplus,
            toSurplus
        );

        if (fromSurplus.Count == 0 &&
            toSurplus.Count == 0)
        {
            return false;
        }

        if (random == null)
            random = new System.Random();

        if (random.NextDouble() >= relationProbability)
            return false;

        TradeConnectionData connection =
            new TradeConnectionData(
                from,
                to,
                fromSurplus,
                toSurplus
            );

        connections.Add(connection);

        ProvinceController visualFrom =
            fromSurplus.Count > 0 ? from : to;

        ProvinceController visualTo =
            fromSurplus.Count > 0 ? to : from;

        // Ресурсы пересчитываются относительно направления линии.
        IReadOnlyList<ResourceType> forwardResources =
            visualFrom == from ? fromSurplus : toSurplus;

        IReadOnlyList<ResourceType> backwardResources =
            visualFrom == from ? toSurplus : fromSurplus;

        CreateArrow(
            visualFrom,
            visualTo,
            forwardResources,
            backwardResources
        );

        return true;
    }

    private List<ResourceType> GetSurplusResources(
        ProvinceController province)
    {
        List<ResourceType> result =
            new List<ResourceType>();

        Array values = Enum.GetValues(typeof(ResourceType));

        foreach (ResourceType resource in values)
        {
            if (province.GetResourceLevel(resource) >
                surplusThreshold)
            {
                result.Add(resource);
            }
        }

        return result;
    }

    private static void RemoveCommonResources(
        List<ResourceType> first,
        List<ResourceType> second)
    {
        for (int i = first.Count - 1; i >= 0; i--)
        {
            if (second.Contains(first[i]))
            {
                ResourceType commonResource = first[i];

                first.RemoveAt(i);
                second.Remove(commonResource);
            }
        }
    }

    private void CreateArrow(
        ProvinceController from,
        ProvinceController to,
        IReadOnlyList<ResourceType> forwardResources,
        IReadOnlyList<ResourceType> backwardResources)
    {
        Transform parent =
            arrowsRoot != null ? arrowsRoot : transform;

        GameObject arrowObject = new GameObject(
            $"Trade_{from.ProvinceId}_{to.ProvinceId}"
        );

        arrowObject.transform.SetParent(parent, false);

        RelationArrow arrow =
            arrowObject.AddComponent<RelationArrow>();

        arrow.Initialize(
            from.CityTransform,
            to.CityTransform,
            arrowColor,
            lineWidth,
            arcHeight,
            forwardResources,
            backwardResources,
            resourceIcons
        );

        arrows.Add(arrow);
    }

    public void SetRandomSeed(int seed)
    {
        random = new System.Random(seed);
    }

    private static ulong MakePairKey(
        int first,
        int second)
    {
        uint a = (uint)Mathf.Min(first, second);
        uint b = (uint)Mathf.Max(first, second);

        return ((ulong)a << 32) | b;
    }
}