
using System;
using UnityEngine;

namespace AR
{
    public enum EScreenOrientation
    {
        Unknown,
        Portrait,
        PortraitUpsideDown,
        LandscapeRight,
        LandscapeLeft
    }

    public enum EStatus
    {
        Add = 0,
        Remove = 1,
        Update = 2
    }

    public enum EAnchorType
    {
        WorldPosition,
        Plane,
        Probe
    }

    public class Project
    {
        public string Name;
        public string Description;
        public DateTime CreationDate;
        public int NumberOfFrames;
        public Vector2Int ColorSize;
        public Vector2Int DepthSize;
        public Vector2Int ViewPortSize;
        public string ModelName;
    }

    public class Camera
    {
        public Vector4 ColorIntrinsics;
        public Vector4 DepthIntrinsics;
        public Matrix4x4 Transform;
        public EScreenOrientation Orientation;
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionMatrix;
    }

    public class LightEstimation
    {
        public float AmbientIntensity;
        public float AmbientColorTemperature;
    }

    public class Anchor
    {
        public EAnchorType AnchorType;
        public string Name;
        public string Identifier;
        public Matrix4x4 Transform;
        public EStatus Status;
    }

    public class PlaneAnchor : Anchor
    {
        public string Classification;
        public int Alignment;
        public Vector4 Center;
        public Vector4 Extent;
    }

    public class EnvironmentProbe : Anchor
    {
        public Vector3 Extent;
        public Cubemap Texture;
    }

    public class Frame
    {
        public Project Project;
        public Camera Camera;
        public RenderTexture ColorTexture;
        public RenderTexture DepthTexture;
        public RenderTexture SmoothDepthTexture;
        public RenderTexture ConfidenceTexture;
        public LightEstimation LightEstimation;
    }
}
