using UnityEngine;
using UnityEngine.UI;
namespace DigDeep
{
    // One reusable comic burst mesh. No texture or particle object is created when it fires.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BlastBurstGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            for (int layer = 0; layer < 2; layer++)
            {
                int start = mesh.currentVertCount;
                var tint = layer == 0 ? new Color(0, 0, 0, color.a) : color;
                mesh.AddVert(Vector3.zero, tint, Vector2.zero);
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * Mathf.PI / 12;
                    float radius = (i % 2 == 0 ? 1f : .57f) * (layer == 0 ? 1f : .88f);
                    mesh.AddVert(new Vector3(Mathf.Cos(angle) * rectTransform.rect.width * .5f * radius, Mathf.Sin(angle) * rectTransform.rect.height * .5f * radius), tint, Vector2.zero);
                }
                for (int i = 0; i < 24; i++) mesh.AddTriangle(start, start + i + 1, start + (i + 1) % 24 + 1);
            }
        }
    }
}
