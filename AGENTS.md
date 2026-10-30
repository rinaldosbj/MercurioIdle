# Git Workflow — Unity Project

Objetivo: desenvolver em equipe sem perder alterações, sobrescrever o trabalho dos colegas ou transformar conflitos do Unity em um problema recorrente.

Este documento define como vamos organizar branches, commits, cenas, prefabs, scripts e Pull Requests durante o desenvolvimento do jogo.

As regras foram pensadas para uma equipe pequena. O objetivo não é burocratizar o desenvolvimento, mas estabelecer hábitos que evitem retrabalho e facilitem a colaboração.

## 1. Princípios fundamentais

Nunca trabalhar diretamente na main. Toda alteração deve ser feita em uma branch própria.

Uma tarefa, uma branch. Cada branch deve ter um objetivo claro.

Uma cena compartilhada, um responsável por vez. Antes de editar uma cena, verifique se outra pessoa já está trabalhando nela.

Prefabs antes de objetos configurados diretamente na cena. Sempre que possível, a lógica e a configuração dos objetos devem ficar em prefabs e componentes reutilizáveis.

Scripts organizados por responsabilidade. Prefira scripts coesos, com responsabilidades claras e fáceis de entender.

Não fazer commit de tudo automaticamente. Confira exatamente quais arquivos foram alterados.

Quanto menor o commit, melhor. Faça commits pequenos, claros e relacionados, facilitando a revisão e a identificação de problemas.

Toda alteração deve ser testada antes do merge.

Se houver risco de conflito, avise antes de editar. É mais fácil combinar o trabalho do que resolver conflitos depois.

Todo Pull Request deve ser revisado por outra pessoa. Quem criou o PR não pode ser a única pessoa responsável por aprová-lo.

As branches devem ser preservadas após o merge. Elas servem como histórico de referência para revisitar implementações anteriores.

Nenhuma alteração importante deve existir apenas no computador de uma pessoa. Faça commits e pushes regularmente.

## 2. Estrutura de branches

Utilizaremos um fluxo baseado em branches de tarefa e Pull Requests.

### Branches permanentes

main: branch principal, deve permanecer funcional e representar uma versão estável do projeto.

