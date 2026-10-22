# Wallpaper dos anéis

Modo integrado ao Player. Inicie `Builds/Sun/MercurioIdle.exe -dysonWallpaper`. A quantidade inicial usa a população da cena; `-dysonWallpaperCount 10000000` permite fornecer outra. Não execute o integrador novamente: os hooks já estão no renderer.

## Câmera e apresentação

Câmera externa fixa, pesquisa de 4.096 direções usando os mesmos raios e normais resolvidos pelo renderer. Prioriza abertura nos núcleos opacos para o disco solar. Os anéis mais densos podem não deixar uma abertura suficiente; nesse caso escolhe a maior margem e registra aviso. Confirmado visualmente que o Sol aparece com 10 milhões na configuração atual. A direção não muda durante a reprodução, mesmo ao receber uma previsão nova.

Limite de 30 FPS com VSync desligado e execução em background. HUD e controles são suspensos. Desligar o componente restaura a câmera e os ajustes de frame rate. Não há integração nativa com o desktop do Windows nesta versão: o Player fornece o conteúdo que o app poderá hospedar.

## Frames futuros

Quatro texturas HDR RGBA16F, até três imagens futuras (100 ms). Cada captura usa explicitamente seu instante orbital e quantidade prevista. Os buffers grandes de coletores continuam únicos. As imagens usam aproximadamente 94,9 MiB em 1080p incluindo depth de quatro bytes; os temporários URP são adicionais.

Renderização e apresentação são submetidas à mesma fila gráfica: cada imagem é escrita antes de ser amostrada, e uma textura é reutilizada após seus usos anteriores nessa fila. Não há leitura de pixels pela CPU, consulta de async-compute fence nem espera bloqueante por conclusão. O caminho usa SingleCameraRequest com atualização explícita dos volumes. As texturas HDR evitam limitar o brilho antes do pós-processamento do Sol.

A antecipação só ocorre com medição positiva de GPU abaixo de 45% do orçamento de 33,33 ms e custo CPU de submissão abaixo de 35%. Pode preparar uma ou duas imagens conforme a folga; a fila tem limite de três futuras. Sem Frame Timing Stats válido, somente a antecipação fica desligada. Não há promessa de redução de energia: cache desloca trabalho para antes do prazo e acrescenta apresentação/memória.

## Integração da previsão

`DysonWallpaperController.Active.FeedPopulation(count)` mantém a quantidade constante. `FeedPopulation(count, panelsPerSecond)` fornece previsão linear ancorada no instante atual. Cada alteração invalida as imagens futuras sem reiniciar a fase das órbitas. `CurrentDisplayedCount` informa a população efetivamente exibida; durante a preparação, o renderer pode conter uma população futura.

Ao prever outra quantidade, a captura chama SetMirrorCount antes de renderizar. Isso recalcula distribuição, superfícies e culling. Taxas não nulas frequentes ainda usam a reconstrução atual dos buffers: medir esse custo com a taxa real do app. Os testes de utilização usam população constante, como solicitado para esta fase.

Mudanças de luz, alvo ou material também devem invalidar a fila por uma nova chamada FeedPopulation. A antecipação cobre a simulação dos espelhos; outras animações precisam de seu próprio relógio. TAA/motion blur e mudanças de resolução não foram submetidos à mesma bateria de medição.
