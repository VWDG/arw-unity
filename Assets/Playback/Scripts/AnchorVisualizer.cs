using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnchorVisualizer : MonoBehaviour
{
    public GameObject ARObject;

    public float HeightOffset = 0.0f;

    void Start()
    {
        var arTrack = GetComponent<ARTrack>();

        arTrack.OnAnchor.AddListener(OnAnchor);
    }

    public void OnAnchor(AR.Anchor anchor)
    {
        if (anchor.AnchorType != AR.EAnchorType.WorldPosition) return;

        GameObject objInScene = null;

        switch (anchor.Status)
        {
            case AR.EStatus.Add:
                RemoveObjectWithIdentifier(anchor.Identifier);

                objInScene = new GameObject(anchor.Identifier);

                var arEntity = GameObject.Instantiate(ARObject);

                arEntity.transform.SetParent(objInScene.transform, false);

                TransformExtensions.FromARKitMatrix(objInScene.transform, anchor.Transform);

                objInScene.transform.Translate(new Vector3(0.0f, HeightOffset, 0.0f));
                break;
            case AR.EStatus.Remove:
                RemoveObjectWithIdentifier(anchor.Identifier);
                break;
            case AR.EStatus.Update:
                objInScene = GameObject.Find(anchor.Identifier);

                if (objInScene == null)
                {
                    anchor.Status = AR.EStatus.Add;
                    OnAnchor(anchor);
                }
                else
                {
                    TransformExtensions.FromARKitMatrix(objInScene.transform, anchor.Transform);

                    objInScene.transform.Translate(new Vector3(0.0f, HeightOffset, 0.0f));
                }
                break;
        }
    }

    private void RemoveObjectWithIdentifier(string identidier)
    {
        var obj = GameObject.Find(identidier);

        if (obj != null) GameObject.Destroy(obj);
    }
}
