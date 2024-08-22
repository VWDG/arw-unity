
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

public class ARTrack : MonoBehaviour
{
    #region Singleton
    static public ARTrack Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }
    #endregion

    [Header("Settings")]
    public string ProjectPath;
    public int FPS = 60;
    public int RecordingFrameStep = 1;
    public bool LoadOnStart = true;
    public bool ExitOnFinish = true;  

    [Header("Delegates")]
    public UnityEvent<AR.Frame> OnFrame = new UnityEvent<AR.Frame>();
    public UnityEvent<AR.Anchor> OnAnchor = new UnityEvent<AR.Anchor>();

    public AR.Project Project { get; private set; }

    private ARW.ProjectParser _projectParser = new ARW.ProjectParser();
    private ARW.CameraParser _cameraParser = new ARW.CameraParser();
    private ARW.DepthStreamParser _depthStreamParser = new ARW.DepthStreamParser();
    private ARW.EnvMapParser _envMapStreamParser = new ARW.EnvMapParser();
    private ARW.AnchorParser _anchorParser = new ARW.AnchorParser();
    private ARW.PlaneAnchorParser _planeAnchorParser = new ARW.PlaneAnchorParser();
    private ARW.LightEstimationParser _lightEstimationParser = new ARW.LightEstimationParser();

    enum EOrientationType
    {
        VIDEO,
        PORTRAIT,
        LANDSCAPE,
        COUNT
    }

    private RenderTexture[] _depthTextures = new RenderTexture[(int)EOrientationType.COUNT];
    private RenderTexture[] _smoothDepthTextures = new RenderTexture[(int)EOrientationType.COUNT];
    private RenderTexture[] _confidenceTextures = new RenderTexture[(int)EOrientationType.COUNT];
    private RenderTexture[] _colorTextures = new RenderTexture[(int)EOrientationType.COUNT];

    private VideoPlayer _videoPlayer;

    private ComputeShader _landscapeLeftCS;
    private ComputeShader _portraitCS;
    private ComputeShader _portraitUpsideDownCS;

    private bool _isFinished = false;

    private bool _canRequestFrame = false;

    private int _videoFrameIndex;
    private AR.Frame _latestARFrame;

    public void Load()
    {
        _isFinished = false;

        Application.targetFrameRate = FPS;

        LoadProject();
        LoadCamera();
        LoadVideo();
        LoadDepth();
        LoadEnvMap();
        LoadAnchors();
        LoadLightEstimation();

        _landscapeLeftCS = Resources.Load<ComputeShader>("LandscapeLeft");
        _portraitCS = Resources.Load<ComputeShader>("Portrait");
        _portraitUpsideDownCS = Resources.Load<ComputeShader>("PortraitUpsideDown");

        if (!IsTrackValid())
        {
            throw new Exception("The track is invalid.");
        }

        StartCoroutine(WaitForVideoPlayer());
    }

    IEnumerator WaitForVideoPlayer()
    {
        while (!_videoPlayer.isPrepared)
        {
            yield return null;
        }

        _videoPlayer.frameReady += OnNewVideoFrame;
        _videoPlayer.sendFrameReadyEvents = true;
    }

    void Start()
    {
        if (LoadOnStart)
        {
            Load();
        }
    }

    void Update()
    {
        if (_canRequestFrame)
        {
            _canRequestFrame = false;
            _videoPlayer.StepForward();
        }
    }

    void OnNewVideoFrame(VideoPlayer videoSource, long videoFrameIndex)
    {
        _videoFrameIndex = (int)videoFrameIndex;

        if (_isFinished)
        {
            return;
        }

        if (!IsTrackValid())
        {
            throw new Exception("The track is invalid.");
        }

        // Frame
        _latestARFrame = new AR.Frame();
        AR.EScreenOrientation orientation = AR.EScreenOrientation.Unknown;

        _latestARFrame.Project = Project;

        if (_cameraParser.Frames.ContainsKey(_videoFrameIndex))
        {
            _latestARFrame.Camera = _cameraParser.Get(_videoFrameIndex);
            orientation = _latestARFrame.Camera.Orientation;
        }

        if (_depthStreamParser.Frames.ContainsKey(_videoFrameIndex))
        {
            var DepthTextureSet = _depthStreamParser.Get(_videoFrameIndex);

            Graphics.Blit(DepthTextureSet.Depth, _depthTextures[(int)EOrientationType.VIDEO]);
            Graphics.Blit(DepthTextureSet.SmoothDepth, _smoothDepthTextures[(int)EOrientationType.VIDEO]);
            Graphics.Blit(DepthTextureSet.Confidence, _confidenceTextures[(int)EOrientationType.VIDEO]);

            _latestARFrame.DepthTexture = GetOrientedTexture(_depthTextures, orientation);
            _latestARFrame.SmoothDepthTexture = GetOrientedTexture(_smoothDepthTextures, orientation);
            _latestARFrame.ConfidenceTexture = GetOrientedTexture(_confidenceTextures, orientation);
        }

        if (_lightEstimationParser.Frames.ContainsKey(_videoFrameIndex))
        {
            _latestARFrame.LightEstimation = _lightEstimationParser.Get(_videoFrameIndex);
        }

        _latestARFrame.ColorTexture = GetOrientedTexture(_colorTextures, orientation);

        if (!IsFrameValid(_latestARFrame))
        {
            throw new Exception("Got invalid frame in " + _videoFrameIndex.ToString() + ". Frame skipped.");
        }

        OnFrame.Invoke(_latestARFrame);

        // Anchors
        var anchors = _anchorParser.Get(_videoFrameIndex);

        foreach (var anchor in anchors)
        {
            OnAnchor.Invoke(anchor);
        }

        var environmentMapAnchors = _envMapStreamParser.Get(_videoFrameIndex);

        foreach (var environmentMapAnchor in environmentMapAnchors)
        {
            OnAnchor.Invoke(environmentMapAnchor);
        }

        var planeAnchors = _planeAnchorParser.Get(_videoFrameIndex);

        foreach (var planeAnchor in planeAnchors)
        {
            OnAnchor.Invoke(planeAnchor);
        }

        // Finish
        if (_videoFrameIndex == (int)_videoPlayer.frameCount - 1)
        {
            _isFinished = true;
            Debug.Log("Finished playback of AR recording");

            if (ExitOnFinish)
            {
                EditorApplication.isPlaying = false;
            }
        }

        _canRequestFrame = true;
    }

    private RenderTexture GetOrientedTexture(RenderTexture[] input, AR.EScreenOrientation orientation)
    {
        RenderTexture resultTexture;
        ComputeShader rotationShader;

        switch (orientation)
        {
            case AR.EScreenOrientation.LandscapeRight:
                {
                    return input[(int)EOrientationType.VIDEO];
                }
            case AR.EScreenOrientation.LandscapeLeft:
                {
                    resultTexture = input[(int)EOrientationType.LANDSCAPE];
                    rotationShader = _landscapeLeftCS;
                    break;
                }
            case AR.EScreenOrientation.Portrait:
                {
                    resultTexture = input[(int)EOrientationType.PORTRAIT];
                    rotationShader = _portraitCS;
                    break;
                }
            case AR.EScreenOrientation.PortraitUpsideDown:
                {
                    resultTexture = input[(int)EOrientationType.PORTRAIT];
                    rotationShader = _portraitUpsideDownCS;
                    break;
                }
            default:
                return null;
        }

        var kernel = rotationShader.FindKernel("CSMain");
        rotationShader.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out _);

        rotationShader.SetTexture(kernel, "Input", input[(int)EOrientationType.VIDEO]);
        rotationShader.SetTexture(kernel, "Output", resultTexture);

        rotationShader.SetInt("Width", input[0].width);
        rotationShader.SetInt("Height", input[0].height);

        rotationShader.Dispatch(kernel, DivUp(input[0].width, (int)x), DivUp(input[0].height, (int)y), 1);

        return resultTexture;
    }

    void LoadProject()
    {
        Project = _projectParser.Get(ProjectPath);
    }

    bool LoadCamera()
    {
        return _cameraParser.Load(ProjectPath, Project);
    }

    bool LoadVideo()
    {
        RenderTextureDescriptor colorTextureDesc = new RenderTextureDescriptor(
            width: Project.ColorSize.x,
            height: Project.ColorSize.y,
            colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,
            depthBufferBits: 0,
            mipCount: 0
            );

        colorTextureDesc.enableRandomWrite = true;

        _colorTextures[(int)EOrientationType.VIDEO] = new RenderTexture(colorTextureDesc);

        _videoPlayer = gameObject.AddComponent<VideoPlayer>();

        _videoPlayer.source        = VideoSource.Url;
        _videoPlayer.url           = ProjectPath + "/color.mov";
        _videoPlayer.playOnAwake   = false;
        _videoPlayer.aspectRatio   = VideoAspectRatio.Stretch;
        _videoPlayer.isLooping     = false;
        _videoPlayer.renderMode    = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = _colorTextures[(int)EOrientationType.VIDEO];
        _videoPlayer.playbackSpeed = 0.0f;

        _colorTextures[(int)EOrientationType.LANDSCAPE] = new RenderTexture(colorTextureDesc);

        colorTextureDesc.width = Project.ColorSize.y;
        colorTextureDesc.height = Project.ColorSize.x;

        _colorTextures[(int)EOrientationType.PORTRAIT] = new RenderTexture(colorTextureDesc);

        return true;
    }

    bool LoadDepth()
    {
        var depthRTFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
        var confidenceRTFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm;

        RenderTextureDescriptor depthTextureDesc = new RenderTextureDescriptor(
            width: Project.DepthSize.x,
            height: Project.DepthSize.y,
            colorFormat: depthRTFormat,
            depthBufferBits: 0,
            mipCount: 0
        );

        RenderTextureDescriptor confidenceTextureDesc = new RenderTextureDescriptor(
            width: Project.DepthSize.x,
            height: Project.DepthSize.y,
            colorFormat: confidenceRTFormat,
            depthBufferBits: 0,
            mipCount: 0
        );

        depthTextureDesc.enableRandomWrite = true;

        // Create landscape depth textures

        _depthTextures[(int)EOrientationType.VIDEO] = new RenderTexture(depthTextureDesc);
        _smoothDepthTextures[(int)EOrientationType.VIDEO] = new RenderTexture(depthTextureDesc);

        _depthTextures[(int)EOrientationType.LANDSCAPE] = new RenderTexture(depthTextureDesc);
        _smoothDepthTextures[(int)EOrientationType.LANDSCAPE] = new RenderTexture(depthTextureDesc);

        // Create landscape confidence textures

        depthTextureDesc.graphicsFormat = confidenceRTFormat;

        _confidenceTextures[(int)EOrientationType.VIDEO] = new RenderTexture(confidenceTextureDesc);
        _confidenceTextures[(int)EOrientationType.LANDSCAPE] = new RenderTexture(confidenceTextureDesc);

        // Create portrait confidence texture

        depthTextureDesc.width = Project.DepthSize.y;
        depthTextureDesc.height = Project.DepthSize.x;

        _confidenceTextures[(int)EOrientationType.PORTRAIT] = new RenderTexture(confidenceTextureDesc);

        // Create portrait depth textures

        depthTextureDesc.graphicsFormat = depthRTFormat;

        _depthTextures[(int)EOrientationType.PORTRAIT] = new RenderTexture(depthTextureDesc);
        _smoothDepthTextures[(int)EOrientationType.PORTRAIT] = new RenderTexture(depthTextureDesc);

        return _depthStreamParser.Load(ProjectPath, Project.NumberOfFrames, Project);
    }

    bool LoadEnvMap()
    {
        return _envMapStreamParser.Load(ProjectPath, Project.NumberOfFrames);
    }

    bool LoadAnchors()
    {
        bool check = true;
        check &= _planeAnchorParser.Load(ProjectPath);
        check &= _anchorParser.Load(ProjectPath);
        return check;
    }

    bool LoadLightEstimation()
    {
        return _lightEstimationParser.Load(ProjectPath);
    }

    bool IsFrameValid(AR.Frame frame)
    {
        if (frame.ColorTexture == null) return false;
        if (frame.DepthTexture == null) return false;
        if (frame.Camera == null) return false;

        return true;
    }

    bool IsTrackValid()
    {
        return _videoPlayer != null;
    }

    private int DivUp(int totalShaderCount, int workgroupSize)
    {
        return (totalShaderCount + workgroupSize - 1) / workgroupSize;
    }
}
