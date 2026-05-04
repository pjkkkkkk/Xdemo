using UnityEngine;

public sealed class StoreFloatingText : MonoBehaviour
{
    [SerializeField] private Vector2 m_FloatAmplitude = new Vector2(0.025f, 0.035f);
    [SerializeField, Range(0.1f, 12f)] private float m_FloatSpeed = 2.2f;
    [SerializeField, Range(0f, 0.08f)] private float m_JitterAmount = 0.009f;
    [SerializeField, Range(0.1f, 30f)] private float m_JitterSpeed = 8.5f;

    private Vector3 m_BaseLocalPosition;
    private float m_Seed;

    private void Awake()
    {
        m_BaseLocalPosition = transform.localPosition;
        Vector3 position = transform.position;
        m_Seed = Mathf.Abs(position.x * 12.9898f + position.y * 78.233f + position.z * 37.719f);
    }

    private void OnEnable()
    {
        m_BaseLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        float time = Time.unscaledTime;
        float floatX = Mathf.Sin(time * m_FloatSpeed + m_Seed) * m_FloatAmplitude.x;
        float floatY = Mathf.Cos(time * (m_FloatSpeed * 0.83f) + m_Seed) * m_FloatAmplitude.y;
        float jitterX = (Mathf.PerlinNoise(m_Seed, time * m_JitterSpeed) - 0.5f) * m_JitterAmount;
        float jitterY = (Mathf.PerlinNoise(time * m_JitterSpeed, m_Seed) - 0.5f) * m_JitterAmount;

        transform.localPosition = m_BaseLocalPosition + new Vector3(floatX + jitterX, floatY + jitterY, 0f);
    }
}
