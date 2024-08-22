using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Globalization;

namespace ARW
{
    class ProjectParser
    {
        struct JSONProject
        {
            public string Name;
            public string Description;
            public string CreationDate;
            public string NumberOfFrames;
            public List<int> ColorSize;
            public List<int> DepthSize;
            public List<int> ViewportSize;
            public string ModelName;
        };

        public AR.Project Get(string pathToProject)
        {
            string pathToProjectFile = pathToProject + "/project.json";

            string jsonString = null;

            using (StreamReader r = new StreamReader(pathToProjectFile))
            {
                jsonString = r.ReadToEnd();
            }

            if (jsonString == null) return null;

            var jsonData = JsonConvert.DeserializeObject<JSONProject>(jsonString);

            var project = new AR.Project();

            project.Name           = jsonData.Name;
            project.Description    = jsonData.Description;
            project.CreationDate   = ToDateTime(Double.Parse(jsonData.CreationDate, CultureInfo.InvariantCulture));
            project.NumberOfFrames = int.Parse(jsonData.NumberOfFrames);
            project.ColorSize      = VectorExtensions.FromArray(jsonData.ColorSize);
            project.DepthSize      = VectorExtensions.FromArray(jsonData.DepthSize);
            project.ViewPortSize   = VectorExtensions.FromArray(jsonData.ViewportSize);
            project.ModelName      = jsonData.ModelName;

            Debug.Log("Loaded project props of " + project.Name + " with " + project.NumberOfFrames.ToString() + " frames.");

            return project;
        }

        private DateTime ToDateTime(double unixTimeStamp)
        {
            DateTime dateTime = new DateTime(2001, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dateTime;
        }
    }

    class CameraParser
    {
        [Serializable]
        class InternCamera
        {
            public int orientation;
            public float[] viewMatrix;
            public float[] projectionMatrix;
            public float[] transform;
            public float[] intrinsics;
            public int frame;
        }

        [Serializable]
        class InternCameraList
        {

            public List<InternCamera> internCameraList;
        }

        public Dictionary<int, AR.Camera> Frames = new Dictionary<int, AR.Camera>();

        public static Vector4 GetIntrinsics(Matrix4x4 matrix)
        {
            float fx = matrix.m00;
            float fy = matrix.m11;
            float cx = matrix.m02;
            float cy = matrix.m12;

            return new Vector4(fx, fy, cx, cy);
        }

        public bool Load(string pathToProject, AR.Project Project)
        {
            string pathToProjectFile = pathToProject + "/camera.json";

            string jsonString = null;

            using (StreamReader r = new StreamReader(pathToProjectFile))
            {
                jsonString = r.ReadToEnd();
            }

            if (jsonString == null) return false;

            var InternalCameras = JsonUtility.FromJson<InternCameraList>("{\"internCameraList\":" + jsonString + "}").internCameraList;

            foreach (var obj in InternalCameras)
            {
                var newCamera = new AR.Camera();

                newCamera.Transform        = MatrixExtensions.FromArray(obj.transform);
                newCamera.ViewMatrix       = MatrixExtensions.FromArray(obj.viewMatrix);
                newCamera.ProjectionMatrix = MatrixExtensions.FromArray(obj.projectionMatrix);

                var intrinsics             = GetIntrinsics(MatrixExtensions.FromArray(obj.intrinsics));
                var orientation            = (AR.EScreenOrientation)obj.orientation;

                // TODO: Check if everything works correctly
                // Convert intrinsics from the default LandscapeRight if necessary
                switch (orientation)
                {
                    case AR.EScreenOrientation.LandscapeLeft:
                        intrinsics = new Vector4(
                            intrinsics.x,
                            intrinsics.y,
                            Project.ColorSize.x - intrinsics.z,
                            Project.ColorSize.y - intrinsics.w
                        );
                        break;
                    case AR.EScreenOrientation.Portrait:
                        intrinsics = new Vector4(intrinsics.y, intrinsics.x, intrinsics.w, intrinsics.z);
                        break;
                    case AR.EScreenOrientation.PortraitUpsideDown:
                        intrinsics = new Vector4(
                            intrinsics.y,
                            intrinsics.x,
                            Project.ColorSize.y - intrinsics.w,
                            Project.ColorSize.x - intrinsics.z
                        );
                        break;
                }

                if (orientation == AR.EScreenOrientation.LandscapeLeft || orientation == AR.EScreenOrientation.LandscapeRight)
                {
                    newCamera.DepthIntrinsics = new Vector4(
                        intrinsics.x / Project.ColorSize.x * Project.DepthSize.x,
                        intrinsics.y / Project.ColorSize.y * Project.DepthSize.y,
                        intrinsics.z / Project.ColorSize.x * Project.DepthSize.x,
                        intrinsics.w / Project.ColorSize.y * Project.DepthSize.y
                    );
                }
                else
                {
                    newCamera.DepthIntrinsics = new Vector4(
                        intrinsics.x / Project.ColorSize.y * Project.DepthSize.y,
                        intrinsics.y / Project.ColorSize.x * Project.DepthSize.x,
                        intrinsics.z / Project.ColorSize.y * Project.DepthSize.y,
                        intrinsics.w / Project.ColorSize.x * Project.DepthSize.x
                    );
                }

                newCamera.ColorIntrinsics = intrinsics;
                newCamera.Orientation     = orientation;

                Frames.Add(obj.frame, newCamera);
            }

            return true;
        }

