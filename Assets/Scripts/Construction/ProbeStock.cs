using System;
using UnityEngine;

namespace MercurioIdle.Construction
{
    public static class ProbeStock
    {
        public static int Count { get; private set; }

        public static event Action<int> Produced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Count = 0;
            Produced = null;
        }

        public static void Add(int amount)
        {
            Count += amount;
            Produced?.Invoke(Count);
        }
    }
}
