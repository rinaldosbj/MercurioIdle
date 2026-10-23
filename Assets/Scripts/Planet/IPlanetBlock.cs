using System;

namespace MercurioIdle.Planet
{
    /// <summary>Bloco com identidade, limites físicos e dados próprios de cada sistema de gameplay.</summary>
    public interface IPlanetBlock
    {
        PlanetBlockAddress Address { get; }
        PlanetBlockBounds Bounds { get; }
        bool IsRemoved { get; }

        /// <summary>
        /// Retorna os dados existentes ou cria e associa uma instância não nula.
        /// Existe no máximo uma entrada por tipo T, mantida durante a vida deste level.
        /// </summary>
        T GetOrAddData<T>(Func<T> create) where T : class;

        /// <summary>Consulta dados sem criá-los. Quando ausentes, retorna false e data nulo.</summary>
        bool TryGetData<T>(out T data) where T : class;
    }
}
