using System.IO;
using HarmonyLib;
using PerfectRandom.Sulfur.Core.World;
using SulfurCraft.Game;
using SulfurCraft.Link;
using UnityEngine;

namespace SulfurCraft.World
{
    internal sealed class LadderExporter
    {
        private readonly WorldMapping world;
        private readonly Collider[] colliders = new Collider[256];
        private byte[] previous;
        private float nextPoll;
        public static PlayerBridge Player;
        public int Count { get; private set; }
        public LadderExporter(WorldMapping world) { this.world = world; }
        public void Reset() { previous = null; nextPoll = 0; Count = 0; }

        public void Update(SharedLink link, Vector3 feet)
        {
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .1f;
            int found = Physics.OverlapBoxNonAlloc(feet + Vector3.up * (6 * world.Units), new Vector3(6, 10, 6) * world.Units, colliders, Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Collide);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(world.Epoch); writer.Write(0);
                Count = 0;
                for (int i = 0; i < found; i++)
                {
                    Collider collider = colliders[i];
                    if (!collider.isTrigger || collider.GetComponentInParent<Ladder>() == null) continue;
                    Vector3 a = world.ToMc(collider.bounds.min), b = world.ToMc(collider.bounds.max);
                    Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);
                    writer.Write(min.x); writer.Write(min.y); writer.Write(min.z); writer.Write(max.x); writer.Write(max.y); writer.Write(max.z); Count++;
                }
                stream.Position = 4; writer.Write(Count);
                byte[] payload = stream.ToArray();
                bool changed = previous == null || previous.Length != payload.Length;
                if (!changed) for (int i = 0; i < payload.Length; i++) if (payload[i] != previous[i]) { changed = true; break; }
                if (changed && link.Collision(Protocol.CollisionLadders, payload)) previous = payload;
            }
        }
    }

    // Native ladder code writes Rigidbody velocity; the bridge keeps native walkers disabled.
    // While linked, Minecraft handles these volumes using its own climbing physics.
    [HarmonyPatch(typeof(Ladder), "Update")]
    internal static class NativeLadderUpdate
    {
        private static bool Prefix() => LadderExporter.Player == null || !LadderExporter.Player.Active;
    }

}
