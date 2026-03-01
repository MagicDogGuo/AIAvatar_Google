// 麥克風橋接：一般平台用 UnityEngine.Microphone，WebGL 用 jslib
using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class MicrophoneBridge
{
    public const string WebGLDeviceName = "WebGL Microphone";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int MicrophoneWebGL_Start(int sampleRate);
    [DllImport("__Internal")]
    private static extern void MicrophoneWebGL_Stop();
    [DllImport("__Internal")]
    private static extern int MicrophoneWebGL_IsRecording();
    [DllImport("__Internal")]
    private static extern int MicrophoneWebGL_GetPosition();
    [DllImport("__Internal")]
    private static extern int MicrophoneWebGL_GetSampleRate();
    [DllImport("__Internal")]
    private static extern int MicrophoneWebGL_GetLatestSamples(IntPtr heapPtr, int numSamples);

    private static int _webglLastPosition;
    private static int _webglSampleRate = 48000;

    public static string[] devices
    {
        get { return new[] { WebGLDeviceName }; }
    }

    public static bool IsRecording(string deviceName)
    {
        if (deviceName != WebGLDeviceName) return false;
        return MicrophoneWebGL_IsRecording() != 0;
    }

    public static int GetPosition(string deviceName)
    {
        if (deviceName != WebGLDeviceName) return 0;
        return MicrophoneWebGL_GetPosition();
    }

    public static void GetDeviceCaps(string deviceName, out int minFreq, out int maxFreq)
    {
        minFreq = 44100;
        maxFreq = 48000;
    }

    public static AudioClip Start(string deviceName, bool loop, int lengthSec, int frequency)
    {
        if (deviceName != WebGLDeviceName) return null;
        _webglSampleRate = frequency;
        MicrophoneWebGL_Start(frequency);
        return AudioClip.Create("WebGL_Mic", lengthSec * frequency, 1, frequency, false);
    }

    public static void End(string deviceName)
    {
        if (deviceName == WebGLDeviceName)
            MicrophoneWebGL_Stop();
    }

    /// <summary> 從 WebGL 緩衝區讀取最新音訊到 float 陣列（用於即時播放或 STT） </summary>
    public static int ReadLatestSamples(float[] buffer)
    {
        if (buffer == null || buffer.Length == 0) return 0;
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            int n = MicrophoneWebGL_GetLatestSamples(handle.AddrOfPinnedObject(), buffer.Length);
            return n;
        }
        finally
        {
            handle.Free();
        }
    }

    public static bool IsWebGL => true;
#else
    public static string[] devices => Microphone.devices;
    public static bool IsRecording(string deviceName) => Microphone.IsRecording(deviceName);
    public static int GetPosition(string deviceName) => Microphone.GetPosition(deviceName);
    public static void GetDeviceCaps(string deviceName, out int minFreq, out int maxFreq) => Microphone.GetDeviceCaps(deviceName, out minFreq, out maxFreq);
    public static AudioClip Start(string deviceName, bool loop, int lengthSec, int frequency) => Microphone.Start(deviceName, loop, lengthSec, frequency);
    public static void End(string deviceName) => Microphone.End(deviceName);
    public static int ReadLatestSamples(float[] buffer) => 0;
    public static bool IsWebGL => false;
#endif
}
