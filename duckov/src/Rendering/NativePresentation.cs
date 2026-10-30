using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DuckovCraft.Rendering
{
    internal sealed class NativePresentation : IDisposable
    {
        private readonly Dictionary<Renderer, bool> renderers = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Behaviour, bool> lasers = new Dictionary<Behaviour, bool>();
        private readonly Dictionary<GameObject, bool> markers = new Dictionary<GameObject, bool>();
        private CharacterMainControl player;
        public int LaserCount => lasers.Count;

        public void TakeOver(CharacterMainControl value)
        {
            player = value;
            player.OnHoldAgentChanged += TrackAgent;
            Track(player.gameObject);
            Apply();
        }
        private void TrackAgent(DuckovItemAgent agent)
        {
            if (agent != null) Track(agent.gameObject);
        }
        private void Track(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (!renderers.ContainsKey(renderer)) renderers[renderer] = renderer.enabled;
            foreach (Accessory_Lazer laser in root.GetComponentsInChildren<Accessory_Lazer>(true)) TrackLaser(laser);
            foreach (TecLazer laser in root.GetComponentsInChildren<TecLazer>(true)) TrackLaser(laser);
        }
        private void TrackLaser(Behaviour laser)
        {
            if (lasers.ContainsKey(laser)) return;
            lasers[laser] = laser.enabled;
            var line = laser.GetType().GetField("lineRenderer", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(laser) as Renderer;
            if (line != null && !renderers.ContainsKey(line)) renderers[line] = line.enabled;
            var marker = laser.GetType().GetField("hitMarker", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(laser) as GameObject;
            if (marker != null && !markers.ContainsKey(marker)) markers[marker] = marker.activeSelf;
        }
        public void Apply()
        {
            foreach (var entry in lasers) if (entry.Key != null) entry.Key.enabled = false;
            foreach (var entry in markers) if (entry.Key != null) entry.Key.SetActive(false);
            foreach (var entry in renderers) if (entry.Key != null) entry.Key.enabled = false;
        }
        public void Dispose()
        {
            if (player != null) player.OnHoldAgentChanged -= TrackAgent;
            foreach (var entry in renderers) if (entry.Key != null) entry.Key.enabled = entry.Value;
            foreach (var entry in markers) if (entry.Key != null) entry.Key.SetActive(entry.Value);
            foreach (var entry in lasers) if (entry.Key != null) entry.Key.enabled = entry.Value;
            renderers.Clear(); markers.Clear(); lasers.Clear(); player = null;
        }
    }
}
