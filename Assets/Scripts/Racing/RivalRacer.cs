using System.Collections.Generic;
using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// A holographic rival that runs the course at a fixed pace (its finish time is known in advance).
    /// It ignores hazards and never collides with the player; it follows the ground height under it,
    /// rides moving platforms and hops over gaps.
    /// </summary>
    public class RivalRacer : MonoBehaviour
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[12];

        private GameManager gm;
        private RunnerAvatar avatar;
        private List<Vector3> path;
        private float[] cumulative;
        private float length;
        private float speed;
        private float laneX;

        private float distance;
        private float y;
        private float lastGroundY;
        private float airTime;
        private bool finished;

        public string Label { get; private set; }
        public Color Tint { get; private set; }

        /// <summary>Course progress 0..1 (by distance along the path).</summary>
        public float Progress01 { get { return length > 0f ? Mathf.Clamp01(distance / length) : 0f; } }

        /// <summary>Running time at which this rival reaches the finish (seconds).</summary>
        public float PredictedFinishTime { get; private set; }

        public bool Finished { get { return finished; } }

        public void Bind(GameManager manager, List<Vector3> pathPoints, float finishSeconds, float lane, string label,
                         Color tint, Material bodyMaterial, Material glowMaterial)
        {
            gm = manager;
            path = pathPoints;
            laneX = lane;
            Label = label;
            Tint = tint;

            cumulative = new float[path.Count];
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = path[i - 1];
                Vector3 b = path[i];
                cumulative[i] = cumulative[i - 1] + Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            }
            length = cumulative[cumulative.Length - 1];

            PredictedFinishTime = Mathf.Max(5f, finishSeconds);
            speed = length / PredictedFinishTime;

            Material ghost = RunnerAvatar.MakeGhostMaterial(tint, glowMaterial != null ? glowMaterial : bodyMaterial);
            avatar = RunnerAvatar.Create(transform, "Avatar", ghost, ghost, null);

            Vector3 start = path[0];
            y = start.y;
            lastGroundY = y;
            transform.position = new Vector3(start.x + laneX, y, start.z);
            transform.rotation = Quaternion.identity;
        }

        private void Update()
        {
            if (path == null || gm == null) return;

            float dt = Time.deltaTime;
            bool racing = gm.State == GameState.Running && !finished;

            if (racing)
            {
                distance += speed * dt;
                if (distance >= length)
                {
                    distance = length;
                    finished = true;
                    avatar.SetVictory(true);
                }
            }

            Vector3 point;
            Vector3 direction;
            Evaluate(distance, out point, out direction);

            // Ground follow
            float x = point.x + laneX;
            float ground;
            bool grounded = FindGround(x, point.z, out ground);
            if (!grounded)
            {
                x = point.x;
                grounded = FindGround(x, point.z, out ground);
            }

            if (grounded)
            {
                airTime = 0f;
                lastGroundY = ground;
                y = Mathf.MoveTowards(y, ground, 30f * dt);
            }
            else
            {
                airTime += racing ? dt : 0f;
                float k = Mathf.Clamp01(airTime / 1.2f);
                y = lastGroundY + Mathf.Sin(k * Mathf.PI) * 1.8f;
            }

            transform.position = new Vector3(x, y, point.z);

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-10f * dt));
            }

            float speed01 = racing ? Mathf.Clamp01(speed / 6f) : 0f;
            avatar.SetMotion(speed01, grounded || !racing);
        }

        private void Evaluate(float d, out Vector3 point, out Vector3 direction)
        {
            int last = path.Count - 1;
            if (d >= length)
            {
                point = path[last];
                direction = (path[last] - path[last - 1]);
                direction.y = 0f;
                return;
            }

            int segment = 0;
            while (segment < last - 1 && cumulative[segment + 1] < d) segment++;

            Vector3 a = path[segment];
            Vector3 b = path[segment + 1];
            float span = Mathf.Max(0.001f, cumulative[segment + 1] - cumulative[segment]);
            float u = Mathf.Clamp01((d - cumulative[segment]) / span);

            point = Vector3.Lerp(a, b, u);
            direction = b - a;
            direction.y = 0f;
        }

        /// <summary>Highest solid, non-hazard surface under (x, z), ignoring the player and triggers.</summary>
        private bool FindGround(float x, float z, out float groundY)
        {
            groundY = 0f;
            Vector3 origin = new Vector3(x, y + 3f, z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, Hits, 14f, ~0, QueryTriggerInteraction.Ignore);

            bool found = false;
            float best = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider c = Hits[i].collider;
                if (c is CharacterController) continue;
                if (c.GetComponentInParent<Hazard>() != null) continue;

                float hitY = Hits[i].point.y;
                if (hitY > best)
                {
                    best = hitY;
                    found = true;
                }
            }

            if (found) groundY = best;
            return found;
        }
    }
}
