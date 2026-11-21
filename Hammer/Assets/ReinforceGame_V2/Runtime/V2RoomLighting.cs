using UnityEngine;
using UnityEngine.Rendering;

namespace ReinforceGameV2
{
    public sealed class V2RoomLighting : MonoBehaviour
    {
        public bool blackoutTest = true;
        public Light[] interiorLights;
        public Color ambient = new Color(.025f, .03f, .035f);
        void OnEnable() => Apply();
        void OnValidate() { if (Application.isPlaying && isActiveAndEnabled) Apply(); }
        public void Apply()
        {
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = blackoutTest ? Color.black : ambient;
            RenderSettings.ambientIntensity = blackoutTest ? 0 : 1;
            RenderSettings.reflectionIntensity = 0;
            RenderSettings.fog = false;
            if (interiorLights == null) return;
            foreach (var lamp in interiorLights) if (lamp) lamp.enabled = !blackoutTest;
        }
    }
}
