using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelToolkit
{
    [System.Serializable]
    public class GenericList<T> where T : class
    {
        [SerializeReference] private List<T> values = new List<T>();

        public List<T> Values => values;
    }
}