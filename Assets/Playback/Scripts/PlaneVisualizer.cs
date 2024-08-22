using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaneVisualizer : MonoBehaviour
{
    void Start()
    {
        var arTrack = GetComponent<ARTrack>();

        arTrack.OnAnchor.AddListener(OnAnchor);
    }

    public void OnAnchor(AR.Anchor anchor)
    {
        if (anchor.AnchorType != AR.EAnchorType.Plane) return;

        var planeAnchor = anchor as AR.PlaneAnchor;

        switch (anchor.Status)
        {
            case AR.EStatus.Add:
                RemoveObjectWithIdentifier(anchor.Identifier);

                var newEntity = new GameObject(anchor.Identifier);

                var meshRenderer = newEntity.AddComponent<MeshRenderer>();

                meshRenderer.material = new Material(Shader.Find("Standard"));

                var meshFilter = newEntity.AddComponent<MeshFilter>();

                var mesh = new Mesh();

                Vector3[] vertices = new Vector3[4]
                {
                    new Vector3(0, 0, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(0, 0, 1),
                    new Vector3(1, 0, 1)
                };
                mesh.vertices = vertices;

                int[] tris = new int[6]
                {
                    // lower left triangle
                    0, 2, 1,
                    // upper right triangle
                    2, 3, 1
                };
                mesh.triangles = tris;

                Vector3[] normals = new Vector3[4]
                {
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward
                };
                mesh.normals = normals;

                Vector2[] uv = new Vector2[4]
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1)
                };
                mesh.uv = uv;

                meshFilter.mesh = mesh;

                TransformExtensions.FromARKitMatrix(newEntity.transform, anchor.Transform);

                newEntity.transform.Translate(VectorExtensions.ARKitToUnity(planeAnchor.Center));

                newEntity.transform.localScale = SaturateExtent(planeAnchor.Extent);

                // TODO: Use ARPlaneExtent from iOS >16.0 ???

                break;
            case AR.EStatus.Remove:
                RemoveObjectWithIdentifier(anchor.Identifier);
                break;
            case AR.EStatus.Update:
                var objInScene = GameObject.Find(anchor.Identifier);

                if (objInScene == null)
                {
                    anchor.Status = AR.EStatus.Add;
                    OnAnchor(anchor);
                    objInScene = GameObject.Find(anchor.Identifier);
                }

                TransformExtensions.FromARKitMatrix(objInScene.transform, anchor.Transform);

                objInScene.transform.Translate(VectorExtensions.ARKitToUnity(planeAnchor.Center));

                objInScene.transform.localScale = SaturateExtent(planeAnchor.Extent);
                break;
        }
    }

    private void RemoveObjectWithIdentifier(string identidier)
    {
        var obj = GameObject.Find(identidier);

        if (obj != null) GameObject.Destroy(obj);
    }

    private Vector3 SaturateExtent(Vector3 extent)
    {
        var saturateExtent = new Vector3();

        saturateExtent.x = Mathf.Max(0.01f, extent.x);
        saturateExtent.y = Mathf.Max(0.01f, extent.y);
        saturateExtent.z = Mathf.Max(0.01f, extent.z);

        return saturateExtent;
    }
}
