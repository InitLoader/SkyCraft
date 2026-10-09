using System;
using System.Collections.Generic;
using Cinemachine.PostFX;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuckovCraft.Rendering
{
    internal sealed class CameraEffectsBridge : IDisposable
    {
        private readonly Dictionary<DepthOfField, bool> effects = new Dictionary<DepthOfField, bool>();
        private GameCamera camera;
        private UniversalAdditionalCameraData cameraData;
        private float nextRefresh;
        public int Count => effects.Count;

        public void TakeOver(GameCamera value)
        {
            camera = value;
            cameraData = camera.renderCamera.GetComponent<UniversalAdditionalCameraData>();
            nextRefresh = 0;
            Apply();
        }
        public void Apply()
        {
            if (camera == null) return;
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 1;
                if (camera.mainVCam != null)
                    Track(camera.mainVCam.GetComponent<CinemachineVolumeSettings>()?.m_Profile);
                if (cameraData != null)
                    foreach (Volume volume in VolumeManager.instance.GetVolumes(cameraData.volumeLayerMask))
                        Track(volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile);
            }
            foreach (var entry in effects) if (entry.Key != null) entry.Key.active = false;
        }
        private void Track(VolumeProfile profile)
        {
            if (profile != null && profile.TryGet(out DepthOfField effect) && !effects.ContainsKey(effect))
                effects[effect] = effect.active;
        }
        public void Dispose()
        {
            foreach (var entry in effects) if (entry.Key != null) entry.Key.active = entry.Value;
            effects.Clear(); camera = null; cameraData = null;
        }
    }
}
