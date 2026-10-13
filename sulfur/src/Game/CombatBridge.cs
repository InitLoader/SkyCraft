using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Stats;
using PerfectRandom.Sulfur.Core.Units;
using SulfurCraft.Configuration;
using SulfurCraft.Link;
using UnityEngine;

namespace SulfurCraft.Game
{
    internal sealed class CombatBridge
    {
        private readonly SharedLink link;
        private readonly WorldMapping world;
        private readonly PlayerBridge player;
        private readonly BridgeSettings settings;
        private readonly Dictionary<uint, Unit> units = new Dictionary<uint, Unit>();
        private float nextActors;
        public static CombatBridge Current;
        public int Hits { get; private set; }
        public CombatBridge(SharedLink link, WorldMapping world, PlayerBridge player, BridgeSettings settings)
        {
            this.link = link; this.world = world; this.player = player; this.settings = settings;
        }
        public void Update()
        {
            if (Time.unscaledTime >= nextActors)
            {
                nextActors = Time.unscaledTime + .1f; units.Clear();
                var actors = new List<Unit>();
                if (player.Active)
                    foreach (Unit unit in UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
                        if (unit != null && !unit.isPlayer && unit.IsAlive && unit.mainCollider != null && (unit.transform.position - player.Feet).sqrMagnitude < 96 * 96 * world.Units * world.Units)
                            actors.Add(unit);
                actors.Sort((a, b) => (a.transform.position - player.Feet).sqrMagnitude.CompareTo((b.transform.position - player.Feet).sqrMagnitude));
                uint seq = link.BeginWrite(Protocol.Actors);
                int count = Math.Min(256, actors.Count); link.Put(Protocol.Actors + 4, (uint)count);
                for (int i = 0; i < count; i++)
                {
                    Unit unit = actors[i]; uint id = unchecked((uint)unit.GetInstanceID()); units[id] = unit;
                    Bounds bounds = unit.mainCollider.bounds; Vector3 feet = world.ToMc(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                    long at = Protocol.Actors + 64 + i * 64;
                    link.Put(at, id); link.Put(at + 4, unit.IsHostileTo(player.Player.playerUnit) ? 1u : 0u);
                    link.PutFloat(at + 8, feet.x); link.PutFloat(at + 12, feet.y); link.PutFloat(at + 16, feet.z);
                    link.PutFloat(at + 20, unit.transform.eulerAngles.y + 180);
                    link.PutFloat(at + 24, Math.Max(.1f, Math.Max(bounds.size.x, bounds.size.z) / world.Units));
                    link.PutFloat(at + 28, Math.Max(.1f, bounds.size.y / world.Units)); link.PutFloat(at + 32, unit.normalizedHealth);
                    link.Put(at + 36, 1);
                    var name = new byte[24]; byte[] encoded = Encoding.UTF8.GetBytes(unit.name); Buffer.BlockCopy(encoded, 0, name, 0, Math.Min(23, encoded.Length)); link.Write(at + 40, name);
                }
                link.EndWrite(Protocol.Actors, seq);
            }
            link.DrainEvents(Receive);
        }
        private void Receive(McEvent ev)
        {
            if (!player.Active) return;
            if (ev.Type == 2) { player.Player.playerUnit.Die(); return; }
            if (ev.Type != 1 || ev.A <= 0 || float.IsNaN(ev.A) || float.IsInfinity(ev.A) || !units.TryGetValue(ev.Id, out Unit target) || target == null || !target.IsAlive) return;
            var source = new DamageSourceData(player.Player.playerUnit)
            {
                damageType = (ev.Flags & 8) != 0 ? DamageTypes.Fire : DamageTypes.Normal,
                melee = (ev.Flags & 2) == 0, isCritical = (ev.Flags & 1) != 0, critChance = 0
            };
            Vector3 point = world.FromMc(ev.B, ev.C, ev.D);
            target.ReceiveDamage(ev.A * settings.MeleeDamageMultiplier.Value, source, Hitmesh.Data.Default, point);
            Hits++;
        }
        public bool Hurt(Unit unit, float damage, DamageSourceData source, Hitmesh.Data hitbox)
        {
            if (!player.Active || unit != player.Player.playerUnit) return false;
            GameManager manager = GameManager.Instance;
            if (damage > 0 && !unit.isInvulnerable && !unit.DamageDisabledFromSettings && !hitbox.isInvulnerable && manager != null && !manager.InSafeZone && !manager.PlayerInvulnerable)
            {
                ushort kind = source.melee ? (ushort)0 : source.sourceWeapon != null ? (ushort)1 : (ushort)3;
                int amount = Mathf.RoundToInt(Mathf.Clamp(damage * settings.IncomingDamageMultiplier.Value * 500, 0, int.MaxValue));
                int attacker = source.sourceUnit != null ? source.sourceUnit.GetInstanceID() : 0;
                link.Input(Protocol.Hurt, kind, amount, attacker);
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Unit), nameof(Unit.ReceiveDamage), new[] { typeof(float), typeof(DamageSourceData), typeof(Hitmesh.Data), typeof(Vector3?) })]
    internal static class PlayerDamagePatch
    {
        private static bool Prefix(Unit __instance, float damage, DamageSourceData sourceData, Hitmesh.Data hitbox, ref bool __result)
        {
            if (CombatBridge.Current == null || !CombatBridge.Current.Hurt(__instance, damage, sourceData, hitbox)) return true;
            __result = false; return false;
        }
    }
}
