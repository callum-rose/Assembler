using UnityEngine;

namespace VoxelToolkit
{
    public static class DebugUtility
    {
        public static void DrawQuad(Vector3 location, Vector3 direction, Vector3 size, Color color)
        {
            var cross = Vector3.Cross(Vector3.right, direction);
            if (cross.magnitude == 0.0f)
                cross = Vector3.Cross(Vector3.up, direction);

            var secondCross = Vector3.Cross(cross, direction);

            var firstSize = Mathf.Abs(Vector3.Dot(size, cross));
            var secondSize = Mathf.Abs(Vector3.Dot(size, secondCross));

            cross *= firstSize;
            secondCross *= secondSize;
            
            var shifted = location + direction * 0.5f * Mathf.Abs(Vector3.Dot(direction, size));
            
            Debug.DrawLine(shifted + (-cross - secondCross) * 0.5f, shifted + (cross - secondCross) * 0.5f, color);
            Debug.DrawLine(shifted + (cross - secondCross) * 0.5f, shifted + (cross + secondCross) * 0.5f, color);
            Debug.DrawLine(shifted + (cross + secondCross) * 0.5f, shifted + (-cross + secondCross) * 0.5f, color);
            Debug.DrawLine(shifted + (-cross + secondCross) * 0.5f, shifted + (-cross - secondCross) * 0.5f, color);
        }
        
        public static void DrawBox(Bounds bounds, Color color)
        {
            DrawQuad(bounds.center, Vector3.up, bounds.size, color);
            DrawQuad(bounds.center, Vector3.down, bounds.size, color);
            DrawQuad(bounds.center, Vector3.left, bounds.size, color);
            DrawQuad(bounds.center, Vector3.right, bounds.size, color);
            DrawQuad(bounds.center, Vector3.forward, bounds.size, color);
            DrawQuad(bounds.center, Vector3.back, bounds.size, color);
        }
    }
}