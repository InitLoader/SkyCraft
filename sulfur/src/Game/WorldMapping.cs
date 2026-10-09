using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SulfurCraft.Configuration;
using PerfectRandom.Sulfur.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SulfurCraft.Game
{
    internal sealed class WorldMapping
    {
        private readonly BridgeSettings settings;
        private readonly string path;
        private readonly Dictionary<string, int> slots = new Dictionary<string, int>();
        private string key;
        public uint WorldId { get; private set; }
        public uint Epoch { get; private set; }
        public int OriginX { get; private set; }
        public float Units => settings.UnitsPerBlock.Value;

        public WorldMapping(BridgeSettings settings, string directory)
        {
            this.settings = settings;
            path = Path.Combine(directory, "SulfurCraft.worlds.tsv");
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadAllLines(path))
            {
                string[] parts = line.Split('\t');
                if (parts.Length == 2 && int.TryParse(parts[1], out int slot) && slot > 0 && slot < 3500)
                    slots[Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]))] = slot;
            }
        }

        public bool Select(GameManager manager)
        {
            string next = (manager.currentEnvironment != null ? manager.currentEnvironment.id.ToString() : "scene")
                + ":" + manager.currentLevelIndex + ":" + manager.currentSeed + ":" + SceneManager.GetActiveScene().name;
            if (next == key) return false;
            key = next;
            if (!slots.TryGetValue(key, out int slot))
            {
                slot = 1;
                foreach (int used in slots.Values) slot = Math.Max(slot, used + 1);
                if (slot >= 3500) throw new InvalidOperationException("SulfurCraft world slot limit reached.");
                slots.Add(key, slot);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var writer = new StreamWriter(path, false, Encoding.UTF8))
                    foreach (var entry in slots)
                        writer.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.Key)) + "\t" + entry.Value);
            }
            OriginX = slot * 8192;
            WorldId = (uint)slot;
            Epoch++;
            return true;
        }

        public Vector3 ToMc(Vector3 unity) => new Vector3(unity.x / Units + OriginX, unity.y / Units + 128, -unity.z / Units);
        public Vector3 FromMc(double x, double y, double z) => new Vector3((float)((x - OriginX) * Units), (float)((y - 128) * Units), (float)(-z * Units));
        public Vector3 FromMc(Vector3 mc) => FromMc(mc.x, mc.y, mc.z);
    }
}
