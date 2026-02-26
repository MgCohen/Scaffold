# Documento de Contexto do Projeto: Jogo Stealth Assimétrico (TripleZ Stealth)

## 1. Visão Geral do Projeto
- **Gênero:** Multiplayer Online, Stealth Assimétrico, Ação Tática.
- **Formato das Partidas:** 3v3 (3 Invasores contra 3 Guardas).
- **Engine Padrão:** Unity.
- **Linguagem de Programação:** C#.
- **Framework de Rede (Multiplayer):** FishNet.
- **Estilo/Temática:** Ambiente militar moderno com um tom semi-realista.
- **Estado Atual (Fase de Desenvolvimento):** Early Stage. O jogo já conta com um sistema de Lobby baseado em códigos de sala onde os jogadores escolhem seus times antes de iniciar a partida.

## 2. Facções e Perspectivas

### 2.1. Invasores (Invaders)
- **Perspectiva de Câmera:** Terceira Pessoa (Third-Person / TPS).
- **Mecânicas e Habilidades:**
  - Altamente ágeis: possuem mecânicas de *Climbing* (escalada) e *Parkour*.
  - Capacidade de utilizar sistema de *Hacking* para interagir com os objetivos do cenário.
  - Acesso a *Gadgets* específicos da classe/time.
  - Combate armado (atiradores).

### 2.2. Guardas (Guards)
- **Perspectiva de Câmera:** Primeira Pessoa (First-Person / FPS).
- **Mecânicas e Velocidade:**
  - Movimentação mais restrita/tática (andar, correr/sprint, agachar). Não possuem mecânicas avançadas de parkour.
  - Foco em patrulha, defesa e interceptação.
  - Acesso a *Gadgets* específicos focados em defesa/detecção.
  - Combate armado.

## 3. Dinâmica da Partida e Condições de Vitória
O jogo funciona em uma dinâmica de ataque contra defesa com tempo limite.  

- **Condições de Vitória dos Invasores:**
  1. Completar os objetivos de *Hack* no cenário com sucesso.
  2. **OU** Eliminar todos os integrantes da equipe dos Guardas.

- **Condições de Vitória dos Guardas:**
  1. Eliminar todos os integrantes da equipe de Invasores.
  2. **OU** Sobreviver e proteger os objetivos até que o Tempo Limite da partida se esgote (Time Out).

## 4. Diretrizes Técnicas para a IA (Prompting)
- Ao escrever ou refatorar código, utilize **C#** focado no ecossistema da **Unity**.
- Ao sugerir arquiteturas de rede (Networking), considere que o jogo possui elementos multiplayer (lobby, sincronização de movimentação, estado de jogo e replicação de gadgets/armas), utilizando **FishNet** como framework base.
- Código deve ser modular e limpo, separando a lógica de input, movimentação local e sincronização de rede.
- Cuidado com as diferenças de implementação de mira e câmera, visto que uma classe usa Third-Person e a outra usa First-Person: os Raycasts, projéteis e lógicas de Hitbox podem variar dependendo do time do jogador instanciado.
