# Dyson Swarm

Abra Assets/Scenes/SunShaderTest.unity e selecione Sun. Cada anel tem uma normal central própria. Os planos são gerados por revolução em torno dessa normal e todas as órbitas circulares permanecem centradas no Sol.

## Distribuição

Plane Count, Slots Per Plane, Angular Thickness e Orbital Speed são globais. Use Per Ring Overrides permite ajustes individuais. Cada órbita mantém slots igualmente espaçados; Phase Stagger distribui a fase relativa entre órbitas usando uma sequência de baixa discrepância. Não é um simulador de prevenção de colisões.

A distancia de cada orbita a estrela e amostrada continuamente por uma normal, sem raios de camadas. Distribution Seed torna as amostras reproduziveis. Orbit Groups controla a quantidade manual de orbitas; no crescimento continuo, os grupos sao calculados pela populacao. Migra o produto antigo de Radial Layers e Inclination Layers.

Radial Variation agora e o desvio padrao radial (sigma), em unidades mundiais. Altitude Variation e o desvio padrao vertical, somado a contribuicao de Angular Thickness. A amplitude de inclinacao segue Rayleigh, com nos uniformemente distribuidos em torno da normal central do anel. Assim, a altura dos coletores ao longo das orbitas tem uma marginal aproximadamente normal, em vez do pico produzido por uma amplitude gaussiana multiplicada por seno. Os slots permanecem regulares em cada orbita.

As caudas sao amostradas com truncamento em quatro sigma, por inversao da CDF; nao sao presas numa borda por clamp. Em nuvens muito espessas, a inclinacao precisa de truncamento adicional para conservar circulos centrados no Sol. A aproximacao cilindrica e melhor quando a espessura e pequena frente ao raio do anel. Curvatura, tangencias e sobreposicao de aneis ainda alteram a projecao global.

Ring Count controla os anéis principais. Use Uniform Ring Spacing aplica Base Radius + índice × Ring Separation e mostra os raios calculados no Inspector. Auto Distribute Ring Normals recalcula as orientações ao mudar a quantidade. Panel Size é o raio do hexágono em unidades mundiais. Os parâmetros atualizam a prévia no Editor, mesmo com Animate In Editor desativado.

## LoD e GPU

Enable Point Lod classifica cada coletor na GPU pelo diâmetro projetado em pixels. Abaixo de Point Lod Pixels, o coletor vira um pequeno triângulo voltado para a câmera, usado como ponto. Minimum Point Pixels controla seu tamanho mínimo. O mesmo cálculo atende câmeras de perspectiva e ortográficas e responde ao zoom e à resolução.

Os pontos preservam a posição orbital e a quantidade total. Eles evitam gerar a base completa do painel no compute e calculam a cor solar no vertex shader, compartilhada pelo triângulo. Debug Reflection força hexágonos para verificar os espelhos. Há até dois desenhos indiretos por anel: hexágonos e pontos, com contadores e argumentos escritos pela GPU. Nenhum contador é lido pela CPU durante o desenho normal.

O compute shader calcula a posição uma vez por coletor e a orientação somente para hexágonos. Os vértices reutilizam os resultados. Buffers persistem entre quadros e crescem em blocos de 256 coletores quando sua capacidade se esgota. O cache considera fase, distribuição, câmera e parâmetros do LoD. Anéis fora do frustum são descartados antes do cálculo. Não há orçamento total nem redução automática da densidade.

A capacidade reservada é aproximadamente 84 bytes por coletor: transformação de 64 bytes, índice de 4 bytes e posição de ponto de 16 bytes. O modo de pontos reduz cálculos e escritas, mas a reserva de memória é mantida para permitir misturar representações sem realocação. Não há GameObject ou Transform por coletor.

O URP usa passes Compute e Raster do RenderGraph com dependências explícitas. Exige RenderGraph ativo; modo de compatibilidade não é suportado. Sombras, motion vectors e PBR completo não estão implementados.

