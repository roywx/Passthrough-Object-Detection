


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GazeObjectPeripheralShowManager : MonoBehaviour
{
    [SerializeField] private DataCommunicator dataCommunicator;
    [SerializeField] private Renderer peripheralQuadRenderer;

    [SerializeField] private float displaySeconds = 2.0f; // how long to keep it visible
    private float hideAtTime = -1f;                       // next time to hide

    void Start()
    {
        if (dataCommunicator != null)
            dataCommunicator.OnTextureReceived.AddListener(OnServerTextureReceived);

        peripheralQuadRenderer.gameObject.SetActive(false);
    }

    void Update()
    {
        if (peripheralQuadRenderer.gameObject.activeSelf && Time.time >= hideAtTime)
            peripheralQuadRenderer.gameObject.SetActive(false);
    }

    private void OnServerTextureReceived(Texture2D serverTexture)
    {
        peripheralQuadRenderer.material.mainTexture = serverTexture;
        peripheralQuadRenderer.gameObject.SetActive(true);
        hideAtTime = Time.time + displaySeconds;
    }
}
