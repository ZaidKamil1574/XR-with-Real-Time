using UnityEngine;

/// Attach to an empty GameObject. It generates a cylinder-arc mesh you can
/// use as a giant screen (normals point inward so the seats see it).
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ArcScreenGenerator : MonoBehaviour
{
    [Header("Geometry")]
    [Min(0.1f)] public float radius = 45f;        // meters from center to screen
    [Range(5, 360)] public float arcDegrees = 120f;
    [Min(0.1f)] public float height = 18f;
    [Range(8, 256)] public int segments = 96;     // smoothness along the arc
    [Tooltip("Make the mesh double sided (back face too)")]
    public bool doubleSided = false;

    [Header("UV / Aspect")]
    [Tooltip("Keeps UV-V scaled to ~16:9 relative to arc width")]
    public bool keepAspect16x9 = true;
    [Tooltip("Flip horizontally if your video appears mirrored")]
    public bool flipUVHoriz = false;

    MeshFilter mf; MeshRenderer mr;

    [ContextMenu("Rebuild Screen")]
    public void Rebuild()
    {
        if (!mf) mf = GetComponent<MeshFilter>();
        if (!mr) mr = GetComponent<MeshRenderer>();

        var mesh = BuildArcMesh(radius, height, arcDegrees, segments, doubleSided, keepAspect16x9, flipUVHoriz);
        mesh.name = "ArcScreen";
        mf.sharedMesh = mesh;
    }

    void OnEnable() { if (!Application.isPlaying) Rebuild(); }
    void OnValidate() { Rebuild(); }

    static Mesh BuildArcMesh(float r, float h, float deg, int seg, bool dbl, bool aspect169, bool flipU)
    {
        r = Mathf.Max(0.01f, r);
        h = Mathf.Max(0.01f, h);
        seg = Mathf.Clamp(seg, 2, 1024);

        float arcRad = Mathf.Deg2Rad * Mathf.Clamp(deg, 1f, 360f);
        int rows = 2;              // bottom & top
        int vPerRow = seg + 1;
        int sides = dbl ? 2 : 1;
        int vCount = vPerRow * rows * sides;
        int tCount = seg * 2 * sides;

        var verts = new Vector3[vCount];
        var uvs   = new Vector2[vCount];
        var tris  = new int[tCount * 3];

        float halfH = h * 0.5f;

        // UV V-scaling to preserve ~16:9 relative to chord width
        float vTop = 1f;
        if (aspect169)
        {
            float chord = 2f * r * Mathf.Sin(arcRad * 0.5f);   // width the viewer sees
            float idealH = chord * 9f / 16f;
            vTop = Mathf.Clamp01(h / Mathf.Max(0.001f, idealH));
        }

        int vi = 0, ti = 0;
        for (int side = 0; side < sides; side++)
        {
            bool back = (side == 1);
            int baseV = side * vPerRow * rows;

            // vertices + uvs
            for (int y = 0; y < rows; y++)
            {
                float yy = (y == 0 ? -halfH : halfH);
                for (int i = 0; i <= seg; i++)
                {
                    float t = (float)i / seg;
                    float ang = (-arcRad * 0.5f) + arcRad * t;
                    float x = Mathf.Sin(ang) * r;
                    float z = Mathf.Cos(ang) * r;

                    // faces inward by default (toward (+z) for center at origin)
                    verts[vi] = new Vector3(x, yy, z);

                    float u = flipU ? 1f - t : t;
                    if (back) u = 1f - u; // keep texture facing correctly on the back
                    float v = (y == 0 ? 0f : vTop);
                    uvs[vi] = new Vector2(u, v);
                    vi++;
                }
            }

            // triangles
            for (int i = 0; i < seg; i++)
            {
                int a = baseV + i;
                int b = baseV + i + 1;
                int c = baseV + i + vPerRow;
                int d = baseV + i + vPerRow + 1;

                // inward-facing front side (clockwise from stadium view)
                if (!back)
                {
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
                else // back side: flip winding
                {
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                    tris[ti++] = b; tris[ti++] = d; tris[ti++] = c;
                }
            }
        }

        var mesh = new Mesh();
        mesh.indexFormat = (vCount > 65000)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}