## Validação

Tools > Dyson Swarm > Validate shader and reflection compila os dois passes, testa a GPU em modos perto, longe, misto e ortográfico, verifica a conservação da quantidade, os argumentos indiretos, a igualdade das posições entre representações, os raios distintos, os planos centrados no Sol e a reflexão. Também verifica 230.400 amostras analíticas. Relatório em Library/DysonSwarm/Validation.txt.

A nuvem tem volume e densidade concentrada, mas círculos inclinados centrados no Sol não formam um torus matemático exato em todos os instantes. Fases são reduzidas módulo 2π em precisão dupla antes de chegar à GPU.

## Reflexo e eclipse

Solar Color e Shadow Color controlam a luz quente refletida e a face escura. Reflection Strength, Glint Width, Smoothness e Solar Radius definem o brilho conforme a câmera se aproxima da direção refletida. O raio do Sol é manual, em unidades mundiais. A luz considera a incidência na face refletora; o verso é mais escuro. O efeito funciona nos hexágonos e nos pontos.

Eclipse Occluder é um Transform opcional de um planeta ou outro corpo; Eclipse Radius representa seu raio esférico mundial e Eclipse Softness suaviza a borda da sombra. Sem esse corpo, não há eclipse de iluminação por outro objeto. Quando o Sol está entre um coletor e o alvo, a reflexão entregue ao alvo é bloqueada e a tonalidade escurece. Isso não remove a iluminação incidente do próprio Sol. Oclusão por outros coletores não é simulada.

## Population Growth

O mapeamento e 1:1 ate Linear Mirror Limit (L). Depois usa y = L + a * ln(1 + (x-L)/a), com a positivo resolvido para atingir Maximum Visual Mirrors em Maximum Real Mirrors. A derivada e 1 na juncao e a/(a+x-L) no trecho logaritmico: nunca ultrapassa 1 e diminui com a populacao. O teto continua limitado ao maximo visual. Tools > Dyson Swarm > Validate mirror mapping verifica os extremos e as derivadas.

Mirror Count representa a populacao real, convertida pelo mapeamento linear/logaritmico em populacao visual. Com Population Growth, cada anel revela um prefixo de IDs deterministico. Plane Count e Slots Per Plane definem uma malha fixa. Cada orbita recebe todos os seus slots antes da proxima: ID / Slots Per Plane escolhe a orbita e ID % Slots Per Plane escolhe o slot. Os slots aparecem numa permutacao que espalha as fases ao longo da mesma trilha, sem mover os anteriores. Cada intervalo de Mirrors Per Additional Ring abre uma etapa com mais um anel: somente os espelhos novos sao divididos entre os aneis dessa etapa. As normais automaticas usam o maximo configurado de aneis. Populacao zero nao renderiza; desativar Population Growth restaura a malha manual completa.

A CPU resolve os valores somente em OnEnable/OnValidate ou ao editar a quantidade maxima de aneis. Maximum Cloud Width limita a nuvem desde o primeiro espelho. Os antigos limiares de planos, slots e largura ficam serializados para compatibilidade, mas nao alteram as trajetorias. Buffers crescem em blocos de 256 espelhos; acrescentar um anel preserva os buffers dos anteriores.

## Continuous Surface

Com Population Growth, Filled Torus e Continuous Surface ativos, cada espelho visual conserva seu ID mesmo apos a saturacao. A area do hexagono e 3 sqrt(3) Physical Mirror Radius^2 / 2; Saturation Occupancy define a fracao ocupada. Cada anel calcula sua faixa usando a populacao acumulada nele.

A densidade sobre a superficie esferica usa s = sin(latitude): nucleo constante ate |s| = c e bordas gaussianas exp(-(|s|-c)^2/(2 sigma^2)). A CPU integra esse perfil e inverte a area para obter c. A area por espelho permanece constante durante a expansao, sem controle por decadas. Os desvios padrao radial e das bordas nao aumentam com a expansao: cresce a superficie ocupada. Cada faixa respeita o raio e a normal do seu anel.

