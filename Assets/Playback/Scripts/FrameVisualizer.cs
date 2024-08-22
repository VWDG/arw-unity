
using UnityEngine;

public class FrameVisualizer : MonoBehaviour
{
    public Camera ProjectCamera;
    public RenderTexture ColorTexture;
    public RenderTexture DepthTexture;

    // TODO: Get values from project settings file
    private float _focalLength = 26.0f;

    void Start()
    {
        var arTrack = GetComponent<ARTrack>();

        arTrack.OnFrame.AddListener(OnFrame);
    }

    public void OnFrame(AR.Frame frame)
    {
        // Camera
        if (frame.Camera != null)
        {
            // Transformation
            TransformExtensions.FromARKitMatrix(ProjectCamera.gameObject.transform, frame.Camera.ViewMatrix.inverse);

            // Intrinsics
            // TODO: Use correctly rotated intrinsics depending on orientation
            float fx = frame.Camera.ColorIntrinsics.x;
            float fy = frame.Camera.ColorIntrinsics.y;
            float cx = frame.Camera.ColorIntrinsics.z;
            float cy = frame.Camera.ColorIntrinsics.w;

            float SizeX = _focalLength * frame.ColorTexture.width / fx;
            float SizeY = _focalLength * frame.ColorTexture.height / fy;

            float ShiftX = -(cx - frame.ColorTexture.width / 2.0f) / frame.ColorTexture.width;
            float ShiftY = (cy - frame.ColorTexture.height / 2.0f) / frame.ColorTexture.height;

            ProjectCamera.usePhysicalProperties = true;

            ProjectCamera.sensorSize = new Vector2(SizeX, SizeY);
            ProjectCamera.focalLength = _focalLength;
            ProjectCamera.lensShift = new Vector2(ShiftX, ShiftY);
        }

        // Light Estimation
        if (frame.LightEstimation != null)
        {
            // TODO: Add light estimation to scene
            // Debug.Log("Ambient intensits: " + frame.LightEstimation.AmbientIntensity);
        }        

        // Color + Depth
        if (frame.ColorTexture != null)
        {
            Graphics.Blit(frame.ColorTexture, ColorTexture);
        }

        if (frame.DepthTexture != null)
        {
            Graphics.Blit(frame.DepthTexture, DepthTexture);
        }
    }
}
