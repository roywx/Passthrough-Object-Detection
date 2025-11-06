using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;
using Vector3 = UnityEngine.Vector3;

namespace Trev3d.Quest.ScreenCapture
{
	[DefaultExecutionOrder(-1000)]
	public class QuestScreenCaptureTextureManager : MonoBehaviour
	{
		private AndroidJavaObject byteBuffer;
		private unsafe sbyte* imageData;
		private int bufferSize;
		public static QuestScreenCaptureTextureManager Instance { get; private set; }

		private AndroidJavaClass UnityPlayer;
		private AndroidJavaObject UnityPlayerActivityWithMediaProjector;

		public bool startScreenCaptureOnStart = true;

		public UnityEvent<Texture2D> OnTextureInitialized = new();
		public UnityEvent OnScreenCaptureStarted = new();
		public UnityEvent OnScreenCapturePermissionDeclined = new();
		public UnityEvent OnScreenCaptureStopped = new();
		public UnityEvent OnNewFrameIncoming = new();
		public UnityEvent<byte[]> OnNewFrame = new UnityEvent<byte[]>();


		public static readonly Vector2Int Size = new(640, 640);

		private byte[] rgbImageData;
		private Texture2D debugTexture;

		[SerializeField] private DataCommunicator dataCommunicator;

		private void Awake()
		{
			Instance = this;
			rgbImageData = new byte[Size.y * Size.x * 3];
			debugTexture = new Texture2D(Size.x, Size.y, TextureFormat.RGB24, false);
		}

		private void Start()
		{
			Debug.Log("screen cap start");
			UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			UnityPlayerActivityWithMediaProjector = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

			if (startScreenCaptureOnStart)
			{
				Debug.Log("screen cap starting..");
				StartScreenCapture();
			}
			bufferSize = Size.x * Size.y * 4; // RGBA_8888 format: 4 bytes per pixel
		}

		private unsafe void InitializeByteBufferRetrieved()
		{
			// Retrieve the ByteBuffer from Java and cache it
			byteBuffer = UnityPlayerActivityWithMediaProjector.Call<AndroidJavaObject>("getLastFrameBytesBuffer");

			// Get the memory address of the direct ByteBuffer
			imageData = AndroidJNI.GetDirectBufferAddress(byteBuffer.GetRawObject());
		}

		public void StartScreenCapture()
		{
			UnityPlayerActivityWithMediaProjector.Call("startScreenCaptureWithPermission", gameObject.name, Size.x, Size.y);
		}

		public void StopScreenCapture()
		{
			UnityPlayerActivityWithMediaProjector.Call("stopScreenCapture");
		}

		// Messages sent from android activity

		private void ScreenCaptureStarted()
		{
			OnScreenCaptureStarted.Invoke();
			InitializeByteBufferRetrieved();
		}

		private void ScreenCapturePermissionDeclined()
		{
			OnScreenCapturePermissionDeclined.Invoke();
		}

		private void NewFrameIncoming()
		{
			OnNewFrameIncoming.Invoke();
		}

		private unsafe void NewFrameAvailable()
		{
			if (imageData == default) return;

			for (int y = 0; y < Size.y; y++)
			{
				int flipY = Size.y - 1 - y;
				for (int x = 0; x < Size.x; x++)
				{
					int src = (flipY * Size.x + x) * 4;   // RGBA
					int dst = (y * Size.x + x) * 3;   // RGB
					// int srcIndex = y * Size.x + x;
					// int destIndex = y * Size.x + x;

					byte r = (byte)imageData[src];     // R
					byte g = (byte)imageData[src + 1]; // G
					byte b = (byte)imageData[src + 2]; // B
					rgbImageData[dst] = r;
					rgbImageData[dst + 1] = g;
					rgbImageData[dst + 2] = b;
				}
			}

			debugTexture.LoadRawTextureData(rgbImageData);
			debugTexture.Apply();

			OnNewFrame.Invoke(rgbImageData);
			dataCommunicator.TrySendFrame(rgbImageData);
		}

		private void ScreenCaptureStopped()
		{
			OnScreenCaptureStopped.Invoke();
		}
	}
}