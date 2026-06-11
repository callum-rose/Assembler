using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Libraries
{
	/// <summary>
	/// First-class steering helpers for descriptor expressions, registered globally in
	/// CompiledExpressionsRegistry so every expression can call them by bare name (Seek, Flee, Arrive, …). Each
	/// movement function returns a desired-velocity <c>Vector3</c>, so they compose inside a <c>velocity: !expr</c>
	/// (or feed the <c>steering</c> aggregator behaviour). Positions/velocities are carried as <c>Vector3</c>
	/// (z = 0 for 2D), matching VectorMath; all numeric parameters are float so int arguments coerce
	/// automatically. <c>Wander</c> draws on the global RNG and is therefore non-deterministic, like RandomMath.
	/// </summary>
	public static class SteeringMath
	{
		/// <summary>Desired velocity that drives straight toward a target at full speed.</summary>
		/// <param name="position">Current position.</param>
		/// <param name="target">Point to steer toward.</param>
		/// <param name="maxSpeed">Maximum speed (units per second).</param>
		/// <returns>A velocity of length <paramref name="maxSpeed"/> pointing at the target (zero if already there).</returns>
		public static Vector3 Seek(Vector3 position, Vector3 target, float maxSpeed) =>
			(target - position).normalized * maxSpeed;

		/// <summary>Desired velocity that drives straight away from a target at full speed.</summary>
		/// <param name="position">Current position.</param>
		/// <param name="target">Point to steer away from.</param>
		/// <param name="maxSpeed">Maximum speed (units per second).</param>
		/// <returns>A velocity of length <paramref name="maxSpeed"/> pointing away from the target.</returns>
		public static Vector3 Flee(Vector3 position, Vector3 target, float maxSpeed) =>
			(position - target).normalized * maxSpeed;

		/// <summary>Like <see cref="Seek"/> but eases to a stop, scaling speed down inside the slowing radius.</summary>
		/// <param name="position">Current position.</param>
		/// <param name="target">Point to arrive at.</param>
		/// <param name="maxSpeed">Maximum speed (units per second).</param>
		/// <param name="slowingRadius">Distance from the target at which to begin slowing.</param>
		/// <returns>A velocity toward the target, ramped to zero as the target is reached.</returns>
		public static Vector3 Arrive(Vector3 position, Vector3 target, float maxSpeed, float slowingRadius)
		{
			var offset = target - position;
			var distance = offset.magnitude;

			if (distance < 1e-4f)
			{
				return Vector3.zero;
			}

			var speed = slowingRadius > 1e-4f ? maxSpeed * Mathf.Min(1f, distance / slowingRadius) : maxSpeed;
			return offset / distance * speed;
		}

		/// <summary>Seek the target's predicted future position, leading a moving target.</summary>
		/// <param name="position">Current position.</param>
		/// <param name="target">Target's current position.</param>
		/// <param name="targetVelocity">Target's current velocity.</param>
		/// <param name="maxSpeed">Maximum speed (units per second).</param>
		/// <returns>A velocity toward where the target is heading.</returns>
		public static Vector3 Pursue(Vector3 position, Vector3 target, Vector3 targetVelocity, float maxSpeed)
		{
			var lead = maxSpeed > 1e-4f ? Vector3.Distance(position, target) / maxSpeed : 0f;
			return Seek(position, target + targetVelocity * lead, maxSpeed);
		}

		/// <summary>Flee the target's predicted future position, dodging a moving threat.</summary>
		/// <param name="position">Current position.</param>
		/// <param name="target">Threat's current position.</param>
		/// <param name="targetVelocity">Threat's current velocity.</param>
		/// <param name="maxSpeed">Maximum speed (units per second).</param>
		/// <returns>A velocity away from where the threat is heading.</returns>
		public static Vector3 Evade(Vector3 position, Vector3 target, Vector3 targetVelocity, float maxSpeed)
		{
			var lead = maxSpeed > 1e-4f ? Vector3.Distance(position, target) / maxSpeed : 0f;
			return Flee(position, target + targetVelocity * lead, maxSpeed);
		}

		/// <summary>Nudge the current heading by a random jitter, for aimless roaming. Non-deterministic.</summary>
		/// <param name="velocity">Current velocity (its direction is the base heading).</param>
		/// <param name="maxSpeed">Speed of the returned velocity (units per second).</param>
		/// <param name="jitterDegrees">Maximum turn this step, in degrees either way.</param>
		/// <returns>A velocity of length <paramref name="maxSpeed"/> turned by a random amount.</returns>
		public static Vector3 Wander(Vector3 velocity, float maxSpeed, float jitterDegrees)
		{
			var heading = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.right;
			var turn = Random.Range(-jitterDegrees, jitterDegrees) * Mathf.Deg2Rad;
			var c = Mathf.Cos(turn);
			var s = Mathf.Sin(turn);
			return new Vector3(heading.x * c - heading.y * s, heading.x * s + heading.y * c, heading.z) * maxSpeed;
		}

		/// <summary>Repulsion velocity that pushes away from nearby neighbours, for flock/crowd separation.</summary>
		/// <param name="position">Current position.</param>
		/// <param name="neighbours">Positions of nearby entities.</param>
		/// <param name="separationRadius">Only neighbours within this distance contribute.</param>
		/// <param name="maxSpeed">Speed of the returned velocity (units per second).</param>
		/// <returns>A velocity steering away from crowding neighbours, or zero if none are close.</returns>
		public static Vector3 Separate(Vector3 position, List<Vector3> neighbours, float separationRadius, float maxSpeed)
		{
			var push = Vector3.zero;

			foreach (var neighbour in neighbours)
			{
				var offset = position - neighbour;
				var distance = offset.magnitude;

				// Weight by closeness so the nearest neighbours dominate; skip self/coincident and out-of-range.
				if (distance > 1e-4f && distance <= separationRadius)
				{
					push += offset / (distance * distance);
				}
			}

			return push.sqrMagnitude > 1e-6f ? push.normalized * maxSpeed : Vector3.zero;
		}

		/// <summary>Steer toward the average position (centre of mass) of nearby neighbours, for flock cohesion.</summary>
		/// <param name="position">Current position.</param>
		/// <param name="neighbours">Positions of nearby entities.</param>
		/// <param name="maxSpeed">Speed of the returned velocity (units per second).</param>
		/// <returns>A velocity steering toward the neighbours' centroid, or zero if there are none.</returns>
		public static Vector3 Cohesion(Vector3 position, List<Vector3> neighbours, float maxSpeed)
		{
			if (neighbours.Count == 0)
			{
				return Vector3.zero;
			}

			var centre = Vector3.zero;

			foreach (var neighbour in neighbours)
			{
				centre += neighbour;
			}

			centre /= neighbours.Count;
			return Seek(position, centre, maxSpeed);
		}

		/// <summary>Steer to match the average heading of nearby neighbours, for flock alignment.</summary>
		/// <param name="velocity">Current velocity (unused, kept for signature symmetry with the other rules).</param>
		/// <param name="neighbourVelocities">Velocities of nearby entities.</param>
		/// <param name="maxSpeed">Speed of the returned velocity (units per second).</param>
		/// <returns>A velocity matching the neighbours' average heading, or zero if there are none (or they cancel out).</returns>
		public static Vector3 Alignment(Vector3 velocity, List<Vector3> neighbourVelocities, float maxSpeed)
		{
			if (neighbourVelocities.Count == 0)
			{
				return Vector3.zero;
			}

			var average = Vector3.zero;

			foreach (var neighbourVelocity in neighbourVelocities)
			{
				average += neighbourVelocity;
			}

			return average.sqrMagnitude > 1e-6f ? average.normalized * maxSpeed : Vector3.zero;
		}

		/// <summary>Heading angle from one point toward another, in degrees CCW from +x, in [-180, 180].</summary>
		/// <param name="from">The origin point.</param>
		/// <param name="to">The point to face.</param>
		/// <returns>The 2D heading angle in degrees.</returns>
		public static float Heading2D(Vector3 from, Vector3 to) =>
			Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

		/// <summary>Euler rotation that faces from one point toward another in the XY plane (rotation about z).</summary>
		/// <param name="from">The origin point.</param>
		/// <param name="to">The point to aim at.</param>
		/// <returns>An euler-angles vector (0, 0, heading) suitable for an entity's Rotation.</returns>
		public static Vector3 LookRotation2D(Vector3 from, Vector3 to) =>
			new(0f, 0f, Heading2D(from, to));
	}
}
