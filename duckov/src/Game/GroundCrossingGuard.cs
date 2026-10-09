using UnityEngine;

namespace DuckovCraft.Game
{
    internal static class GroundCrossingGuard
    {
        public static bool TryCatch(Vector3 previous, Vector3 proposed, int mask, float units, out Vector3 landed)
        {
            landed = proposed;
            if (proposed.y >= previous.y || mask == 0) return false;
            float skin = .025f * units;
            Vector3 origin = new Vector3(proposed.x, previous.y + skin, proposed.z);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, previous.y - proposed.y + skin, mask, QueryTriggerInteraction.Ignore)
                || hit.normal.y < .7f || hit.collider.GetComponentInParent<CharacterMainControl>() != null || hit.point.y <= proposed.y + .001f * units)
                return false;
            landed.y = hit.point.y;
            return true;
        }
    }
}
