using System;
using System.Collections.Generic;
using UnityEngine;

public enum RulerTitle
{
    Lord,
    Tsar,
    King,
    Emperor,
    Overlord,
    Tyrant
}

[Serializable]
public class RulerData
{
    public int countryId;
    public RulerTitle title;
    public string rulerName;
    public Color color;
    public int holdingsCount = 0;

    // ”никальный портрет правител€, созданный из спрайтшита.
    public Sprite portrait;
}

public class RulerDatabase : MonoBehaviour
{
    private const int NamePoolSize = 30;

    [Header("Ruler Portraits")]
    [Tooltip("“екстура со спрайтшитом портретов. ќжидаетс€ сетка 5x5.")]
    [SerializeField] private Texture2D portraitSpriteSheet;

    [Tooltip("ѕортрет-заглушка дл€ правителей, которым не хватило уникальных портретов.")]
    [SerializeField] private Sprite placeholderPortrait;

    [Min(1)]
    [SerializeField] private int portraitColumns = 5;

    [Min(1)]
    [SerializeField] private int portraitRows = 5;

    // ‘иксированный набор имЄн.  аждое им€ используетс€ не более одного раза
    // в рамках одной генерации карты.
    private readonly string[] allNames =
    {
        "Aldric", "Baldwyn", "Cedric", "Darian", "Edric",
        "Faelan", "Garrick", "Hadrian", "Isen", "Jorvan",
        "Kaelen", "Leofric", "Maelor", "Nerian", "Ormund",
        "Percival", "Quorin", "Roderic", "Soren", "Theron",
        "Ulric", "Valerian", "Wystan", "Xavian", "Yorick",
        "Zephan", "Arveth", "Belisar", "Corvyn", "Demeric"
    };

    [SerializeField]
    private List<RulerData> rulers = new List<RulerData>();

    private List<string> availableNames = new List<string>();
    private List<int> availablePortraitIndices = new List<int>();
    private readonly List<Sprite> createdPortraitSprites = new List<Sprite>();

    public IReadOnlyList<RulerData> Rulers => rulers;

    private void Awake()
    {
        ResetAvailableNames();
        ResetAvailablePortraits();
    }

    public void ClearDatabase()
    {
        rulers.Clear();
        ResetAvailableNames();
        ResetAvailablePortraits();
        DestroyCreatedPortraitSprites();
    }

    public void AddRuler(
        int countryId,
        Color countryColor,
        int holdingsCount,
        System.Random random)
    {
        if (random == null)
        {
            Debug.LogError(
                "RulerDatabase: дл€ назначени€ имени и портрета передан null вместо System.Random.",
                this
            );
            return;
        }

        if (availableNames == null || availableNames.Count == 0)
        {
            Debug.LogWarning(
                $"RulerDatabase: все {NamePoolSize} имЄн уже использованы. " +
                $"ѕравитель дл€ государства {countryId} не был добавлен. " +
                "”величьте список имЄн, если на карте может быть больше 30 государств.",
                this
            );
            return;
        }

        int nameIndex = random.Next(availableNames.Count);
        string selectedName = availableNames[nameIndex];
        availableNames.RemoveAt(nameIndex);

        RulerTitle[] titles = (RulerTitle[])Enum.GetValues(typeof(RulerTitle));
        RulerTitle selectedTitle = titles[random.Next(titles.Length)];

        Sprite selectedPortrait = GetRandomUniquePortrait(random);

        RulerData ruler = new RulerData
        {
            countryId = countryId,
            title = selectedTitle,
            rulerName = selectedName,
            color = countryColor,
            holdingsCount = holdingsCount,
            portrait = selectedPortrait
        };

        rulers.Add(ruler);
    }

    public RulerData GetRulerByCountryId(int countryId)
    {
        return rulers.Find(ruler => ruler.countryId == countryId);
    }

    private Sprite GetRandomUniquePortrait(System.Random random)
    {
        if (portraitSpriteSheet == null)
        {
            Debug.LogWarning(
                "RulerDatabase: спрайтшит портретов не назначен. " +
                "ѕравитель будет создан без портрета.",
                this
            );
            return null;
        }

        if (availablePortraitIndices == null || availablePortraitIndices.Count == 0)
        {
            if (placeholderPortrait == null)
            {
                Debug.LogError(
                    "RulerDatabase: все уникальные портреты использованы, " +
                    "но не назначен Placeholder Portrait. Ќазначьте портрет-заглушку в Inspector.",
                    this
                );
                return null;
            }

            // «аглушка может повтор€тьс€: это общий портрет дл€ правителей,
            // которым не досталс€ уникальный портрет из спрайтшита.
            return placeholderPortrait;
        }

        int listIndex = random.Next(availablePortraitIndices.Count);
        int portraitIndex = availablePortraitIndices[listIndex];
        availablePortraitIndices.RemoveAt(listIndex);

        int column = portraitIndex % portraitColumns;
        int rowFromTop = portraitIndex / portraitColumns;

        int cellWidth = portraitSpriteSheet.width / portraitColumns;
        int cellHeight = portraitSpriteSheet.height / portraitRows;

        // Unity использует начало координат текстуры в левом нижнем углу.
        int x = column * cellWidth;
        int y = portraitSpriteSheet.height - (rowFromTop + 1) * cellHeight;

        Rect rect = new Rect(x, y, cellWidth, cellHeight);

        Sprite sprite = Sprite.Create(
            portraitSpriteSheet,
            rect,
            new Vector2(0.5f, 0.5f),
            100f
        );

        sprite.name = $"RulerPortrait_{portraitIndex + 1:00}";
        createdPortraitSprites.Add(sprite);

        return sprite;
    }

    private void ResetAvailableNames()
    {
        availableNames = new List<string>(allNames);
    }

    private void ResetAvailablePortraits()
    {
        availablePortraitIndices = new List<int>();

        int totalPortraits = portraitColumns * portraitRows;

        for (int i = 0; i < totalPortraits; i++)
            availablePortraitIndices.Add(i);
    }

    private void DestroyCreatedPortraitSprites()
    {
        foreach (Sprite sprite in createdPortraitSprites)
        {
            if (sprite == null)
                continue;

            if (Application.isPlaying)
                Destroy(sprite);
            else
                DestroyImmediate(sprite);
        }

        createdPortraitSprites.Clear();
    }

    private void OnDestroy()
    {
        DestroyCreatedPortraitSprites();
    }
}
