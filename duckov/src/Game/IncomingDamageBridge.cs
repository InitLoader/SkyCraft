using System;
using System.Collections.Generic;
using Duckov.Utilities;
using DuckovCraft.Configuration;
using DuckovCraft.Link;
using ItemStatsSystem;
using UnityEngine;

namespace DuckovCraft.Game
{
    internal sealed class IncomingDamageBridge : IDisposable
    {
        private readonly SharedLink link;
        private readonly PlayerBridge player;
        private readonly BridgeSettings settings;
        private readonly Func<float, float> process;
        private readonly Stack<float> pending = new Stack<float>();
        private Health health;
        private bool applyingNative;
        public int Forwarded { get; private set; }
        public int Directional { get; private set; }

        public IncomingDamageBridge(SharedLink link, PlayerBridge player, BridgeSettings settings)
        {
            this.link = link; this.player = player; this.settings = settings; process = Process;
        }
        public void Attach(Health next)
        {
            if (ReferenceEquals(health, next)) return;
            Dispose(); health = next;
            if (health == null) return;
            health.finalDamageProcessFuncs.Add(process); Health.OnHurt += Complete;
        }
        private float Process(float damage)
        {
            if (applyingNative || !player.Active || !link.Connected || damage <= 0) return damage;
            pending.Push(damage); return 0;
        }
        private void Complete(Health target, DamageInfo info)
        {
            if (target != health || applyingNative || pending.Count == 0) return;
            float damage = pending.Pop();
            ushort kind = Protocol.HurtOther;
            int attacker = 0;
            if (!info.isFromBuffOrEffect && !info.isExplosion && info.damageType != DamageTypes.realDamage && info.fromCharacter != null)
            {
                Health source = info.fromCharacter.Health;
                if (source != null) attacker = source.GetInstanceID();
                var tags = info.fromWeaponItemID > 0 ? ItemAssetsCollection.GetMetaData(info.fromWeaponItemID).tags : null;
                kind = tags != null && Array.IndexOf(tags, GameplayDataSettings.Tags.Gun) >= 0 ? Protocol.HurtProjectile : Protocol.HurtMelee;
            }
            int amount = Mathf.RoundToInt(Mathf.Clamp(damage * settings.IncomingDamageMultiplier * 500, 0, int.MaxValue));
            if (link.Input(Protocol.Hurt, kind, amount, attacker))
            {
                Forwarded++; if (kind != Protocol.HurtOther && attacker != 0) Directional++;
            }
            else ApplyNative(new DamageInfo(info.fromCharacter) { damageType = DamageTypes.realDamage, damageValue = damage, ignoreArmor = true, ignoreDifficulty = true });
        }
        public void ApplyNative(DamageInfo info)
        {
            bool previous = applyingNative; applyingNative = true;
            try { health?.Hurt(info); }
            finally { applyingNative = previous; }
        }
        public void Dispose()
        {
            Health.OnHurt -= Complete;
            if (health != null) health.finalDamageProcessFuncs.Remove(process);
            health = null; pending.Clear();
        }
    }
}
