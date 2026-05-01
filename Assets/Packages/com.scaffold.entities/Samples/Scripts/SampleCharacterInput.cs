using System;
using UnityEngine;

namespace Scaffold.Entities.Samples
{
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
