using System.Collections.Generic;
using UnityEngine;

namespace MercurioIdle.Construction
{
    public sealed class RobotFactory : Building
    {
        public static readonly List<RobotFactory> All = new List<RobotFactory>();

        [SerializeField] private int robotsProvided = 3;

        public int RobotsProvided => robotsProvided;
        public Vector3 DoorPoint => new Vector3(Left + Width * FactoryArt.DoorCenter, Bottom + 0.8f, transform.position.z);
        public Vector3 HomePoint => new Vector3(transform.position.x, Top + 1.5f, transform.position.z);

        public static int TotalRobotCapacity
        {
            get
            {
                int total = 0;
                foreach (var factory in All) total += factory.robotsProvided;
                return total;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetFactories() => All.Clear();

        public static RobotFactory Create(Vector3 basePosition, float fall)
        {
            return Spawn<RobotFactory>("Fábrica", FactoryArt.GetFactorySprite(), FactoryArt.WorldWidth, FactoryArt.WorldHeight, basePosition, fall);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!All.Contains(this)) All.Add(this);
        }

        protected override void OnDisable()
        {
            All.Remove(this);
            base.OnDisable();
        }
    }
}
