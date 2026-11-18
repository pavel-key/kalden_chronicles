
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ProvinceManagementSceneManager : MonoBehaviour
{
    public static ProvinceManagementSceneManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerManager playerManager;

    [Header("Scene Settings")]
    [SerializeField] private string managementSceneName = "ProvinceManagementScene";

    private bool isLoading;
    private bool isLoaded;
    private bool closeRequested;

    private Coroutine sceneCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "Обнаружено несколько ProvinceManagementSceneManager.",
                this
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Открывает экран управления, если провинция принадлежит игроку.
    /// </summary>
    public void OpenForProvince(ProvinceController province)
    {
        if (province == null)
            return;

        if (playerManager == null)
        {
            Debug.LogError(
                "ProvinceManagementSceneManager: не назначен PlayerManager.",
                this
            );
            return;
        }

        if (!playerManager.IsPlayerCountry(province))
            return;

        Open();
    }

    /// <summary>
    /// Загружает сцену управления поверх текущей.
    /// </summary>
    public void Open()
    {
        if (isLoading || isLoaded)
            return;

        closeRequested = false;

        sceneCoroutine = StartCoroutine(LoadManagementScene());
    }

    private IEnumerator LoadManagementScene()
    {
        isLoading = true;

        AsyncOperation operation = SceneManager.LoadSceneAsync(
            managementSceneName,
            LoadSceneMode.Additive
        );

        if (operation == null)
        {
            Debug.LogError(
                $"Не удалось начать загрузку сцены {managementSceneName}.",
                this
            );

            isLoading = false;
            yield break;
        }

        yield return operation;

        isLoading = false;
        isLoaded = true;

        // Если закрытие запросили ещё во время загрузки.
        if (closeRequested)
        {
            yield return UnloadManagementScene();
        }

        sceneCoroutine = null;
    }

    /// <summary>
    /// Закрывает экран управления, не затрагивая стратегическую карту.
    /// </summary>
    public void Close()
    {
        if (isLoading)
        {
            closeRequested = true;
            return;
        }

        if (!isLoaded)
            return;

        if (sceneCoroutine == null)
        {
            sceneCoroutine = StartCoroutine(UnloadManagementScene());
        }
    }

    private IEnumerator UnloadManagementScene()
    {
        isLoaded = false;

        Scene scene = SceneManager.GetSceneByName(managementSceneName);

        if (scene.IsValid() && scene.isLoaded)
        {
            AsyncOperation operation =
                SceneManager.UnloadSceneAsync(managementSceneName);

            if (operation != null)
                yield return operation;
        }

        closeRequested = false;
        sceneCoroutine = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
