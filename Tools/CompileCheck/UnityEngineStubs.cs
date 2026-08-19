// Minimal UnityEngine stubs — just enough surface for the Unity-independent
// core files (Compiler_S, Dialogue_Database, Dialogue_Service, File_S) to
// compile outside Unity. The test program can drive Time.time directly.
namespace UnityEngine
{
    public static class Debug
    {
        public static bool Verbose = false;

        public static void Log(object message)
        {
            if (Verbose) System.Console.WriteLine("[Log] " + message);
        }

        public static void LogWarning(object message)
        {
            System.Console.WriteLine("[Warn] " + message);
        }

        public static void LogError(object message)
        {
            System.Console.WriteLine("[Error] " + message);
        }
    }

    public static class Time
    {
        public static float time;
        public static float unscaledTime;
    }

    public static class Mathf
    {
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static float Abs(float a) { return a < 0f ? -a : a; }
        public static int FloorToInt(float f) { return (int)System.Math.Floor(f); }
        public static int RoundToInt(float f) { return (int)System.Math.Round(f, System.MidpointRounding.AwayFromZero); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float Clamp01(float value) { return value < 0f ? 0f : (value > 1f ? 1f : value); }
        public static float Sin(float f) { return (float)System.Math.Sin(f); }
    }
}
