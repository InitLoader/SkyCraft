using System;
using System.Collections.Generic;
using System.Text;
using Duckov.Utilities;
using DuckovCraft.Configuration;
using DuckovCraft.Link;
using UnityEngine;

namespace DuckovCraft.Game
{
    internal sealed class CombatBridge : IDisposable
    {
        private readonly SharedLink link;
        private readonly WorldMapping world;
        private readonly PlayerBridge player;
        private readonly BridgeSettings settings;
        private readonly Dictionary<uint, Health> actors = new Dictionary<uint, Health>();
        private readonly List<Health> sorted = new List<Health>();
        private readonly Collider[] nearby = new Collider[2048];
        private readonly Func<float, float> processDamage;
        private Health health;
        private bool nativeDeath;
        private float nextActors;
        public int Hits { get; private set; }
        public int ActorCount => actors.Count;

        public CombatBridge(SharedLink link, WorldMapping world, PlayerBridge player, BridgeSettings settings)
        {
            this.link = link; this.world = world; this.player = player; this.settings = settings;
            processDamage = ProcessDamage;
        }
        public void Update()
        {
            Health next = player.Active ? player.Player.Health : null;
            if (health != next)
            {
                Detach(); health = next;
                if (health != null) health.finalDamageProcessFuncs.Add(processDamage);
            }
            if (Time.unscaledTime >= nextActors)
            {
                nextActors = Time.unscaledTime + .1f; actors.Clear(); sorted.Clear();
                if (player.Active)
                {
                    int count = Physics.OverlapSphereNonAlloc(player.Feet, 96 * world.Units, nearby, GameplayDataSettings.Layers.damageReceiverLayerMask, QueryTriggerInteraction.Collide);
                    for (int i = 0; i < count; i++)
                    {
                        var receiver = nearby[i].GetComponentInParent<DamageReceiver>();
                        Health target = receiver != null ? receiver.health : null;
                        if (target == null || target == health || target.IsDead) continue;
                        uint id = unchecked((uint)target.GetInstanceID());
                        if (!actors.ContainsKey(id)) { actors.Add(id, target); sorted.Add(target); }
                    }
                    sorted.Sort((a, b) => (a.transform.position - player.Feet).sqrMagnitude.CompareTo((b.transform.position - player.Feet).sqrMagnitude));
                }
                uint seq = link.BeginWrite(Protocol.Actors);
                int total = Math.Min(256, sorted.Count); link.Put(Protocol.Actors + 4, (uint)total);
                for (int i = 0; i < total; i++)
                {
                    Health target = sorted[i]; CharacterMainControl character = target.TryGetCharacter();
                    Collider collider = character != null && character.mainDamageReceiver != null ? character.mainDamageReceiver.GetComponent<Collider>() : target.GetComponentInChildren<Collider>();
                    Bounds bounds = collider != null ? collider.bounds : new Bounds(target.transform.position + Vector3.up * .5f * world.Units, Vector3.one * world.Units);
                    Vector3 feet = world.ToMc(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                    long at = Protocol.Actors + 64 + i * 64;
                    link.Put(at, unchecked((uint)target.GetInstanceID()));
                    link.Put(at + 4, Team.IsEnemy(target.team, player.Player.Team) ? 1u : 0u);
                    link.PutFloat(at + 8, feet.x); link.PutFloat(at + 12, feet.y); link.PutFloat(at + 16, feet.z);
                    link.PutFloat(at + 20, target.transform.eulerAngles.y + 180);
                    link.PutFloat(at + 24, Math.Max(.1f, Math.Max(bounds.size.x, bounds.size.z) / world.Units));
                    link.PutFloat(at + 28, Math.Max(.1f, bounds.size.y / world.Units));
                    link.PutFloat(at + 32, target.CurrentHealth / Math.Max(1, target.MaxHealth)); link.Put(at + 36, 1);
                    var name = new byte[24]; byte[] encoded = Encoding.UTF8.GetBytes(target.name);
                    Buffer.BlockCopy(encoded, 0, name, 0, Math.Min(23, encoded.Length)); link.Write(at + 40, name);
                }
                link.EndWrite(Protocol.Actors, seq);
            }
            link.DrainEvents(Receive);
        }
        private float ProcessDamage(float damage)
        {
            if (nativeDeath || !player.Active || !link.Connected || damage <= 0) return damage;
            int amount = Mathf.RoundToInt(Mathf.Clamp(damage * settings.IncomingDamageMultiplier * 500, 0, int.MaxValue));
            return link.Input(Protocol.Hurt, 3, amount) ? 0 : damage;
        }
        private void Receive(McEvent ev)
        {
            if (!player.Active) return;
            if (ev.Type == 2)
            {
                nativeDeath = true;
                try
                {
                    player.Player.Health.Hurt(new DamageInfo(player.Player) { damageType = DamageTypes.realDamage, damageValue = player.Player.Health.MaxHealth + 1, ignoreArmor = true, ignoreDifficulty = true });
                }
                finally { nativeDeath = false; }
                return;
            }
            if (ev.Type != 1 || ev.A <= 0 || float.IsNaN(ev.A) || float.IsInfinity(ev.A) || !actors.TryGetValue(ev.Id, out Health target) || target == null || target.IsDead) return;
            var damage = new DamageInfo(player.Player)
            {
                damageValue = ev.A * settings.MeleeDamageMultiplier,
                damagePoint = world.FromMc(ev.B, ev.C, ev.D),
                critRate = (ev.Flags & 1) != 0 ? 1 : 0,
                critDamageFactor = 1
            };
            target.Hurt(damage); Hits++;
        }
        private void Detach()
        {
            if (health != null) health.finalDamageProcessFuncs.Remove(processDamage);
            health = null;
        }
        public void Dispose() => Detach();
    }
}
