using System;
using System.Collections.Generic;

namespace MercurioIdle.Planet
{
    /// <summary>
    /// Acesso ao terreno sem conhecer sua geração ou armazenamento.
    /// Consultas podem materializar blocos sob demanda e preservam sua identidade e dados.
    /// </summary>
    public interface IPlanetLevel
    {
        /// <summary>
        /// Obtém a grade de filhos imediatos do setor, com X angular e Y radial.
        /// Retorna false para folha final, setor removido ou endereço inexistente.
        /// O contexto obtido permanece utilizável após escavação; células vazias retornam false.
        /// </summary>
        bool TryGetGridContext(PlanetBlockAddress sectorAddress, out PlanetGridContext context);

        /// <summary>Obtém a folha de gameplay. Retorna false no núcleo, fora do planeta ou em uma cavidade.</summary>
        bool TryGetBlock(PlanetPosition position, out IPlanetBlock block);

        /// <summary>
        /// Obtém o bloco no endereço, inclusive blocos intermediários.
        /// Retorna false para endereço inexistente, núcleo ou bloco completamente removido.
        /// </summary>
        bool TryGetBlock(PlanetBlockAddress address, out IPlanetBlock block);

        /// <summary>
        /// Enumera blocos que intersectam a região na profundidade solicitada, sem duplicatas.
        /// Zero representa as raízes; folhas mais rasas são retornadas sem subdivisão artificial.
        /// Exclui núcleo e blocos completamente removidos; pais parcialmente escavados permanecem.
        /// A enumeração é preguiçosa. Não modificar o level durante a iteração.
        /// </summary>
        IEnumerable<IPlanetBlock> EnumerateBlocks(PlanetRegion region, int depth);

        /// <summary>
        /// Verifica se toda a região radial acima do bloco está vazia.
        /// Blocos externos, removidos ou de outro level não podem ser escavados.
        /// </summary>
        bool CanRemoveBlock(IPlanetBlock block);

        /// <summary>
        /// Remove um bloco e toda sua região descendente somente quando sua borda externa está exposta.
        /// Retorna false se coberto, já removido ou pertencente a outro level.
        /// </summary>
        bool RemoveBlock(IPlanetBlock block);

        /// <summary>
        /// Disparado uma vez por chamada de remoção bem-sucedida, com o bloco solicitado.
        /// Os dados existentes continuam acessíveis pela referência ao bloco removido.
        /// </summary>
        event Action<IPlanetBlock> BlockRemoved;
    }
}
