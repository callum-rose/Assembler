using UnityEngine;

namespace VoxelToolkit
{
    public abstract class VoxelAnimationBase : MonoBehaviour
    {
        [SerializeField] protected float time = 0.0f;
        
        public abstract int MaxFrame { get; }
    }
}