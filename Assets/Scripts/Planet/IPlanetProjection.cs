using UnityEngine;

namespace MercurioIdle.Planet
{
    /// <summary>
    /// Conversão entre pixels da tela (origem inferior esquerda) e posições polares.
    /// A implementação considera câmera, plano do corte e transformação do planeta.
    /// </summary>
    public interface IPlanetProjection
    {
        /// <summary>Retorna false quando o raio da câmera não intersecta o plano à sua frente.</summary>
        bool TryScreenToPlanet(Vector2 screenPixels, out PlanetPosition position);

        /// <summary>
        /// Projeta uma posição em pixels. Retorna false se não projetável ou atrás da câmera.
        /// Uma posição válida pode estar fora dos limites da tela.
        /// </summary>
        bool TryPlanetToScreen(PlanetPosition position, out Vector2 screenPixels);
    }
}
