using UnityEngine;
using MercurioIdle.Planet;
using MercurioIdle.Robots;

namespace MercurioIdle.Construction
{
    public static class TerrainQuery
    {
        private const float ScanStep = 0.5f;
        private const int RefineSteps = 6;
        private const int MaxLiftSteps = 160;

        public static float PlaneZ
        {
            get
            {
                var designations = DigDesignations.Instance;
                return designations != null && designations.Terrain != null ? designations.Terrain.transform.position.z : 0f;
            }
        }

        public static bool IsSolid(Vector3 world)
        {
            var designations = DigDesignations.Instance;
            if (designations == null || designations.Terrain == null) return false;

            var terrain = designations.Terrain;
            var level = terrain.Level;
            var projection = terrain.Projection as PlanetProjection;
            if (level == null || projection == null) return false;

            try
            {
                Vector3 local = terrain.transform.InverseTransformPoint(world);
                var position = projection.FromLocal(new Vector3(local.x, local.y, 0f));
                return level.TryGetBlock(position, out _);
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        public static bool TryFindGround(float x, float z, float startY, float maxDistance, out float groundY)
        {
            groundY = 0f;
            float y = startY;

            int lifted = 0;
            while (IsSolid(new Vector3(x, y, z)) && lifted++ < MaxLiftSteps) y += ScanStep;
            if (IsSolid(new Vector3(x, y, z))) return false;

            for (float travelled = 0f; travelled < maxDistance; travelled += ScanStep)
            {
                float lowerY = y - ScanStep;
                if (IsSolid(new Vector3(x, lowerY, z)))
                {
                    float freeY = y;
                    float solidY = lowerY;
                    for (int i = 0; i < RefineSteps; i++)
                    {
                        float middle = (freeY + solidY) * 0.5f;
                        if (IsSolid(new Vector3(x, middle, z))) solidY = middle;
                        else freeY = middle;
                    }

                    groundY = freeY;
                    return true;
                }

                y = lowerY;
            }

            return false;
        }
    }
}
