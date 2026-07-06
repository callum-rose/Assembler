using UnityEngine;

namespace Assembler.Libraries
{
	/// <summary>
	/// First-class 2D/3D vector helpers for descriptor expressions. Registered globally
	/// in CompiledExpressionsRegistry so every expression can call these by bare name
	/// (ScaleVector, Distance, Rotate2D, IntegratePosition, ...). Vectors are carried as
	/// Vector3 (z = 0 for 2D), matching GridMath. All numeric parameters are float so int
	/// arguments coerce automatically during overload resolution. Method names are chosen
	/// to avoid colliding with the scalar NumberMath helpers (e.g. LerpVector, not Lerp).
	/// </summary>
	public static class VectorMath
	{
		/// <summary>Multiply a vector by a scalar.</summary>
		/// <param name="v">The vector.</param>
		/// <param name="k">The scalar factor.</param>
		/// <returns>The scaled vector.</returns>
		public static Vector3 ScaleVector(Vector3 v, float k) => new(v.x * k, v.y * k, v.z * k);

		/// <summary>Add two vectors.</summary>
		/// <param name="a">First vector.</param>
		/// <param name="b">Second vector.</param>
		/// <returns>The component-wise sum a + b.</returns>
		public static Vector3 AddVector(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);

		/// <summary>Subtract one vector from another.</summary>
		/// <param name="a">The minuend.</param>
		/// <param name="b">The subtrahend.</param>
		/// <returns>The component-wise difference a - b.</returns>
		public static Vector3 SubtractVector(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);

		/// <summary>Euclidean length of a vector.</summary>
		/// <param name="v">The vector.</param>
		/// <returns>The magnitude (length) of the vector.</returns>
		public static float Magnitude(Vector3 v) => v.magnitude;

		/// <summary>A unit-length vector in the same direction (zero vector returns zero).</summary>
		/// <param name="v">The vector to normalize.</param>
		/// <returns>The normalized vector.</returns>
		public static Vector3 Normalize(Vector3 v) => v.normalized;

		/// <summary>Straight-line distance between two points.</summary>
		/// <param name="a">First point.</param>
		/// <param name="b">Second point.</param>
		/// <returns>The Euclidean distance between a and b.</returns>
		public static float Distance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

		/// <summary>Unit vector pointing from one point toward another.</summary>
		/// <param name="from">The starting point.</param>
		/// <param name="to">The target point.</param>
		/// <returns>The normalized direction from <paramref name="from"/> to <paramref name="to"/>.</returns>
		public static Vector3 Direction(Vector3 from, Vector3 to) => (to - from).normalized;

		/// <summary>Dot product of two vectors.</summary>
		/// <param name="a">First vector.</param>
		/// <param name="b">Second vector.</param>
		/// <returns>The dot product a · b.</returns>
		public static float Dot(Vector3 a, Vector3 b) => Vector3.Dot(a, b);

		/// <summary>Linear interpolation between two vectors (t clamped to [0, 1]).</summary>
		/// <param name="a">Start vector (t = 0).</param>
		/// <param name="b">End vector (t = 1).</param>
		/// <param name="t">Interpolation factor; clamped to [0, 1].</param>
		/// <returns>The interpolated vector.</returns>
		public static Vector3 LerpVector(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, t);

		/// <summary>
		/// Rotate a 2D vector (x, y) counter-clockwise about the origin by an angle in
		/// degrees. The z component is preserved. Replaces hand-rolled cos/sin rotation
		/// matrices in descriptor expressions.
		/// </summary>
		/// <param name="v">The vector to rotate (x, y used; z preserved).</param>
		/// <param name="degrees">Counter-clockwise rotation angle in degrees.</param>
		/// <returns>The rotated vector.</returns>
		public static Vector3 Rotate2D(Vector3 v, float degrees)
		{
			float rad = degrees * Mathf.Deg2Rad;
			float c = Mathf.Cos(rad);
			float s = Mathf.Sin(rad);
			return new Vector3(v.x * c - v.y * s, v.x * s + v.y * c, v.z);
		}