        public AR.Camera Get(int frame)
        {
            if (!Frames.ContainsKey(frame)) return null;

            return Frames[frame];
        }
    }

    class DepthStreamParser
    {
        public class DepthSet
        {
            public RenderTexture Depth;
            public RenderTexture SmoothDepth;
            public RenderTexture Confidence;
        }

        public bool Load(string pathToProject, int numberOfFrames, AR.Project project)
        {
            _textureFlipCS = Resources.Load<ComputeShader>("TextureFlip");

            _newDepthTexture = new Texture2D(project.DepthSize.x, project.DepthSize.y, TextureFormat.RFloat, false);
            _newConfidenceTexture = new Texture2D(project.DepthSize.x, project.DepthSize.y, TextureFormat.R8, false);

            _depthTextureDesc = new RenderTextureDescriptor(
                width: project.DepthSize.x,
                height: project.DepthSize.y,
                colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat,
                depthBufferBits: 0,
                mipCount: 0
            );

            _confidenceTextureDesc = new RenderTextureDescriptor(
                width: project.DepthSize.x,
                height: project.DepthSize.y,
                colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm,
                depthBufferBits: 0,
                mipCount: 0
            );

            _depthTextureDesc.enableRandomWrite = true;
            _confidenceTextureDesc.enableRandomWrite = true;

            for (int frame = 0; frame < numberOfFrames; ++ frame)
            {
                string pathToDepth = pathToProject + "/" + frame.ToString() + "/depth.raw";
                string pathToSmoothDepth = pathToProject + "/" + frame.ToString() + "/smooth_depth.raw";
                string pathToConfidence = pathToProject + "/" + frame.ToString() + "/depth_conf.raw";

                var depthSet = new DepthSet();

                depthSet.Depth = LoadDepthTexture(pathToDepth);
                depthSet.SmoothDepth = LoadDepthTexture(pathToSmoothDepth);
                depthSet.Confidence = LoadConfidenceTexture(pathToConfidence);

                Frames.Add(frame, depthSet);
            }

            return true;
        }

        public DepthSet Get(int frame)
        {
            if (!Frames.ContainsKey(frame)) return null;

            return Frames[frame];
        }

        private int DivUp(int totalShaderCount, int workgroupSize)
        {
            return (totalShaderCount + workgroupSize - 1) / workgroupSize;
        }

