﻿using System;
using UnityEngine;

namespace Source.Features.SceneEditor.Data
{
    [Serializable]
    public class CubeData
    {
        public float X;
        public float Y;
        public float Z;
        
        public float XRotation;
        public float YRotation;
        public float ZRotation;

        public int PrefabIndex;

        public CubeData(Vector3 position, Quaternion rotation, int prefabIndex)
        {
            X = position.x;
            Y = position.y;
            Z = position.z;

            XRotation = rotation.eulerAngles.x;
            YRotation = rotation.eulerAngles.y;
            ZRotation = rotation.eulerAngles.z;

            PrefabIndex = prefabIndex;
        }
    }
}