# UI

MirrorProgressBar.prefab é somente UI: Canvas, CanvasScaler, Slider, bordas, preenchimento e texto. Não possui scripts personalizados nem referências à simulação.

Edite a barra em Prefab Mode. O componente Slider no objeto ProgressBar controla o preenchimento pelo campo Value (0–1). O texto Label é editável de forma independente.

O estilo usa NegativeUI.mat e TerminalMono.ttf. As bordas e a área de preenchimento acompanham o RectTransform da barra por âncoras. Na cena SunShaderTest, a instância fica no topo, com margem de 28. O componente DysonMirrorProgressUI adicionado à instância conecta o Slider e o Label ao MirrorCount e MaximumRealMirrors do enxame da cena.