		/// <summary>Angle of a 2D vector in degrees, measured CCW from the +x axis, in [-180, 180].</summary>
		/// <param name="v">The vector (x, y used).</param>
		/// <returns>The heading angle in degrees.</returns>
		public static float Angle2D(Vector3 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

		/// <summary>Advance a position by a velocity over an explicit time step.</summary>
		/// <param name="pos">Current position.</param>
		/// <param name="vel">Velocity (units per second).</param>
		/// <param name="dt">Time step in seconds.</param>
		/// <returns>The new position pos + vel * dt.</returns>
		public static Vector3 IntegratePosition(Vector3 pos, Vector3 vel, float dt) =>
			new(pos.x + vel.x * dt, pos.y + vel.y * dt, pos.z + vel.z * dt);

		/// <summary>
		/// Unit forward direction for a yaw angle (rotation about the +Y axis), in the XZ
		/// ground plane. Yaw 0 faces +Z, yaw 90 faces +X — matching
		/// Quaternion.Euler(0, yaw, 0) * Vector3.forward. Replaces hand-rolled sin/cos
		/// forward vectors when building first-person / 3D directional movement ("move
		/// forward relative to facing").
		/// </summary>
		/// <param name="yawDegrees">Yaw angle in degrees (rotation about +Y).</param>
		/// <returns>The unit forward vector (sin(yaw), 0, cos(yaw)).</returns>
		public static Vector3 ForwardFromYaw(float yawDegrees)
		{
			float rad = yawDegrees * Mathf.Deg2Rad;
			return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
		}

		/// <summary>
		/// Unit right direction for a yaw angle (rotation about the +Y axis), in the XZ
		/// ground plane — 90 degrees clockwise of <see cref="ForwardFromYaw"/>, matching
		/// Quaternion.Euler(0, yaw, 0) * Vector3.right. Use for strafing.
		/// </summary>
		/// <param name="yawDegrees">Yaw angle in degrees (rotation about +Y).</param>
		/// <returns>The unit right vector (cos(yaw), 0, -sin(yaw)).</returns>
		public static Vector3 RightFromYaw(float yawDegrees)
		{
			float rad = yawDegrees * Mathf.Deg2Rad;
			return new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad));
		}

		/// <summary>
		/// Unit forward direction for a full set of euler angles in degrees, equivalent to
		/// Quaternion.Euler(eulerAngles) * Vector3.forward. Feed it an entity's Rotation
		/// (e.g. !entity { Property: Rotation }) to get its facing direction including pitch
		/// and roll, not just yaw.
		/// </summary>
		/// <param name="eulerAngles">Euler angles in degrees (x = pitch, y = yaw, z = roll).</param>
		/// <returns>The unit forward vector.</returns>
		public static Vector3 ForwardFromAngles(Vector3 eulerAngles) =>
			Quaternion.Euler(eulerAngles) * Vector3.forward;

		/// <summary>
		/// Unit right direction for a full set of euler angles in degrees, equivalent to
		/// Quaternion.Euler(eulerAngles) * Vector3.right. The strafe companion to
		/// <see cref="ForwardFromAngles"/>.
		/// </summary>
		/// <param name="eulerAngles">Euler angles in degrees (x = pitch, y = yaw, z = roll).</param>
		/// <returns>The unit right vector.</returns>
		public static Vector3 RightFromAngles(Vector3 eulerAngles) =>
			Quaternion.Euler(eulerAngles) * Vector3.right;

		/// <summary>
		/// Unit up direction for a full set of euler angles in degrees, equivalent to
		/// Quaternion.Euler(eulerAngles) * Vector3.up. Completes the forward/right/up basis
		/// alongside <see cref="ForwardFromAngles"/> and <see cref="RightFromAngles"/>.
		/// </summary>
		/// <param name="eulerAngles">Euler angles in degrees (x = pitch, y = yaw, z = roll).</param>
		/// <returns>The unit up vector.</returns>
		public static Vector3 UpFromAngles(Vector3 eulerAngles) =>
			Quaternion.Euler(eulerAngles) * Vector3.up;

		/// <summary>
		/// Unit forward direction for a 2D top-down entity from its Z-axis rotation, in the XY
		/// plane. Rotation 0 faces +Y (up), 90 faces -X — the convention for a sprite drawn
		/// pointing up, matching Quaternion.Euler(0, 0, degrees) * Vector3.up. Feed it an
		/// entity's Rotation.z to get its facing direction; drops the sin/cos boilerplate every
		/// top-down shooter ("thrust along facing") would otherwise hand-roll. This is the
		/// +Y-up counterpart to the +X-forward <c>Heading2D</c>/<c>LookRotation2D</c> convention
		/// in SteeringMath.
		/// </summary>
		/// <param name="degrees">Z-axis rotation in degrees (counter-clockwise).</param>
		/// <returns>The unit forward vector (-sin(degrees), cos(degrees), 0).</returns>
		public static Vector3 ForwardFromRotation2D(float degrees)
		{
			float rad = degrees * Mathf.Deg2Rad;
			return new Vector3(-Mathf.Sin(rad), Mathf.Cos(rad), 0f);
		}

		/// <summary>
		/// Unit right direction for a 2D top-down entity from its Z-axis rotation, in the XY
		/// plane — 90 degrees clockwise of <see cref="ForwardFromRotation2D"/>. Rotation 0 gives
		/// +X (right), 90 gives +Y. Use for strafing or lateral offsets relative to facing.
		/// </summary>
		/// <param name="degrees">Z-axis rotation in degrees (counter-clockwise).</param>
		/// <returns>The unit right vector (cos(degrees), sin(degrees), 0).</returns>
		public static Vector3 RightFromRotation2D(float degrees)
		{
			float rad = degrees * Mathf.Deg2Rad;
			return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
		}
	}
}
