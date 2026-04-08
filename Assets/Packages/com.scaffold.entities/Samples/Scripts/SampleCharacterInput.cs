using System;
using UnityEngine;

namespace Scaffold.Entities.Samples
{
    /// <summary>
    /// Per-frame input for the sample character (WASD / arrows on the XZ plane).
    /// </summary>
    [Serializable]
    public readonly struct SampleCharacterInput
    {
        public SampleCharacterInput(Vector2 move)
        {
            Move = move;
        }

        public Vector2 Move { get; }
    }
}
