using System;
using UnityEngine;

namespace MercurioIdle.Planet
{
    /// <summary>
    /// Projeta o corte XY pela câmera usando uma origem polar próxima da região visitada.
    /// Subtrai a origem em double antes de converter para floats do Unity.
    /// Uma unidade local representa um metro; o Transform define posição, rotação e escala da vista.
    /// </summary>
    public sealed class PlanetProjection : IPlanetProjection
    {
        private readonly Camera camera;
        private readonly Transform plane;
        private readonly double originX, originY;

        public PlanetProjection(Camera camera, Transform plane, PlanetPosition origin)
        {
            this.camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            this.plane = plane != null ? plane : throw new ArgumentNullException(nameof(plane));
            originX = origin.RadiusMeters * Math.Cos(origin.AngleRadians);
            originY = origin.RadiusMeters * Math.Sin(origin.AngleRadians);
        }

        /// <summary>Converte coordenadas físicas para coordenadas locais em metros, relativas à origem.</summary>
        public Vector3 ToLocal(PlanetPosition position) => new Vector3(
            (float)(position.RadiusMeters * Math.Cos(position.AngleRadians) - originX),
            (float)(position.RadiusMeters * Math.Sin(position.AngleRadians) - originY), 0);

        /// <summary>Converte uma posição local no corte para coordenadas polares físicas.</summary>
        public PlanetPosition FromLocal(Vector3 local)
        {
            double x = originX + local.x, y = originY + local.y;
            return new PlanetPosition(Math.Sqrt(x * x + y * y), Math.Atan2(y, x));
        }

        public bool TryScreenToPlanet(Vector2 screenPixels, out PlanetPosition position)
        {
            position = default;
            if (!float.IsFinite(screenPixels.x) || !float.IsFinite(screenPixels.y)) return false;
            var ray = camera.ScreenPointToRay(screenPixels);
            var surface = new Plane(plane.forward, plane.position);
            if (!surface.Raycast(ray, out float distance)) return false;
            position = FromLocal(plane.InverseTransformPoint(ray.GetPoint(distance)));
            return true;
        }

        public bool TryPlanetToScreen(PlanetPosition position, out Vector2 screenPixels)
        {
            var projected = camera.WorldToScreenPoint(plane.TransformPoint(ToLocal(position)));
            screenPixels = new Vector2(projected.x, projected.y);
            return projected.z > 0 && float.IsFinite(projected.x) && float.IsFinite(projected.y);
        }
    }
}
