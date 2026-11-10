using UnityEngine;

namespace Game.Effects
{
    public static class Easing
    {
        public static float Linear(float t) => t;

        public static float QuadIn(float t) => t * t;
        public static float QuadOut(float t) => 1f - (1f - t) * (1f - t);
        public static float QuadInOut(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        public static float CubicIn(float t) => t * t * t;
        public static float CubicOut(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float CubicInOut(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        public static float SineIn(float t) => 1f - Mathf.Cos(t * Mathf.PI / 2f);
        public static float SineOut(float t) => Mathf.Sin(t * Mathf.PI / 2f);
        public static float SineInOut(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

        public static float ExpoIn(float t) => t == 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
        public static float ExpoOut(float t) => t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
        public static float ExpoInOut(float t)
        {
            if (t == 0f) return 0f;
            if (t == 1f) return 1f;
            return t < 0.5f 
                ? Mathf.Pow(2f, 20f * t - 10f) / 2f 
                : (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;
        }

        public static float BackIn(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        public static float BackOut(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float ElasticOut(float t)
        {
            const float c4 = (2f * Mathf.PI) / 3f;
            if (t == 0f) return 0f;
            if (t == 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        public static float BounceOut(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
            {
                return n1 * t * t;
            }
            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }
            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }

        public static float Pulse(float t, float frequency = 1f)
        {
            return (Mathf.Sin(t * frequency * Mathf.PI * 2f) + 1f) / 2f;
        }

        public static float PingPong(float t)
        {
            return t < 0.5f ? t * 2f : 2f - t * 2f;
        }

        public static float Spike(float t, float peak = 0.5f)
        {
            return t < peak ? t / peak : (1f - t) / (1f - peak);
        }
    }
}