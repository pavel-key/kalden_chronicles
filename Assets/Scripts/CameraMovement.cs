
using System.Collections;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;

    [Header("Capital Flight")]
    [Tooltip("Продолжительность перелёта к столице в секундах")]
    [SerializeField, Min(0.1f)]
    private float capitalFlightDuration = 2.5f;

    [Tooltip("Кривая ускорения и замедления камеры")]
    [SerializeField]
    private AnimationCurve flightCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine capitalFlightCoroutine;
    private bool isFlyingToCapital;

    private void Update()
    {
        // Во время автоматического перелёта
        // ручное управление временно отключено.
        if (isFlyingToCapital)
            return;

        MoveWithKeyboard();
    }

    /// <summary>
    /// Запускает плавный перелёт камеры к столице игрока.
    /// Сохраняет текущую высоту и поворот камеры.
    /// </summary>
    public void MoveToMyCapital(PlayerManager playerManager)
    {
        if (playerManager == null)
        {
            Debug.LogWarning(
                "CameraMovement: PlayerManager не назначен.",
                this
            );
            return;
        }

        if (!playerManager.IsInitialized ||
            playerManager.PlayerCapital == null)
        {
            Debug.LogWarning(
                "CameraMovement: столица игрока ещё не определена.",
                this
            );
            return;
        }

        Vector3 capitalPosition =
            playerManager.PlayerCapital.CityPosition;

        // Сохраняем текущую высоту камеры.
        Vector3 targetPosition = new Vector3(
            capitalPosition.x,
            transform.position.y,
            capitalPosition.z - 30f
        );

        // Прерываем предыдущий перелёт, если он ещё выполняется.
        if (capitalFlightCoroutine != null)
        {
            StopCoroutine(capitalFlightCoroutine);
            capitalFlightCoroutine = null;
        }

        capitalFlightCoroutine = StartCoroutine(
            FlyToPosition(targetPosition)
        );
    }

    /// <summary>
    /// Плавное перемещение камеры к заданной позиции.
    /// </summary>
    private IEnumerator FlyToPosition(Vector3 targetPosition)
    {
        isFlyingToCapital = true;

        Vector3 startPosition = transform.position;
        float elapsedTime = 0f;

        float duration = Mathf.Max(0.1f, capitalFlightDuration);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(
                elapsedTime / duration
            );

            float curveValue = flightCurve.Evaluate(normalizedTime);

            transform.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                curveValue
            );

            yield return null;
        }

        // Гарантируем точное конечное положение.
        transform.position = targetPosition;

        isFlyingToCapital = false;
        capitalFlightCoroutine = null;
    }

    /// <summary>
    /// Перемещение камеры с помощью WASD.
    /// </summary>
    private void MoveWithKeyboard()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        // Движение только по горизонтальной плоскости.
        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 movement =
            forward * vertical +
            right * horizontal;

        movement = Vector3.ClampMagnitude(movement, 1f);

        transform.position +=
            movement * moveSpeed * Time.deltaTime;
    }

    private void OnDisable()
    {
        if (capitalFlightCoroutine != null)
        {
            StopCoroutine(capitalFlightCoroutine);
            capitalFlightCoroutine = null;
        }

        isFlyingToCapital = false;
    }
}
