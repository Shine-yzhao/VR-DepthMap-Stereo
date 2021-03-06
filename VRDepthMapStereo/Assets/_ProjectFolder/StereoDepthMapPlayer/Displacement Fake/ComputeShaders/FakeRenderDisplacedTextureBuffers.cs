﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FakeRenderDisplacedTextureBuffers : MonoBehaviour {

    public Vector2Int textureSize = Vector2Int.one * 4096;
    public ComputeShader _shader;
    public Material _materialToShareTexture;
    public string _textureName = "_MainTex";

    public Texture2D depth;
    public Texture2D albedo;
    [Range(-1, 1)]
    public float _relativePosition;
    [Range(0, 1)]
    public float _parallaxAmount;


    private RenderTexture _outputRT;
    private ComputeBuffer _depthBuffer;
    private ComputeBuffer _mutexBuffer;
    private int _clearKernel;
    private int _writeDepthKernel;
    private int _displaceKernel;
    private uint _xf, _yf;
    private uint _xw, _yw;
    private uint _xd, _yd;
    
    private bool _kernelsLoaded = false;

    private Camera cam { get { return GetComponent<Camera>(); } }

    void Start() {
        _outputRT = CreateRenderTexture();
        _depthBuffer = CreateDepthBuffer();
        List<ComputeBuffer> _depth0 = new List<ComputeBuffer> {
            CreateDepthBuffer(),
            CreateDepthBuffer(),
            CreateDepthBuffer(),
            CreateDepthBuffer()
        };
        _mutexBuffer = CreateMutexBuffer();
        UpdateShaderParameters(_outputRT, _depthBuffer, _depth0, _mutexBuffer);
    }

    private void OnDestroy() {
        _depthBuffer.Release();
        _mutexBuffer.Release();
        _outputRT.Release();
    }
    
    void OnPreRender() {
        DispatchBoth();
    }

    [ContextMenu("ComputeOnTexture")]
    public void ComputeOnTexture() {
        RenderTexture t = CreateRenderTexture();
        ComputeBuffer d = CreateDepthBuffer();
        ComputeBuffer m = CreateMutexBuffer();
        //UpdateShaderParameters(t, d, m);
        DispatchBoth();
        t.Release();
        d.Release();
        m.Release();
    }

    private void DispatchBoth() {
        _shader.SetFloat("RelativePosition", _relativePosition);
        _shader.SetFloat("ParallaxAmount", _parallaxAmount * 400);
        
        if (_kernelsLoaded) {
            _shader.Dispatch(_clearKernel, textureSize.x / (int) _xf, textureSize.y / (int) _yf, 1);
            _shader.Dispatch(_writeDepthKernel, textureSize.x / (int) _xw, textureSize.y / (int) _yw, 1);
            _shader.Dispatch(_displaceKernel, textureSize.x / (int) _xd, textureSize.y / (int) _yd, 1);
        }
    }

    private RenderTexture CreateRenderTexture() {
        RenderTexture rt = new RenderTexture(textureSize.x, textureSize.y, 1, RenderTextureFormat.ARGBFloat) {
            dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
            enableRandomWrite = true
        };
        rt.Create();

        return rt;
    }

    private ComputeBuffer CreateDepthBuffer() {
        ComputeBuffer buffer = new ComputeBuffer(textureSize.x * textureSize.y, 4, ComputeBufferType.Default);
        int[] data = new int[textureSize.x * textureSize.y];
        buffer.SetData(data);

        return buffer;
    }

    private ComputeBuffer CreateMutexBuffer() {
        ComputeBuffer buffer = new ComputeBuffer(textureSize.x * textureSize.y, 4, ComputeBufferType.Default);
        int[] data = new int[textureSize.x * textureSize.y];
        buffer.SetData(data);

        return buffer;
    }

    private void UpdateShaderParameters(RenderTexture t, ComputeBuffer d, List<ComputeBuffer> d0, ComputeBuffer m) {
        uint zf, zd, zw;
        if (t == null) { t = _outputRT; }

        _kernelsLoaded = _shader.HasKernel("WriteDepth") && _shader.HasKernel("DisplaceAlbedo") && _shader.HasKernel("Clear");
        if (!_kernelsLoaded) {
            Debug.LogError("Shader Kernel compilation error");
            return;
        }

        _materialToShareTexture.SetTexture(_textureName, t);
        _clearKernel = _shader.FindKernel("Clear");
        _writeDepthKernel = _shader.FindKernel("WriteDepth");
        _displaceKernel = _shader.FindKernel("DisplaceAlbedo");

        _shader.SetBuffer(_clearKernel, "Depth", d);
        _shader.SetBuffer(_clearKernel, "Depth0[0]", d0[0]);
        _shader.SetBuffer(_clearKernel, "Depth0[1]", d0[1]);
        _shader.SetBuffer(_clearKernel, "Depth0[2]", d0[2]);
        _shader.SetBuffer(_clearKernel, "Depth0[3]", d0[3]);
        _shader.SetBuffer(_clearKernel, "MutexBuffer", m);
        _shader.SetTexture(_clearKernel, "Result", t);

        _shader.SetBuffer(_writeDepthKernel, "Depth", d);
        _shader.SetBuffer(_writeDepthKernel, "Depth0[0]", d0[0]);
        _shader.SetBuffer(_writeDepthKernel, "Depth0[1]", d0[1]);
        _shader.SetBuffer(_writeDepthKernel, "Depth0[2]", d0[2]);
        _shader.SetBuffer(_writeDepthKernel, "Depth0[3]", d0[3]);
        _shader.SetBuffer(_writeDepthKernel, "MutexBuffer", m);
        _shader.SetTexture(_writeDepthKernel, "Result", t);
        _shader.SetTexture(_writeDepthKernel, "DepthTexture", depth);
        _shader.SetTexture(_writeDepthKernel, "AlbedoTexture", albedo);

        _shader.SetBuffer(_displaceKernel, "Depth", d);
        _shader.SetBuffer(_displaceKernel, "Depth0[0]", d0[0]);
        _shader.SetBuffer(_displaceKernel, "Depth0[1]", d0[1]);
        _shader.SetBuffer(_displaceKernel, "Depth0[2]", d0[2]);
        _shader.SetBuffer(_displaceKernel, "Depth0[3]", d0[3]);
        _shader.SetBuffer(_displaceKernel, "MutexBuffer", m);
        _shader.SetTexture(_displaceKernel, "Result", t);
        _shader.SetTexture(_displaceKernel, "DepthTexture", depth);
        _shader.SetTexture(_displaceKernel, "AlbedoTexture", albedo);

        _shader.GetKernelThreadGroupSizes(_clearKernel, out _xf, out _yf, out zf);
        _shader.GetKernelThreadGroupSizes(_writeDepthKernel, out _xw, out _yw, out zw);
        _shader.GetKernelThreadGroupSizes(_displaceKernel, out _xd, out _yd, out zd);
    }
}
