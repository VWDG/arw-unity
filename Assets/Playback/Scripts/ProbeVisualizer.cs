using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProbeVisualizer : MonoBehaviour
{
    void Start()
    {
         var Track = GetComponent<ARTrack>();

        Track.OnAnchor.AddListener(OnAnchor);
    }

    public void OnAnchor(AR.Anchor anchor)
    {
        if (anchor.AnchorType != AR.EAnchorType.Probe) return;

        var probeAnchor = anchor as AR.EnvironmentProbe;

        ReflectionProbe envMapProbeReflectionProbe = null;

        switch (probeAnchor.Status)
        {
            case AR.EStatus.Add:
                RemoveObjectWithIdentifier(probeAnchor.Identifier);

                var newProbe = new GameObject(probeAnchor.Identifier);

                envMapProbeReflectionProbe = newProbe.AddComponent<ReflectionProbe>();

                envMapProbeReflectionProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
                envMapProbeReflectionProbe.resolution = 256;
                envMapProbeReflectionProbe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
                envMapProbeReflectionProbe.backgroundColor = Color.black;
                envMapProbeReflectionProbe.boxProjection = true;
                envMapProbeReflectionProbe.hdr = true;
                break;
            case AR.EStatus.Remove:
                RemoveObjectWithIdentifier(probeAnchor.Identifier);
                break;
            case AR.EStatus.Update:
                var objInScene = GameObject.Find(probeAnchor.Identifier);

                if (objInScene == null)
                {
                    probeAnchor.Status = AR.EStatus.Add;
                    OnAnchor(probeAnchor);
                    objInScene = GameObject.Find(probeAnchor.Identifier);
                }

                TransformExtensions.FromARKitMatrix(objInScene.transform, probeAnchor.Transform);

                if (probeAnchor.Texture != null)
                {
                    envMapProbeReflectionProbe = objInScene.GetComponent<ReflectionProbe>();

                    Debug.Assert(envMapProbeReflectionProbe != null);

                    envMapProbeReflectionProbe.customBakedTexture = probeAnchor.Texture;

                    envMapProbeReflectionProbe.size = probeAnchor.Extent;
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
