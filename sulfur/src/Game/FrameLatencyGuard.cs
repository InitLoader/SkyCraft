using System;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SulfurCraft.Game
{
    internal sealed class FrameLatencyGuard : IDisposable
    {
        private readonly ManualLogSource log;
        private bool active;
        private int queuedFrames;
        private InputSettings.UpdateMode inputMode;
        public FrameLatencyGuard(ManualLogSource log) { this.log = log; }

        public void Update(bool enabled)
        {
            if (enabled == active) return;
            if (enabled)
            {
                inputMode = InputSystem.settings.updateMode; queuedFrames = QualitySettings.maxQueuedFrames;
                if (inputMode == InputSettings.UpdateMode.ProcessEventsInFixedUpdate)
                    InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                QualitySettings.maxQueuedFrames = 1; active = true;
                log.LogInfo($"Input timing: {inputMode} -> {InputSystem.settings.updateMode}; queued frames {queuedFrames} -> 1; vSync={QualitySettings.vSyncCount}");
            }
            else Dispose();
        }
        public void Dispose()
        {
            if (!active) return;
            InputSystem.settings.updateMode = inputMode; QualitySettings.maxQueuedFrames = queuedFrames; active = false;
        }
    }
}
