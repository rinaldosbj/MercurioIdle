using System;
using UnityEngine;

namespace MercurioIdle.Construction
{
    public sealed class BuildingDefinition
    {
        public string Code;
        public string Name;
        public string Description;
        public string Hotkey;
        public float Width;
        public float Height;
        public int MaxCount;
        public Func<int> BuiltCount;
        public Func<Sprite> GetSprite;
        public Func<Vector3, float, MonoBehaviour> Build;

        public static readonly BuildingDefinition FactoryBuilding = new BuildingDefinition
        {
            Code = "FC",
            Name = "FÁBRICA",
            Description = "+3 ROBÔS",
            Hotkey = "1",
            Width = FactoryArt.WorldWidth,
            Height = FactoryArt.WorldHeight,
            MaxCount = 1,
            BuiltCount = () => RobotFactory.All.Count,
            GetSprite = FactoryArt.GetFactorySprite,
            Build = (position, fall) => RobotFactory.Create(position, fall)
        };
    }
}
