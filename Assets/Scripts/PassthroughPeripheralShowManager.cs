using System;
using System.Collections;
using System.Collections.Generic;
using Trev3d.Quest.ScreenCapture;
using UnityEngine;
using UnityEngine.UI;

public class PassthroughPeripheralShowManager : MonoBehaviour
{
    [SerializeField] private QuestScreenCaptureTextureManager passthroughManager;
    [SerializeField] private Renderer peripheralQuadRenderer;

    private Texture2D passthroughTex;

    void Start()
    {
        int w = QuestScreenCaptureTextureManager.Size.x;    // 640
        int h = QuestScreenCaptureTextureManager.Size.y;    // 640
        passthroughTex = new Texture2D(w, h, TextureFormat.RGB24, mipChain: false, linear: true);

        peripheralQuadRenderer.material.mainTexture = passthroughTex;
        peripheralQuadRenderer.gameObject.SetActive(false);

        if (passthroughManager != null)
            passthroughManager.OnNewFrame.AddListener(OnNewFrameRecieved);
    }

    private void OnNewFrameRecieved(byte[] image)
    {
        passthroughTex.LoadRawTextureData(image);
        passthroughTex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        if (!peripheralQuadRenderer.gameObject.activeSelf)
            peripheralQuadRenderer.gameObject.SetActive(true);
    }
}
