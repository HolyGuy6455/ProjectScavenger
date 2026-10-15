using UnityEngine;

namespace Scavenger.Audio
{
    /// <summary>Finds the ground point at the centre of the gameplay camera's view.</summary>
    public static class SoundListenerFocus
    {
        public static Vector3 Calculate(Camera camera, Vector3 playerPosition)
        {
            if (camera == null)
                return playerPosition;

            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            // Use the player's ground height, so ceilings/walls cannot move the listening point.
            // A level/upward-facing camera has no useful ground focus.
            if (ray.direction.y >= -0.0001f)
                return playerPosition;
            var ground = new Plane(Vector3.up, playerPosition);
            return ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : playerPosition;
        }
    }
}