feature/*: novas funcionalidades.

fix/*: correções de bugs.

hotfix/*: correções urgentes em uma versão que já foi entregue.

refactor/*: reorganização de código sem alterar intencionalmente o comportamento.

chore/*: manutenção, configuração e organização técnica.

docs/*: documentação.

Não precisamos criar uma branch develop neste momento. Para uma equipe pequena, ela adicionaria uma etapa permanente de integração sem necessariamente trazer benefícios suficientes.

### Padrão de nomenclatura

Utilizaremos nomes em inglês, minúsculos, separados por hífen e com o tipo da tarefa no início.

Exemplos:

- `feature/player-movement`

- `feature/inventory-system`

- `feature/pause-menu`

- `fix/player-fall-through-floor`

- `fix/audio-not-playing`

- `refactor/player-controller`

- `chore/project-settings`

- `docs/git-workflow`

- `hotfix/game-crash-on-start`

(Opcional — apresentar à equipe): se utilizarmos um sistema de tarefas, podemos incluir o identificador da tarefa no nome da branch.

- `feature/42-player-movement`

- `fix/57-inventory-duplication`

Os nomes devem indicar o objetivo da branch, não a pessoa que está trabalhando nela. Evite nomes vagos como teste, mudancas, branch-nova ou meu-codigo.

### Preservação das branches

Depois que uma branch for integrada à main, ela deve continuar existindo no repositório.

Isso permite consultar o histórico de uma implementação, recuperar o contexto de uma tarefa e revisitar o desenvolvimento anterior.

Não é necessário excluir branches após o merge. Se o trabalho precisar ser retomado futuramente, crie uma nova branch a partir da main atualizada, utilizando a branch antiga como referência.

## 3. Fluxo de trabalho diário

Toda tarefa deverá seguir este processo.

### Passo 1 — Sincronizar o projeto

Antes de começar, atualize a main local:

```sh
git switch main
git pull origin main
```

### Passo 2 — Criar uma branch

Crie a branch a partir da main atualizada:

```sh
git switch -c feature/player-movement
```

### Passo 3 — Verificar os arquivos envolvidos

Antes de editar, identifique quais arquivos serão afetados.

Exemplo:

```text
Assets/
  Scenes/
    Gameplay.unity
  Scripts/
    Player/
      PlayerMovement.cs
  Prefabs/
    Player.prefab
```

Verifique se alguém já está trabalhando na mesma cena, prefab, script ou configuração compartilhada.

### Passo 4 — Desenvolver e testar

Faça as alterações dentro do escopo da tarefa.

Teste no Unity Editor e verifique se o projeto continua funcionando.

### Passo 5 — Fazer commits pequenos

Registre mudanças relacionadas entre si. Prefira vários commits pequenos a um commit enorme com muitas alterações diferentes.

### Passo 6 — Enviar a branch

```sh
git push -u origin feature/player-movement
```

### Passo 7 — Abrir um Pull Request

Abra um Pull Request (PR) para main, explicando:

O que foi implementado.

Quais arquivos importantes foram alterados.

Como testar.

Se alguma cena ou prefab compartilhado foi modificado.

Se existem limitações ou problemas conhecidos.

### Passo 8 — Revisar e integrar

Outro integrante deve revisar o PR, verificar os arquivos modificados e testar o resultado quando necessário.

O autor não pode ser a única pessoa a aprovar o próprio trabalho. A revisão por outra pessoa é obrigatória, mesmo que a alteração pareça simples.

Resolva os comentários, atualize a branch e faça o merge depois da aprovação.

### Passo 9 — Preservar a branch

Depois do merge, mantenha a branch no repositório remoto.

Para começar uma nova tarefa, crie outra branch a partir da main atualizada, sem reutilizar uma branch antiga para uma tarefa diferente.

## 4. Regras específicas para cenas do Unity

Esta é uma das partes mais importantes do workflow.

Arquivos .unity armazenam a configuração dos objetos, componentes, referências e propriedades presentes em uma cena. Mesmo uma mudança visual aparentemente pequena pode modificar o arquivo em vários trechos.

O Git pode mesclar algumas alterações, mas isso não garante que a cena resultante permaneça correta no Unity.

### Regra 4.1 — Não editar a mesma cena simultaneamente

Antes de começar uma tarefa que envolva uma cena, avise a equipe.

Exemplo:

Vou editar Gameplay.unity para organizar o cenário e configurar os pontos de spawn. Evitem alterar essa cena até eu avisar que terminei.

O responsável pela cena deve ser identificado no canal de comunicação da equipe ou no quadro de tarefas.

### Regra 4.2 — Se precisar trabalhar na mesma cena, crie uma cópia

Se outra pessoa estiver modificando a cena original e você também precisar fazer alterações nela, não trabalhe diretamente no mesmo arquivo.

Em vez disso:

Confirme que sua branch está atualizada e que você entende qual versão da cena está utilizando.

Crie uma cópia da cena pelo Unity Editor.

Dê à cópia um nome identificável, como Gameplay_SeuRecurso.

Faça suas alterações exclusivamente nessa cópia.

Não altere a cena original enquanto o outro integrante estiver trabalhando nela.

Ao terminar, envie sua branch e abra um Pull Request quando sua tarefa estiver pronta para integração.

Exemplo:

```text
Assets/Scenes/
  Gameplay.unity
  Gameplay_PlayerInteraction.unity
```

A cópia serve para desenvolver sua alteração sem interferir diretamente na cena que o outro integrante está editando.

Atenção: copiar uma cena não duplica todos os assets referenciados por ela. Prefabs, materiais e outros recursos podem continuar compartilhados. Se você modificar um desses arquivos, a alteração poderá afetar o trabalho do outro integrante.

### Regra 4.3 — Como integrar as alterações de duas pessoas na mesma cena

Quando duas pessoas precisarem modificar a mesma cena, o processo deve ser coordenado.

Exemplo: a pessoa A está organizando o cenário, enquanto a pessoa B precisa adicionar pontos de interação.

### Etapa 1 — A primeira pessoa conclui a cena original.

A pessoa A termina suas alterações, testa a cena e envia um Pull Request para main. Outra pessoa revisa o PR, e as alterações são integradas.

### Etapa 2 — A segunda pessoa atualiza sua branch.

A pessoa B, que trabalhou na cópia da cena, atualiza a main e integra as alterações recentes à sua própria branch:

```sh
git fetch origin
git switch feature/player-interaction
git merge origin/main
```

### Etapa 3 — A segunda pessoa integra sua alteração à cena atualizada.

A pessoa B abre a cena oficial atualizada e transfere para ela as alterações que fez na cópia, preservando as mudanças que a pessoa A já integrou.

A cópia pode ser utilizada como referência para identificar os objetos, componentes e configurações que precisam ser transferidos.

Depois, a pessoa B deve verificar se a cena oficial contém todas as alterações esperadas e se as referências continuam funcionando.

### Etapa 4 — A segunda pessoa testa e envia um novo PR.

A pessoa B testa a cena atualizada, envia sua branch e abre um Pull Request para main.

Outra pessoa revisa o PR. Depois da aprovação, as alterações são integradas à cena oficial.

O processo pode ser resumido assim:

```text
Pessoa A: edita a cena original
              |
              v
      PR revisado e merge
              |
              v
Pessoa B: atualiza sua branch com main
              |
              v
Transfere suas mudanças da cópia
para a cena oficial atualizada
              |
              v
        Testa a cena
              |
              v
      PR revisado e merge
```

A regra principal é: a cópia é um espaço de trabalho isolado, não uma substituição automática da cena oficial. Antes de integrar suas mudanças, você precisa incorporá-las à versão atualizada da cena, preservando as alterações que já foram integradas.

### Regra 4.4 — Não salvar alterações acidentais

Ao abrir uma cena para inspecionar algo, confira se ela ficou marcada como modificada.

Se não pretendia alterá-la, não salve mudanças acidentais. Confira o diff antes de descartar qualquer alteração para não apagar o trabalho de outra pessoa.

### Regra 4.5 — Sinalizar cenas alteradas (opcional)

Sempre informe no Pull Request quando uma cena for modificada.

Exemplo:

Cenas alteradas:

- Gameplay.unity

Motivo:

- Adição dos pontos de spawn do jogador.

## 5. Prefabs: reduzir conflitos e organizar objetos

Prefabs permitem definir um GameObject e seus componentes como um asset reutilizável. São úteis para organizar o projeto e diminuir a necessidade de modificar cenas compartilhadas.

### Regra 5.1 — Transformar objetos reutilizáveis em prefabs

Sempre que fizer sentido, objetos como estes devem ser prefabs:

Jogador e inimigos.

NPCs.

Portas, baús e itens coletáveis.

Armas e projéteis.

Elementos de cenário reutilizáveis.

Componentes visuais recorrentes.

Elementos de interface que possuam configuração e comportamento próprios.

Exemplo:

```text
Assets/Prefabs/
  Characters/
    Robot.prefab
    Enemy.prefab
  Interactables/
    Door.prefab
    Chest.prefab
  Environment/
    BreakableCrate.prefab
  UI/
    HealthBar.prefab
```

A organização deve acompanhar o crescimento do projeto, sem criar pastas excessivas.

### Regra 5.2 — Evitar configurar repetidamente objetos diretamente na cena

Se um objeto possuir vários componentes, configurações ou comportamentos que possam ser reutilizados, prefira criar um prefab e instanciá-lo na cena.

Assim, a cena fica responsável principalmente por organizar as instâncias e os elementos específicos daquele nível.

### Regra 5.3 — Avisar antes de modificar um prefab compartilhado

Prefabs também podem gerar conflitos.

Antes de modificar um prefab utilizado por várias pessoas, avise a equipe.

Exemplo:

Vou modificar Robot.prefab para adicionar o componente de interação. Quem estiver trabalhando no robô ou nas animações, me avise antes de alterar esse arquivo.

### Regra 5.4 — Não criar cópias desnecessárias de prefabs

Não crie Robot2.prefab, RobotFinal.prefab ou RobotFinalNovo.prefab para contornar conflitos.

Se forem necessárias variantes reais, utilize Prefab Variants ou uma estrutura claramente definida.

### Regra 5.5 — Evitar alterações concorrentes na mesma hierarquia

Mesmo quando duas pessoas trabalham em componentes diferentes, alterações no mesmo prefab podem interferir em referências, overrides, componentes ou na hierarquia dos filhos.

Sempre que possível, divida as responsabilidades por GameObjects ou prefabs independentes.

### Regra 5.6 — Conferir os overrides

Antes de integrar uma mudança, confira se os overrides das instâncias estão corretos e se a aplicação das alterações ao prefab original foi intencional.

Não aplique overrides indiscriminadamente, pois isso pode modificar todas as instâncias que utilizam o prefab.

## 6. Scripts e objetos compartilhados

Conflitos em scripts geralmente são mais simples de identificar e resolver do que conflitos em cenas e prefabs, principalmente quando as alterações são pequenas e independentes.

Por isso, não é necessário evitar alterações simultâneas em scripts com o mesmo rigor aplicado às cenas. Ainda assim, algumas boas práticas ajudam a evitar retrabalho.

### Boas práticas

Organize os scripts por responsabilidade e mantenha o código legível.

Prefira componentes reutilizáveis quando fizer sentido.

Avise a equipe quando for modificar scripts ou objetos centrais do projeto.

Verifique se outra pessoa está alterando a mesma lógica antes de modificar métodos, interfaces ou estruturas compartilhadas.

Evite mudanças incompatíveis em métodos públicos sem verificar os lugares que os utilizam.

Por exemplo, se alguém estiver modificando o sistema do robô, avise antes de alterar o RobotController ou seus componentes relacionados.

Isso não significa que outra pessoa esteja proibida de editar esses scripts. O objetivo é apenas evitar que duas pessoas façam alterações incompatíveis sem saber.

### Exemplo de organização

```text
Assets/Scripts/
  Player/
    PlayerMovement.cs
    PlayerHealth.cs
    PlayerInteraction.cs
  Enemies/
    EnemyAI.cs
    EnemyHealth.cs
  UI/
    HealthBarUI.cs
    PauseMenuController.cs
  Systems/
    AudioManager.cs
    SaveSystem.cs
```

A estrutura é ilustrativa e deve acompanhar a arquitetura real do jogo.

## 7. Commits: pequenos, claros e relacionados

Quanto menor o commit, melhor — desde que ele represente uma alteração coerente.

Commits pequenos facilitam a revisão, a identificação de erros e a recuperação de mudanças específicas. Evite acumular muitas alterações diferentes em um único commit.

### Padrão recomendado

Utilizaremos mensagens no formato:

tipo: descrição curta da alteração

### Tipos sugeridos:

feat: nova funcionalidade.

fix: correção de bug.

refactor: reorganização de código.

chore: manutenção e configuração.

docs: documentação.

test: testes.

Exemplos

feat: add robot movement

feat: add pause menu

fix: prevent player falling through floor

refactor: separate player health component

chore: update input settings

docs: document scene workflow

test: add inventory unit tests

### Regras para commits

Escreva mensagens objetivas.

Faça commits que representem mudanças pequenas e relacionadas.

Não misture funcionalidades sem relação no mesmo commit.

Não faça commit de arquivos temporários.

Não faça commit de mudanças que você não entende.

Confira o diff antes de registrar as alterações.

Faça commits durante o desenvolvimento, não apenas no final.

Não inclua alterações de colegas sem autorização.

Comandos úteis:

```sh
git status
git diff
git diff --staged
git add Assets/Scripts/Player/PlayerMovement.cs
git commit -m "feat: add robot movement"
```

Evite utilizar git add . indiscriminadamente. Primeiro confira quais arquivos foram modificados.

## 8. Arquivos do projeto e versionamento

O projeto já possui suas configurações de versionamento. Portanto, não é necessário reconfigurar o .gitignore ou as opções da Unity a cada tarefa.

Durante o desenvolvimento, lembre-se de:

Não excluir arquivos .meta dos assets versionados.

Fazer renomeações e movimentações de assets pelo Unity Editor, sempre que possível.

Não enviar arquivos temporários ou caches desnecessários.

Conferir se as referências aos assets continuam funcionando após renomeações e movimentações.

Manter versionados os arquivos necessários para que o projeto seja aberto corretamente por todos os integrantes.

### Sobre Git LFS

O Git LFS pode ser necessário em casos específicos de arquivos muito grandes ou binários que não sejam adequados ao armazenamento Git convencional.

Utilize Git LFS somente quando for realmente necessário.

Se surgir essa necessidade, avise no grupo antes de configurar. Todos os integrantes que precisarem trabalhar com esses arquivos devem instalar e configurar o Git LFS para conseguir baixar e utilizar os assets corretamente.

Não introduza novas regras de armazenamento sem comunicar a equipe.

Referência: https://git\-lfs\.com/

## 9. Atualização da branch e resolução de conflitos

Antes de abrir um Pull Request, atualize sua branch com as mudanças recentes da main.

Uma opção simples é:

```sh
git fetch origin
git switch feature/robot-interaction
git merge origin/main
```

Se houver conflitos:

Não escolha uma versão automaticamente.

Leia as alterações de ambas as branches.

Converse com o responsável pelo arquivo, quando necessário.

Resolva o conflito preservando as alterações corretas.

Abra o projeto na Unity.

Verifique referências, componentes, prefabs e cenas.

Execute os testes pertinentes.

Faça commit da resolução e envie a branch atualizada.

Nunca faça o merge de uma cena se o Git informou que há conflitos.

## 10. Pull Requests e revisão obrigatória

Toda alteração destinada à main deve passar por um Pull Request.

### O PR deve conter

Título claro.

Descrição do que mudou.

Motivo da alteração.

Arquivos importantes modificados.

Aviso explícito sobre alterações em cenas.

### Checklist de revisão

Antes de aprovar, verifique:

O escopo da tarefa está claro.

O código está organizado e legível.

Não há arquivos temporários (.proj ou .temp) ou alterações acidentais.

Os arquivos .meta necessários estão presentes.

As cenas e prefabs modificados foram revisados.

O comportamento alterado foi testado.

### Regra de aprovação

Todo Pull Request deve ser revisado e aprovado por pelo menos outro integrante da equipe.

O autor do PR não pode ser a única pessoa a aprovar o próprio trabalho. Mesmo em alterações pequenas, outra pessoa deve conferir o que será integrado à main.

Para uma equipe pequena, uma aprovação de outro integrante é suficiente na maioria dos casos. Alterações estruturais importantes podem exigir uma revisão mais cuidadosa.

Se o repositório estiver no GitHub, configure uma regra de proteção para a main:

Exigir Pull Request antes do merge.

Exigir uma aprovação, quando disponível e adequado à equipe.

Impedir force push e exclusão da branch.

Exigir a resolução das conversas de revisão, quando disponível.

Isso reduz a possibilidade de alguém enviar alterações diretamente para a branch principal

## 11. Fluxo resumido

```text
main atualizada
      |
      v
Criar branch da tarefa
      |
      v
Verificar arquivos compartilhados
      |
      v
Desenvolver e testar
      |
      v
Commits pequenos e claros
      |
      v
Push da branch
      |
      v
Abrir Pull Request
      |
      v
Revisão por outro integrante
      |
      v
Resolver conflitos, se houver
      |
      v
Merge na main
      |
      v
Preservar a branch para referência
```

### Fluxo específico para cenas compartilhadas

```text
Pessoa A edita a cena original
              |
              v
      PR revisado e merge
              |
              v
Pessoa B atualiza sua branch
com as alterações da main
              |
              v
Transfere as mudanças da cópia
para a cena oficial atualizada
              |
              v
        Testa a cena
              |
              v
      PR revisado e merge
```

### Conclusão

O objetivo desse workflow não é impedir que duas pessoas trabalhem em sistemas relacionados, mas garantir que o trabalho de cada integrante seja preservado e integrado corretamente.

Lembrem-se dos pontos principais:

Criem uma branch para cada tarefa.

Mantenham a main estável.

Preservem as branches após o merge.

Evitem editar a mesma cena simultaneamente.

Se precisarem trabalhar na mesma cena, utilizem uma cópia e transfiram as alterações para a versão oficial atualizada antes de abrir o PR.

Utilizem prefabs sempre que fizer sentido.

Avisem antes de modificar objetos e scripts centrais ou compartilhados.

Façam commits pequenos e claros.

Exijam a revisão de outro integrante antes de integrar um Pull Request.

Antes de editar algo compartilhado, conversem. Antes de comitar, confiram. Antes de integrar, testem.

O Git serve para proteger nosso trabalho, não para substituir a comunicação da equipe.
