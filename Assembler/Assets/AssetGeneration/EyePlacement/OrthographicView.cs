using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// An orthographic camera basis for looking at a voxel model, expressed as an
    /// orthonormal <see cref="Right"/>/<see cref="Up"/>/<see cref="Forward"/> triple
    /// where <see cref="Forward"/> points from the camera into the model. All presets
    /// are built for <b>Z-up</b> voxel space (the raw MagicaVoxel <c>.vox</c> space
    /// that <see cref="VoxReader"/> preserves), so "up" in the render is +Z.
    ///
    /// A three-quarter <see cref="Isometric"/> view is the default because a single
    /// flat face view can only reach front-facing surfaces — an isometric view
    /// exposes a front, a side and the top at once, so eyes can be placed on the side
    /// of a head as easily as the front. Pair it with <see cref="IsometricLeft"/> (or
    /// bilateral symmetry) to reach the opposite side.
    /// </summary>
    public readonly struct OrthographicView
    {
        public Vector3 Right { get; }
        public Vector3 Up { get; }
        public Vector3 Forward { get; }

        public OrthographicView(Vector3 right, Vector3 up, Vector3 forward)
        {
            Right = right.normalized;
            Up = up.normalized;
            Forward = forward.normalized;
        }

        /// <summary>Classic three-quarter view onto the front-right-top (yaw 45°, pitch 30°).</summary>
        public static OrthographicView Isometric => FromZUpAngles(45f, 30f);

        /// <summary>Three-quarter view onto the opposite (left) side, to reach the far flank.</summary>
        public static OrthographicView IsometricLeft => FromZUpAngles(135f, 30f);

        /// <summary>Flat view straight at the +X face.</summary>
        public static OrthographicView Front => FromZUpAngles(0f, 0f);

        /// <summary>Straight down the -Z axis onto the top.</summary>
        public static OrthographicView Top => new(Vector3.right, Vector3.up, new Vector3(0f, 0f, -1f));

        /// <summary>
        /// Builds a view for Z-up voxel space from a compass <paramref name="yawDegrees"/>
        /// (rotation about the up axis, 0° looking along +X) and a downward
        /// <paramref name="pitchDegrees"/> tilt from the horizon.
        /// </summary>
        public static OrthographicView FromZUpAngles(float yawDegrees, float pitchDegrees)
        {
            var up = new Vector3(0f, 0f, 1f);
            float yaw = yawDegrees * Mathf.Deg2Rad;
            float pitch = pitchDegrees * Mathf.Deg2Rad;

            var horizontal = new Vector3(Mathf.Cos(yaw), Mathf.Sin(yaw), 0f);
            var forward = (horizontal * Mathf.Cos(pitch) - up * Mathf.Sin(pitch)).normalized;

            var camUp = (up - forward * Vector3.Dot(up, forward)).normalized;
            var right = Vector3.Cross(camUp, forward).normalized;
            return new OrthographicView(right, camUp, forward);
        }
    }

    /// <summary>
    /// Locks an <see cref="OrthographicView"/> to a specific model's bounds, giving an
    /// exact, invertible mapping between the render's normalised image coordinates
    /// (origin top-left, x right, y down, both in [0,1]) and world rays. The renderer
    /// and the reprojection share one instance so a 2D pick lands on the voxel it was
    /// drawn over. The fit is uniform (square) and centred, with a margin, so a pick's
    /// aspect matches the image the model saw.
    /// </summary>
    public sealed class VoxelViewProjection
    {
        private readonly OrthographicView _view;
        private readonly float _uCentre;
        private readonly float _vCentre;
        private readonly float _worldPerNorm;
        private readonly float _depthMin;
        private readonly float _depthMax;

        public OrthographicView View => _view;

        /// <summary>How far a ray must travel to cross the whole model plus a voxel of slack.</summary>
        public float RayLength => (_depthMax - _depthMin) + 2f;

        public VoxelViewProjection(OrthographicView view, VoxelModel model, float margin = 0.08f)
        {
            _view = view;
            margin = Mathf.Clamp(margin, 0f, 0.45f);

            // Project the eight bounding-box corners to find the screen-space extent.
            float uMin = float.MaxValue, uMax = float.MinValue;
            float vMin = float.MaxValue, vMax = float.MinValue;
            _depthMin = float.MaxValue;
            _depthMax = float.MinValue;

            Vector3 min = model.Min;
            Vector3 max = (Vector3)model.Max + Vector3.one; // voxels are unit cells, so span to the far face
            for (int c = 0; c < 8; c++)
            {
                var corner = new Vector3(
                    (c & 1) == 0 ? min.x : max.x,
                    (c & 2) == 0 ? min.y : max.y,
                    (c & 4) == 0 ? min.z : max.z);

                float u = Vector3.Dot(corner, view.Right);
                float v = Vector3.Dot(corner, view.Up);
                float d = Vector3.Dot(corner, view.Forward);

                uMin = Mathf.Min(uMin, u); uMax = Mathf.Max(uMax, u);
                vMin = Mathf.Min(vMin, v); vMax = Mathf.Max(vMax, v);
                _depthMin = Mathf.Min(_depthMin, d); _depthMax = Mathf.Max(_depthMax, d);
            }

            _uCentre = (uMin + uMax) * 0.5f;
            _vCentre = (vMin + vMax) * 0.5f;
            float extent = Mathf.Max(Mathf.Max(uMax - uMin, vMax - vMin), 1e-4f);
            _worldPerNorm = extent / Mathf.Max(1f - 2f * margin, 1e-4f);
        }

        /// <summary>World point → normalised image coordinate (x right, y down, [0,1]).</summary>
        public Vector2 WorldToNormalized(Vector3 world)
        {
            float u = Vector3.Dot(world, _view.Right);
            float v = Vector3.Dot(world, _view.Up);
            float nx = 0.5f + (u - _uCentre) / _worldPerNorm;
            float ny = 0.5f - (v - _vCentre) / _worldPerNorm;
            return new Vector2(nx, ny);
        }

        /// <summary>How many pixels one voxel (one world unit) spans in an image of this size.</summary>
        public float VoxelPixelSize(int imageSize) => imageSize / _worldPerNorm;

        /// <summary>
        /// Normalised image coordinate → a world ray that starts just outside the model
        /// on the camera side and travels along the view direction, for the raycaster to
        /// march. Exact inverse of <see cref="WorldToNormalized"/> in the image plane.
        /// </summary>
        public void NormalizedToRay(Vector2 normalized, out Vector3 origin, out Vector3 direction)
        {
            float u = _uCentre + (normalized.x - 0.5f) * _worldPerNorm;
            float v = _vCentre - (normalized.y - 0.5f) * _worldPerNorm;
            origin = _view.Right * u + _view.Up * v + _view.Forward * (_depthMin - 1f);
            direction = _view.Forward;
        }

        /// <summary>Half-height an orthographic camera needs to frame the model exactly as this projection maps it.</summary>
        public float OrthographicSize => _worldPerNorm * 0.5f;

        /// <summary>The world-space centre of the framed volume (reconstructed from the orthonormal basis).</summary>
        public Vector3 CentreWorld =>
            _view.Right * _uCentre + _view.Up * _vCentre + _view.Forward * ((_depthMin + _depthMax) * 0.5f);

        /// <summary>
        /// Placement for a real orthographic Unity camera that reproduces <see cref="WorldToNormalized"/>
        /// pixel-for-pixel (square aspect, size = <see cref="OrthographicSize"/>), so a pick on its render
        /// reprojects correctly. <paramref name="margin"/> is the clearance in front of the near face.
        /// </summary>
        public void GetCamera(out Vector3 position, out Quaternion rotation, out float nearClip, out float farClip, float margin = 1f)
        {
            float depthHalf = (_depthMax - _depthMin) * 0.5f;
            position = CentreWorld - _view.Forward * (depthHalf + margin);
            rotation = Quaternion.LookRotation(_view.Forward, _view.Up);
            nearClip = 0.01f;
            farClip = (_depthMax - _depthMin) + 2f * margin + 1f;
        }
    }
}