O centro usa uma malha compartilhada e iluminacao solar/reflexo/eclipse; o interior da faixa e opaco. A faixa oculta os individuos internos sem reamostrar suas orbitas: raio, plano e fase dependem do ID, da semente e do regime no nascimento. A borda tem uma transicao espacial de meio sigma; profundidade opaca e culling usam a mesma cobertura. Os buffers conservam todos os IDs visuais, inclusive os ocultos; a reserva de memoria cresce com a populacao visual ate o teto do mapeamento. Aneis em raios diferentes sao superficies diferentes.

A representacao continua exige populacao suficiente para area fisica do anel. Antes dela, populacoes que ainda exijam muitos individuos continuam consumindo memoria; nao ha promessa de FPS independente de camera, tamanho fisico dos espelhos ou raios.

Physical Mirror Radius e independente de Panel Size: o primeiro calcula area fisica e o segundo amplia os coletores para visibilidade. O raio fisico padrao 0.01 produz, na cena SunShaderTest atual, transicoes dos cinco aneis entre aproximadamente 3.6e6 e 5.6e6 espelhos totais. Mudar apenas Panel Size nao altera o regime.

## Culling do nucleo tridimensional

A faixa substitui apenas o volume denso transversal: latitude dentro do nucleo e deslocamento radial dentro de dois desvios padrao. As caudas radiais (entre dois e quatro sigma), incluindo ambos os lados da faixa, conservam individuos em toda a largura angular; as bordas laterais tambem conservam individuos. Trechos orbitais internos sao descartados antes da orientacao e compactacao de LOD, sem alterar ou deslocar a orbita.

Antes da saturacao, orbitas novas usam Rayleigh. Depois, usam a CDF do regime de faixa calculada na sua populacao de nascimento (indice da orbita x slots + 1). A CDF e interpolada em uma tabela de nucleos fixos; a populacao atual nunca reamostra orbitas existentes. As geracoes posteriores alimentam as bordas da faixa crescente. A CDF de inclinacoes continua disponivel para validar o antigo amostrador de bordas. Tools > Dyson Swarm > Validate growth continuity verifica os mesmos IDs antes e depois da saturacao, com e sem cache, a passagem 999 -> 1000 e a entrada de novos aneis.

A faixa continua agora e uma envoltoria fechada do nucleo transversal: superfícies radial externa/interna e paredes nas duas bordas de latitude. Assim, o volume eliminado pelo compute tem representacao tambem em vistas tangentes. O culling preserva uma margem pelo tamanho mundial do painel e pelo erro de aproximacao da malha; somente centros suficientemente internos sao descartados.

A faixa calcula a iluminacao sobre a superficie curva analitica, independente das faces planas usadas para rasterizacao. Bloqueio espelho-alvo nao substitui a iluminacao solar por Shadow Color. As faces de tras ainda ficam escuras e Eclipse Occluder continua reduzindo a luz incidente.

## Tempo da animação

Durante o Play, as órbitas usam Time.timeAsDouble e respeitam a pausa e Time.timeScale. Fora do Play, Animate In Editor usa o relógio do editor; desativado, mantém a prévia estática. PrepareSwarm recebe o instante como argumento, sem manter um relógio próprio. A captura do wallpaper fornece o instante de cada frame antecipado sem avançar o tempo do jogo.


## Build definitivo

A cena principal é `Assets/Scenes/SunShaderTest.unity`. Execute `DysonReleaseBuild.Build` com Unity 6000.6.2f1 em batch mode para validar mapeamento, continuidade, shader e reflexão e gerar `Builds/Sun/MercurioIdle.exe`.

O renderer mantém profundidade compartilhada, iluminação única e oclusão conservadora de coletores. Os protótipos de rasterização de versos e compactação por grupos não fazem parte desta versão.
