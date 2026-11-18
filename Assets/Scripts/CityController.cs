
using UnityEngine;

public class CityController : MonoBehaviour
{
    [Header("Placement")]

    [Tooltip("¬ысота города над поверхностью провинции")]
    [SerializeField] private float heightOffset = 0.1f;

    [Tooltip("—лучайный поворот города вокруг вертикальной оси")]
    [SerializeField] private bool randomRotation = true;

    [Header("Border Distance")]

    [Tooltip("ћинимальный отступ города от границы провинции в единицах Unity. " +
             "√енератор использует это значение при выборе €чейки.")]
    [SerializeField, Min(0f)]
    private float borderDistance = 2f;

    [Header("City Information")]

    [SerializeField] private bool isCapital;

    public float BorderDistance => borderDistance;

    /// <summary>
    /// ѕоказывает, €вл€етс€ ли этот город столицей государства.
    /// </summary>
    public bool IsCapital => isCapital;

    /// <summary>
    /// ¬ызываетс€ генератором после создани€ города.
    /// localPosition Ч положение относительно родительской провинции.
    /// </summary>
    public void Initialize(
        Vector3 localPosition,
        int mapSeed,
        int regionIndex,
        bool isCapital)
    {
        this.isCapital = isCapital;

        transform.localPosition = new Vector3(
            localPosition.x,
            heightOffset,
            localPosition.z
        );

        float rotationY = 0f;

        if (randomRotation)
        {
            rotationY = Mathf.Repeat(
                mapSeed + regionIndex * 137.5f,
                360f
            );
        }

        transform.localRotation = Quaternion.Euler(
            0f,
            rotationY,
            0f
        );

        gameObject.name = isCapital
            ? $"Capital_{regionIndex + 1:000}"
            : $"City_{regionIndex + 1:000}";
    }
}
