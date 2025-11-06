using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.Events;

public class DataCommunicator : MonoBehaviour
{
    // Set your server URL (e.g., "http://127.0.0.1:5000/objectDetection")
    [SerializeField] private string serverUrl = "http://127.0.0.1:5000/objectDetection";

    // This event is invoked when the server returns an enhanced texture.
    public UnityEvent<Texture2D> OnTextureReceived = new UnityEvent<Texture2D>();

    [SerializeField] float minIntervalSec = 0.5f;   // 2 FPS max
    bool busy = false;
    float lastSendTime = -999f;

    public void TrySendFrame(byte[] frame)
    {
        if (busy) return;
        if (Time.time - lastSendTime < minIntervalSec) return;

        lastSendTime = Time.time;
        StartCoroutine(SendFrame(frame));
    }

    private IEnumerator SendFrame(byte[] frame)
    {
        busy = true;
        WWWForm form = new WWWForm();

        using UnityWebRequest req = UnityWebRequest.Post(serverUrl, form);
        req.uploadHandler = new UploadHandlerRaw(frame);
        req.SetRequestHeader("Content-Type", "application/octet-stream");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {

            // Turn server response into a Texture2D for visual check ----
            byte[] resp = req.downloadHandler.data;
            Texture2D serverTex = new Texture2D(2, 2);
            if (serverTex.LoadImage(resp))
            {
                OnTextureReceived.Invoke(serverTex);
            }
            else
            {
                Debug.LogWarning("Server response was not a valid PNG/JPEG");
            }
        }
        else
        {
            Debug.LogError($"HTTP error: {req.responseCode}  {req.error}");
        }
        busy = false;
    }
}
