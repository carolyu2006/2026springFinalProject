using UnityEngine;

public class ScreenBoundsUtility : MonoBehaviour
{
    public static ScreenBoundsUtility Instance { get; private set; }

    [Header("Play Area Corners (X = world X, Y = world Z)")]
    [SerializeField] private Vector2 bottomLeft = new Vector2(-10f, -6f);
    [SerializeField] private Vector2 bottomRight = new Vector2(10f, -6f);
    [SerializeField] private Vector2 topLeft = new Vector2(-10f, 6f);
    [SerializeField] private Vector2 topRight = new Vector2(10f, 6f);

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Clamp a position inside the quadrilateral defined by the 4 corners.
    /// </summary>
    public static Vector3 ClampToVisibleWorld(Vector3 position)
    {
        if (Instance == null) return position;

        Vector2 p = new Vector2(position.x, position.z);
        Vector2 bl = Instance.bottomLeft;
        Vector2 br = Instance.bottomRight;
        Vector2 tl = Instance.topLeft;
        Vector2 tr = Instance.topRight;

        if (IsInsideQuad(p, bl, br, tr, tl))
            return position;

        // Find closest point on the quad edges
        Vector2 closest = ClosestPointOnQuadEdges(p, bl, br, tr, tl);
        position.x = closest.x;
        position.z = closest.y;
        return position;
    }

    /// <summary>
    /// Random point inside the quadrilateral using bilinear interpolation.
    /// </summary>
    public static Vector3 GetRandomPointInsideVisibleWorld(float worldY)
    {
        if (Instance == null) return new Vector3(0f, worldY, 0f);

        Vector2 bl = Instance.bottomLeft;
        Vector2 br = Instance.bottomRight;
        Vector2 tl = Instance.topLeft;
        Vector2 tr = Instance.topRight;

        // Bilinear interpolation across the quad
        float u = Random.Range(0f, 1f);
        float v = Random.Range(0f, 1f);

        Vector2 bottom = Vector2.Lerp(bl, br, u);
        Vector2 top = Vector2.Lerp(tl, tr, u);
        Vector2 point = Vector2.Lerp(bottom, top, v);

        return new Vector3(point.x, worldY, point.y);
    }

    private static bool IsInsideQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        // Check if point is inside quad ABCD by testing two triangles: ABC and ACD
        return IsInsideTriangle(p, a, b, c) || IsInsideTriangle(p, a, c, d);
    }

    private static bool IsInsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a);
        float d2 = Cross(c - b, p - b);
        float d3 = Cross(a - c, p - c);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private static Vector2 ClosestPointOnQuadEdges(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        Vector2 closest = ClosestPointOnSegment(p, a, b);
        float bestDist = (p - closest).sqrMagnitude;

        Vector2[] edges = { ClosestPointOnSegment(p, b, c), ClosestPointOnSegment(p, c, d), ClosestPointOnSegment(p, d, a) };
        foreach (Vector2 candidate in edges)
        {
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
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return a + ab * t;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 bl3 = new Vector3(bottomLeft.x, 0f, bottomLeft.y);
        Vector3 br3 = new Vector3(bottomRight.x, 0f, bottomRight.y);
        Vector3 tl3 = new Vector3(topLeft.x, 0f, topLeft.y);
        Vector3 tr3 = new Vector3(topRight.x, 0f, topRight.y);

        Gizmos.DrawLine(bl3, br3);
        Gizmos.DrawLine(br3, tr3);
        Gizmos.DrawLine(tr3, tl3);
        Gizmos.DrawLine(tl3, bl3);
    }
}
