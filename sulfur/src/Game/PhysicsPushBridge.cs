using System.Collections.Generic;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.Units;
using SulfurCraft.Configuration;
using SulfurCraft.World;
using UnityEngine;

namespace SulfurCraft.Game
{
    internal sealed class PhysicsPushBridge
    {
        private readonly PlayerBridge player;
        private readonly WorldMapping world;
        private readonly BridgeSettings settings;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly HashSet<Rigidbody> pushed = new HashSet<Rigidbody>();
        public int Pushes { get; private set; }
        public PhysicsPushBridge(PlayerBridge player, WorldMapping world, BridgeSettings settings) { this.player = player; this.world = world; this.settings = settings; }

        public void FixedUpdate(GameManager manager, CollisionExporter collision)
        {
            if (!player.MotionReady || player.ScreenOpen || !Application.isFocused || manager == null || manager.gameState != GameState.Running || manager.HasLock(GameManager.PlayerLocks.PlayerMovement)) return;
            var state = player.State;
            if (state.BodyWidth <= 0 || state.BodyHeight <= 0 || state.TickMs <= 0) return;
            Vector3 velocity = new Vector3(state.MoveX, 0, -state.MoveZ) * (world.Units * 1000 / state.TickMs);
            if (!Finite(velocity.x) || !Finite(velocity.z) || velocity.sqrMagnitude < .0001f) return;
            Vector3 feet = world.FromMc(state.CurX, state.CurY, state.CurZ);
            float radius = state.BodyWidth * world.Units * .5f, height = Mathf.Max(radius * 2, state.BodyHeight * world.Units);
            float contact = Physics.defaultContactOffset;
            int count = Physics.CapsuleCastNonAlloc(feet + Vector3.up * radius, feet + Vector3.up * (height - radius), radius,
                velocity.normalized, hits, velocity.magnitude * Time.fixedDeltaTime + contact * 2, manager.geometryMask, QueryTriggerInteraction.Ignore);
            pushed.Clear();
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i]; Rigidbody body = hit.rigidbody;
                if (body == null || body.isKinematic || body.GetComponentInParent<Unit>() != null || !pushed.Add(body)) continue;
                Vector3 direction = -hit.normal; direction.y = 0;
                if (direction.sqrMagnitude < .01f) continue;
                direction.Normalize();
                float closing = Vector3.Dot(velocity - body.GetPointVelocity(hit.point), direction);
                if (closing <= 0) continue;
                body.AddForceAtPosition(direction * (closing * player.BodyMass * settings.PushStrength.Value), hit.point, ForceMode.Impulse);
                collision.Invalidate(hit.collider.bounds); Pushes++;
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
