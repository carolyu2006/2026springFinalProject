using UnityEngine;

public class ScreenBoundsUtility : MonoBehaviour
{
    public static ScreenBoundsUtility Instance { get; private set; }

    [Header("Play Area Vertices (X = world X, Y = world Z), ordered around the polygon")]
    [SerializeField] private Vector2[] vertices = new Vector2[]
    {
        new Vector2(-19.8f, -12.92f),
        new Vector2(19.31f, -11.16f),
        new Vector2(17f, 14.1f),
        new Vector2(0f, 18f),
        new Vector2(-20f, 15.6f),
    };

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Clamp a position inside the polygon defined by <see cref="vertices"/>.
    /// </summary>
    public static Vector3 ClampToVisibleWorld(Vector3 position)
    {
        if (Instance == null || Instance.vertices == null || Instance.vertices.Length < 3)
            return position;

        Vector2[] v = Instance.vertices;
        Vector2 p = new Vector2(position.x, position.z);

        if (IsInsidePolygon(p, v))
            return position;

        Vector2 closest = ClosestPointOnPolygonEdges(p, v);
        position.x = closest.x;
        position.z = closest.y;
        return position;
    }

    /// <summary>
    /// Random point inside the polygon, using rejection sampling on its axis-aligned bounding box.
    /// </summary>
    public static Vector3 GetRandomPointInsideVisibleWorld(float worldY)
    {
        if (Instance == null || Instance.vertices == null || Instance.vertices.Length < 3)
            return new Vector3(0f, worldY, 0f);

        Vector2[] v = Instance.vertices;

        float minX = v[0].x, maxX = v[0].x, minY = v[0].y, maxY = v[0].y;
        for (int i = 1; i < v.Length; i++)
        {
            if (v[i].x < minX) minX = v[i].x;
            if (v[i].x > maxX) maxX = v[i].x;
            if (v[i].y < minY) minY = v[i].y;
            if (v[i].y > maxY) maxY = v[i].y;
        }

        for (int attempt = 0; attempt < 64; attempt++)
        {
            Vector2 candidate = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
            if (IsInsidePolygon(candidate, v))
                return new Vector3(candidate.x, worldY, candidate.y);
        }

        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < v.Length; i++) centroid += v[i];
        centroid /= v.Length;
        return new Vector3(centroid.x, worldY, centroid.y);
    }

    private static bool IsInsidePolygon(Vector2 p, Vector2[] v)
    {
        bool inside = false;
        int n = v.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 vi = v[i];
            Vector2 vj = v[j];
            if (((vi.y > p.y) != (vj.y > p.y)) &&
                (p.x < (vj.x - vi.x) * (p.y - vi.y) / (vj.y - vi.y) + vi.x))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static Vector2 ClosestPointOnPolygonEdges(Vector2 p, Vector2[] v)
    {
        int n = v.Length;
        Vector2 closest = ClosestPointOnSegment(p, v[n - 1], v[0]);
        float bestDist = (p - closest).sqrMagnitude;

        for (int i = 0; i < n - 1; i++)
        {
            Vector2 candidate = ClosestPointOnSegment(p, v[i], v[i + 1]);
            float dist = (p - candidate).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                closest = candidate;
            }
        }

        return closest;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-8f) return a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        return a + ab * t;
    }

    private void OnDrawGizmos()
    {
        if (vertices == null || vertices.Length < 2) return;

        Gizmos.color = Color.green;
        int n = vertices.Length;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = vertices[i];
            Vector2 b = vertices[(i + 1) % n];
            Gizmos.DrawLine(new Vector3(a.x, 0f, a.y), new Vector3(b.x, 0f, b.y));
        }
    }
}