        private RenderTexture LoadDepthTexture(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(path);

            var width = BitConverter.ToInt32(new byte[] { bytes[0], bytes[1], bytes[2], bytes[3] });
            var Height = BitConverter.ToInt32(new byte[] { bytes[4], bytes[5], bytes[6], bytes[7] });

            byte[] rawBytes = new byte[width * Height * 4];

            Buffer.BlockCopy(bytes, 8, rawBytes, 0, width * Height * 4);

            RenderTexture flippedDepthTexture = new RenderTexture(_depthTextureDesc);

            _newDepthTexture.LoadRawTextureData(rawBytes);
            _newDepthTexture.Apply();

            var kernel = _textureFlipCS.FindKernel("CSMain");
            _textureFlipCS.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out _);

            _textureFlipCS.SetTexture(kernel, "Input", _newDepthTexture);
            _textureFlipCS.SetTexture(kernel, "Output", flippedDepthTexture);

            _textureFlipCS.SetInt("Width", width);
            _textureFlipCS.SetInt("Height", Height);

            _textureFlipCS.Dispatch(kernel, DivUp(width, (int)x), DivUp(Height, (int)y), 1);

            return flippedDepthTexture;
        }

        private RenderTexture LoadConfidenceTexture(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(path);

            var width = BitConverter.ToInt32(new byte[] { bytes[0], bytes[1], bytes[2], bytes[3] });
            var Height = BitConverter.ToInt32(new byte[] { bytes[4], bytes[5], bytes[6], bytes[7] });

            byte[] rawBytes = new byte[width * Height];

            Buffer.BlockCopy(bytes, 8, rawBytes, 0, width * Height);

            RenderTexture flippedConfidenceTexture = new RenderTexture(_confidenceTextureDesc);

            _newConfidenceTexture.LoadRawTextureData(rawBytes);
            _newConfidenceTexture.Apply();

            var kernel = _textureFlipCS.FindKernel("CSMain");
            _textureFlipCS.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out _);

            _textureFlipCS.SetTexture(kernel, "Input", _newConfidenceTexture);
            _textureFlipCS.SetTexture(kernel, "Output", flippedConfidenceTexture);

            _textureFlipCS.SetInt("Width", width);
            _textureFlipCS.SetInt("Height", Height);

            _textureFlipCS.Dispatch(kernel, DivUp(width, (int)x), DivUp(Height, (int)y), 1);

            return flippedConfidenceTexture;
        }

        public Dictionary<int, DepthSet> Frames = new Dictionary<int, DepthSet>();

