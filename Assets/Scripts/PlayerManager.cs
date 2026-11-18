
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [Header("Player Settings")]

    [Tooltip("Префаб короны игрока")]
    [SerializeField] private GameObject crownPrefab;

    [Tooltip("Имя объекта для размещения короны внутри префаба столицы")]
    [SerializeField] private string crownPlaceholderName = "CrownPlaceholder";

    private GameObject currentCrown;

    public int PlayerCountryId { get; private set; } = -1;

    public ProvinceController PlayerCapital { get; private set; }

    public bool IsInitialized => PlayerCountryId >= 0;

    /// <summary>
    /// Вызывается после завершения генерации карты.
    /// </summary>
    public void Initialize(Transform mapRoot)
    {
        ClearPlayerState();

        if (mapRoot == null)
        {
            Debug.LogError(
                "PlayerManager: не передан корень сгенерированной карты.",
                this
            );
            return;
        }

        if (crownPrefab == null)
        {
            Debug.LogError(
                "PlayerManager: не назначен Crown Prefab в Inspector.",
                this
            );
            return;
        }

        ProvinceController[] provinces =
            mapRoot.GetComponentsInChildren<ProvinceController>(true);

        // Собираем столицы государств.
        Dictionary<int, ProvinceController> capitalsByCountry =
            new Dictionary<int, ProvinceController>();

        foreach (ProvinceController province in provinces)
        {
            if (province == null || province.City == null)
                continue;

            if (!province.City.IsCapital)
                continue;

            if (!capitalsByCountry.ContainsKey(province.CountryId))
            {
                capitalsByCountry.Add(
                    province.CountryId,
                    province
                );
            }
        }

        if (capitalsByCountry.Count == 0)
        {
            Debug.LogError(
                "PlayerManager: не удалось найти столицы государств. " +
                "Проверьте Capital Prefab и CityController.",
                this
            );
            return;
        }

        // Получаем список государств, для которых найдены столицы.
        List<int> availableCountries =
            new List<int>(capitalsByCountry.Keys);

        // Случайно выбираем государство игрока.
        int randomIndex = Random.Range(
            0,
            availableCountries.Count
        );

        PlayerCountryId = availableCountries[randomIndex];

        PlayerCapital = capitalsByCountry[PlayerCountryId];

        // Устанавливаем корону над столицей.
        PlaceCrown();

        Debug.Log(
            $"PlayerManager: выбрано государство игрока ID {PlayerCountryId}. " +
            $"Столица: {PlayerCapital.name}.",
            this
        );
    }

    /// <summary>
    /// Создаёт корону дочерним объектом CrownPlaceholder.
    /// </summary>
    private void PlaceCrown()
    {
        if (PlayerCapital == null)
        {
            Debug.LogError(
                "PlayerManager: не найдена столица игрока.",
                this
            );
            return;
        }

        CityController capitalCity = PlayerCapital.City;

        if (capitalCity == null)
        {
            Debug.LogError(
                "PlayerManager: у столицы отсутствует CityController.",
                PlayerCapital
            );
            return;
        }

        Transform placeholder = FindChildRecursive(
            capitalCity.transform,
            crownPlaceholderName
        );

        if (placeholder == null)
        {
            Debug.LogError(
                $"PlayerManager: объект '{crownPlaceholderName}' " +
                $"не найден внутри префаба столицы '{capitalCity.name}'.",
                capitalCity
            );
            return;
        }

        currentCrown = Instantiate(
            crownPrefab,
            placeholder,
            false
        );

        currentCrown.name = "PlayerCrown";

        // Совмещаем корону с точкой размещения.
        currentCrown.transform.localPosition = Vector3.zero;
        currentCrown.transform.localRotation = Quaternion.identity;
        currentCrown.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Рекурсивный поиск дочернего объекта по имени.
    /// </summary>
    private Transform FindChildRecursive(
        Transform parent,
        string targetName)
    {
        if (parent == null)
            return null;

        if (parent.name == targetName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(
                parent.GetChild(i),
                targetName
            );

            if (result != null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// Проверяет, принадлежит ли провинция игроку.
    /// </summary>
    public bool IsPlayerCountry(ProvinceController province)
    {
        return province != null &&
               IsInitialized &&
               province.CountryId == PlayerCountryId;
    }

    /// <summary>
    /// Сбрасывает данные игрока и удаляет старую корону.
    /// Вызывается перед повторной генерацией карты.
    /// </summary>
    public void ClearPlayerState()
    {
        if (currentCrown != null)
        {
            if (Application.isPlaying)
                Destroy(currentCrown);
            else
                DestroyImmediate(currentCrown);
        }

        currentCrown = null;
        PlayerCountryId = -1;
        PlayerCapital = null;
    }
}