        private RenderTextureDescriptor _depthTextureDesc;
        private RenderTextureDescriptor _confidenceTextureDesc;
        private ComputeShader _textureFlipCS;
        private Texture2D _newDepthTexture;
        private Texture2D _newConfidenceTexture;
    }

    class EnvMapParser
    {
        [Serializable]
        class InternEnvironmentProbe
        {
            public string name;
            public string identifier;
            public float[] transform;
            public float[] extent;
            public string status;
            public int frame;
        }

        [Serializable]
        class InternEnvironmentProbeList
        {

            public List<InternEnvironmentProbe> internEnvironmentProbeList;
        }

        public Dictionary<int, List<AR.EnvironmentProbe>> Frames = new Dictionary<int, List<AR.EnvironmentProbe>>();

        public bool Load(string pathToProject, int numberOfFrames)
        {
            string PathToProjectFile = pathToProject + "/env_probe.json";

            string jsonString = null;

            using (StreamReader r = new StreamReader(PathToProjectFile))
            {
                jsonString = r.ReadToEnd();
            }

            if (jsonString == null) return false;

            var InternalEnvironmentProbes = JsonUtility.FromJson<InternEnvironmentProbeList>("{\"internEnvironmentProbeList\":" + jsonString + "}").internEnvironmentProbeList;

            foreach (var obj in InternalEnvironmentProbes)
            {
                var newProbe = new AR.EnvironmentProbe();

                newProbe.AnchorType = AR.EAnchorType.Probe;
                newProbe.Name = obj.name;
                newProbe.Identifier = obj.identifier;
                newProbe.Transform = MatrixExtensions.FromArray(obj.transform);
                newProbe.Extent = VectorExtensions.FromArray(obj.extent);
                newProbe.Texture = null;
                newProbe.Status = (AR.EStatus)Enum.Parse(typeof(AR.EStatus), obj.status);

                string pathToFrame = pathToProject + "/" + obj.frame.ToString() + "/";

                if (Directory.Exists(pathToFrame))
                {
                    Texture2D[] cubemapSides = new Texture2D[6];

                    int width = 0, height = 0;

                    for (int i = 0; i < 6; ++i)
                    {
                        string pathToDepthCubemap = pathToFrame + "/" + newProbe.Identifier + "/envcubemap_" + i.ToString() + ".raw";

                        if (!File.Exists(pathToDepthCubemap)) break;

                        var bytes = File.ReadAllBytes(pathToDepthCubemap);

                        width = BitConverter.ToInt32(new byte[] { bytes[0], bytes[1], bytes[2], bytes[3] });
                        height = BitConverter.ToInt32(new byte[] { bytes[4], bytes[5], bytes[6], bytes[7] });

                        // Width + Height + RGBA * 16bit Float
                        byte[] rawBytes = new byte[width * height * 4 * 2];

                        Buffer.BlockCopy(bytes, 8, rawBytes, 0, width * height * 4 * 2);

                        cubemapSides[i] = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);

                        cubemapSides[i].LoadRawTextureData(rawBytes);

                        // Convert from sRGB to RGB
                        var pixelData = cubemapSides[i].GetPixelData<ushort>(0);

                        for (int j = 0; j < pixelData.Length; ++j)
                        {
                            float pixel = Mathf.HalfToFloat(pixelData[j]);

                            var linearPixel = Mathf.Pow(pixel, 1.0f / 2.2f);

                            pixelData[j] = Mathf.FloatToHalf(linearPixel);
                        }

                        // Apply texture to cubemap
                        cubemapSides[i].Apply();
                    }

                    if (width != 0 && height != 0)
                    {
                        // Create cubemap based on the cubemap sides
                        Cubemap cubemap = new Cubemap(width, TextureFormat.RGBAHalf, true);

                        for (int i = 0; i < 6; ++i)
                        {
                            cubemap.SetPixels(cubemapSides[i].GetPixels(), CubemapFace.PositiveX + i);
                        }

                        cubemap.Apply();

                        newProbe.Texture = cubemap;
                    }
                }

                if (!Frames.ContainsKey(obj.frame))
                {
                    Frames.Add(obj.frame, new List<AR.EnvironmentProbe>());
                }

                Frames[obj.frame].Add(newProbe);
            }

            return true;
        }

        public List<AR.EnvironmentProbe> Get(int frame)
        {
            if (!Frames.ContainsKey(frame)) return new List<AR.EnvironmentProbe>();

            return Frames[frame];
        }
    }

    class AnchorParser
    {
        [Serializable]
        class InternAnchor
        {
            public string name;
            public string identifier;
            public float[] transform;
            public string status;
            public int frame;
        }

        [Serializable]
        class InternAnchorList
        {

            public List<InternAnchor> internAnchorList;
        }

        public Dictionary<int, List<AR.Anchor>> Frames = new Dictionary<int, List<AR.Anchor>>();

        public bool Load(string pathToProject)
        {
            string pathToProjectFile = pathToProject + "/anchor.json";

            string jsonString = null;

            using (StreamReader r = new StreamReader(pathToProjectFile))
            {
                jsonString = r.ReadToEnd();
            }

            if (jsonString == null) return false;

            var InternalAnchors = JsonUtility.FromJson<InternAnchorList>("{\"internAnchorList\":" + jsonString + "}").internAnchorList;

            foreach (var obj in InternalAnchors)
            {
                var newEntity = new AR.Anchor();

                newEntity.AnchorType = AR.EAnchorType.WorldPosition;
                newEntity.Name       = obj.name;
                newEntity.Identifier = obj.identifier;
                newEntity.Transform  = MatrixExtensions.FromArray(obj.transform);
                newEntity.Status     = (AR.EStatus)Enum.Parse(typeof(AR.EStatus), obj.status);

                if (!Frames.ContainsKey(obj.frame))
                {
                    Frames.Add(obj.frame, new List<AR.Anchor>());
                }

                Frames[obj.frame].Add(newEntity);
            }

            return true;
        }

        public List<AR.Anchor> Get(int frame)
        {
            if (!Frames.ContainsKey(frame)) return new List<AR.Anchor>();

            return Frames[frame];
        }
    }

    class PlaneAnchorParser
    {
        [Serializable]
        class InternPlane
        {
            public string name;
            public string identifier;
            public float[] transform;
            public string status;
            public string classification;
            public int alignment;
            public float[] center;
            public float[] extent;
            public int frame;
        }

        [Serializable]
        class InternPlaneList
        {

            public List<InternPlane> internPlaneList;
        }

        public Dictionary<int, List<AR.PlaneAnchor>> Frames = new Dictionary<int, List<AR.PlaneAnchor>>();

        public bool Load(string pathToProject)
        {
            string pathToProjectFile = pathToProject + "/plane_anchor.json";

            string jsonString = null;

            using (StreamReader r = new StreamReader(pathToProjectFile))
            {
                jsonString = r.ReadToEnd();
            }

            if (jsonString == null) return false;

            var InternalAnchors = JsonUtility.FromJson<InternPlaneList>("{\"internPlaneList\":" + jsonString + "}").internPlaneList;

            foreach (var obj in InternalAnchors)
            {
                var newPlane = new AR.PlaneAnchor();

                newPlane.AnchorType     = AR.EAnchorType.Plane;
                newPlane.Name           = obj.name;
                newPlane.Identifier     = obj.identifier;
                newPlane.Transform      = MatrixExtensions.FromArray(obj.transform);
                newPlane.Status         = (AR.EStatus)Enum.Parse(typeof(AR.EStatus), obj.status);
                newPlane.Classification = obj.classification;
                newPlane.Alignment      = obj.alignment; 
                newPlane.Center         = VectorExtensions.FromArray(obj.center);
                newPlane.Extent         = VectorExtensions.FromArray(obj.extent);

                if (!Frames.ContainsKey(obj.frame))
                {
                    Frames.Add(obj.frame, new List<AR.PlaneAnchor>());
                }

                Frames[obj.frame].Add(newPlane);
            }

            return true;
        }

        public List<AR.PlaneAnchor> Get(int frame)
        {
            if (!Frames.ContainsKey(frame)) return new List<AR.PlaneAnchor>();

            return Frames[frame];
        }
    }

    class LightEstimationParser
    {
        [Serializable]
        class InternLightEstimation
        {
            public float ambientIntensity;
            public float ambientColorTemperature;
            public int frame;
        }

        [Serializable]
        class InternLightEstimationList
        {

            public List<InternLightEstimation> internLightEstimationList;
        }

        public Dictionary<int, AR.LightEstimation> Frames = new Dictionary<int, AR.LightEstimation>();

        public bool Load(string pathToProject)
        {
            string pathToProjectFile = pathToProject + "/lightestimation.json";

            string jsonString = null;

            using (StreamReader r = new StreamReader(pathToProjectFile))
            {
                jsonString = r.ReadToEnd();
            }

            if (jsonString == null) return false;

            var InternalLightEstimations = JsonUtility.FromJson<InternLightEstimationList>("{\"internLightEstimationList\":" + jsonString + "}").internLightEstimationList;

            foreach (var obj in InternalLightEstimations)
            {
                var newData = new AR.LightEstimation();

                newData.AmbientIntensity        = obj.ambientIntensity;
                newData.AmbientColorTemperature = obj.ambientColorTemperature;

                Frames.Add(obj.frame, newData);
            }

            return true;
        }

        public AR.LightEstimation Get(int frame)
        {
            if (!Frames.ContainsKey(frame)) return null;

            return Frames[frame];
        }
    }
}