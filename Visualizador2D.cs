using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace HologramaGravitacional
{
    // Visualizador 2D em Windows Forms.
    //
    // Desenha os quadros fornecidos por uma IFonteQuadros. A mesma
    // janela é usada para:
    //   - acompanhar uma simulação em andamento (FonteSimulacaoAoVivo);
    //   - fazer o replay de uma simulação gravada (FonteReplay).
    //
    // Possui uma linha do tempo, como um player de vídeo: é possível
    // reproduzir, pausar, rebobinar, avançar/voltar um passo e arrastar
    // até qualquer iteração. Colisões aparecem marcadas na linha do tempo.
    //
    // O visual segue o tema "holograma": fundo espacial estrelado,
    // grade translúcida, corpos coloridos pela densidade com brilho e
    // rastro das trajetórias.
    //
    // Importante: o visualizador NÃO altera a física. Voltar no tempo é
    // apenas reexibir quadros que já foram calculados ou carregados.
    public class Visualizador2D
    {
        private readonly IFonteQuadros fonte;

        public Visualizador2D(IFonteQuadros fonte)
        {
            this.fonte = fonte;
        }

        public void Run()
        {
            using (var form = new VisualizadorForm(fonte))
            {
                Application.Run(form);
            }
        }

        private sealed class VisualizadorForm : Form
        {
            // ============================================================
            // PALETA E CONSTANTES VISUAIS
            // ============================================================

            private static readonly Color CorFundo = Color.FromArgb(7, 10, 20);
            private static readonly Color CorPainel = Color.FromArgb(15, 21, 38);
            private static readonly Color CorBotao = Color.FromArgb(28, 37, 64);
            private static readonly Color CorBotaoHover = Color.FromArgb(44, 58, 98);
            private static readonly Color CorBotaoAtivo = Color.FromArgb(20, 110, 105);
            private static readonly Color CorDestaque = Color.FromArgb(94, 234, 212);
            private static readonly Color CorAviso = Color.FromArgb(251, 191, 36);
            private static readonly Color CorSucesso = Color.FromArgb(74, 222, 128);
            private static readonly Color CorRebobinar = Color.FromArgb(167, 139, 250);
            private static readonly Color CorColisao = Color.FromArgb(244, 63, 94);
            private static readonly Color CorTexto = Color.FromArgb(226, 232, 240);
            private static readonly Color CorTextoSuave = Color.FromArgb(148, 163, 184);

            // Faixa de densidade usada pelo gerador (kg/m³): do hidrogênio
            // ao ósmio. Usada para escolher a cor de cada corpo.
            private const double DensidadeMinima = 0.0899;
            private const double DensidadeMaxima = 22590;

            private const int AlturaBarra = 50;      // barra de botões (topo)
            private const int AlturaLinhaTempo = 64; // linha do tempo (base)

            // Quantos quadros anteriores formam o rastro de cada corpo.
            private const int TamanhoRastro = 45;

            // Acima disto, rastros e nomes começam desligados para
            // manter a animação fluida.
            private const int LimiteCorposDetalhes = 300;

            // ============================================================
            // ESTADO DA REPRODUÇÃO
            // ============================================================

            private readonly IFonteQuadros fonte;
            private readonly System.Windows.Forms.Timer timer;

            // Quadro exibido no momento (posição na linha do tempo).
            private int indice;

            private bool reproduzindo = true;
            private int direcao = 1;      // +1 = para frente, -1 = rebobinando
            private int passoPendente;    // +1/-1 quando o usuário pede um passo
            private int iteracoesPorQuadro = 1; // multiplicador de velocidade

            // Animação suave: a tela é redesenhada a cada ~16 ms (60 quadros/s),
            // mas uma iteração dura "intervaloBaseMs". Entre duas iterações, as
            // posições são interpoladas; "progresso" (0 a 1) diz quanto do
            // caminho até a próxima iteração já foi percorrido.
            private const int IntervaloTelaMs = 16;
            private readonly int intervaloBaseMs;
            private double progresso;

            private bool mostrarRastros;
            private bool mostrarNomes;

            // Índices dos quadros em que o número de corpos diminuiu
            // (houve fusão). Viram marcadores na linha do tempo.
            private readonly List<int> marcadoresColisao = new List<int>();
            private int ultimoQuadroAnalisado;

            // Estrelas do fundo: posição relativa (0..1) e brilho.
            private readonly (float X, float Y, float Tamanho, int Brilho)[] estrelas;

            // ============================================================
            // ESTADO DA CÂMERA E DO MOUSE
            // ============================================================

            // A escala "alvo" enquadra todos os corpos; a câmera se move
            // suavemente até ela (interpolação), evitando trancos.
            private bool cameraIniciada;
            private double cameraX;
            private double cameraY;
            private double cameraEscala = 1;
            private double zoom = 1.0;
            private double deslocamentoX; // arrasto do usuário, em metros
            private double deslocamentoY;
            private Point? inicioArraste;

            private bool arrastandoLinhaTempo;
            private Point posicaoMouse;
            private bool mouseSobreCena;
            private bool mouseSobreLinhaTempo;

            // ============================================================
            // CONTROLES E FONTES
            // ============================================================

            private readonly Button botaoReproduzir;
            private readonly Button botaoRebobinar;
            private readonly Button botaoRastros;
            private readonly Button botaoNomes;
            private readonly Label rotuloVelocidade;

            // Fontes criadas uma vez só (criar a cada quadro é lento).
            private readonly Font fonteHud = new Font("Segoe UI", 9f);
            private readonly Font fonteHudNegrito = new Font("Segoe UI Semibold", 10.5f);
            private readonly Font fonteTitulo = new Font("Segoe UI Semibold", 12f);
            private readonly Font fonteBadge = new Font("Segoe UI Semibold", 8.5f);
            private readonly Font fonteNome = new Font("Segoe UI", 7.5f);
            private readonly Font fonteIteracao = new Font("Segoe UI Semibold", 14f);

            public VisualizadorForm(IFonteQuadros fonte)
            {
                this.fonte = fonte;

                Text = $"Holograma Gravitacional — {fonte.Titulo}";
                DoubleBuffered = true;
                BackColor = CorFundo;
                Width = 1240;
                Height = 840;
                MinimumSize = new Size(1000, 640);
                StartPosition = FormStartPosition.CenterScreen;

                int quantidadeInicial = fonte.ObterQuadro(0).Corpos.Count;
                mostrarRastros = quantidadeInicial <= LimiteCorposDetalhes;
                mostrarNomes = quantidadeInicial <= 12;

                // Semente fixa: o céu é sempre o mesmo, não "pisca" entre execuções.
                var aleatorio = new Random(42);
                estrelas = new (float, float, float, int)[260];

                for (int i = 0; i < estrelas.Length; i++)
                {
                    estrelas[i] = (
                        (float)aleatorio.NextDouble(),
                        (float)aleatorio.NextDouble(),
                        (float)(aleatorio.NextDouble() * 1.6 + 0.4),
                        aleatorio.Next(40, 200)
                    );
                }

                // ========================================================
                // BARRA DE FERRAMENTAS
                // ========================================================

                var barra = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    Height = AlturaBarra,
                    BackColor = CorPainel,
                    Padding = new Padding(12, 9, 12, 0),
                    WrapContents = false
                };
                Controls.Add(barra);

                var dicas = new ToolTip();

                // --- Controles de reprodução (estilo player de vídeo) ---

                var botaoInicio = CriarBotao("|◀", 40);
                botaoInicio.Click += (s, e) => IrParaInicio();
                dicas.SetToolTip(botaoInicio, "Voltar ao início (Home)");
                barra.Controls.Add(botaoInicio);

                botaoRebobinar = CriarBotao("◀◀  Rebobinar", 116);
                botaoRebobinar.Click += (s, e) => AlternarRebobinar();
                dicas.SetToolTip(botaoRebobinar, "Reproduzir de trás para frente (B)");
                barra.Controls.Add(botaoRebobinar);

                var botaoVoltar = CriarBotao("‹", 36);
                botaoVoltar.Click += (s, e) => SolicitarPasso(-1);
                dicas.SetToolTip(botaoVoltar, "Voltar uma iteração (seta ←)");
                barra.Controls.Add(botaoVoltar);

                botaoReproduzir = CriarBotao("‖  Pausar", 112);
                botaoReproduzir.Click += (s, e) => AlternarReproducao();
                dicas.SetToolTip(botaoReproduzir, "Reproduzir / pausar (Espaço)");
                barra.Controls.Add(botaoReproduzir);

                var botaoAvancar = CriarBotao("›", 36);
                botaoAvancar.Click += (s, e) => SolicitarPasso(+1);
                dicas.SetToolTip(botaoAvancar, "Avançar uma iteração (seta →)");
                barra.Controls.Add(botaoAvancar);

                var botaoFim = CriarBotao("▶|", 40);
                botaoFim.Click += (s, e) => IrParaFim();
                dicas.SetToolTip(botaoFim, "Ir para a última iteração disponível (End)");
                barra.Controls.Add(botaoFim);

                barra.Controls.Add(CriarSeparador());

                // --- Câmera ---

                var botaoMais = CriarBotao("+", 36);
                botaoMais.Click += (s, e) => AplicarZoom(1.25);
                dicas.SetToolTip(botaoMais, "Aproximar (+ ou roda do mouse)");
                barra.Controls.Add(botaoMais);

                var botaoMenos = CriarBotao("−", 36);
                botaoMenos.Click += (s, e) => AplicarZoom(1 / 1.25);
                dicas.SetToolTip(botaoMenos, "Afastar (− ou roda do mouse)");
                barra.Controls.Add(botaoMenos);

                var botaoRecentrar = CriarBotao("Recentrar", 92);
                botaoRecentrar.Click += (s, e) => Recentrar();
                dicas.SetToolTip(botaoRecentrar, "Enquadrar todos os corpos (R ou duplo clique)");
                barra.Controls.Add(botaoRecentrar);

                barra.Controls.Add(CriarSeparador());

                // --- Exibição ---

                botaoRastros = CriarBotao("Rastros", 78);
                botaoRastros.Click += (s, e) => AlternarRastros();
                dicas.SetToolTip(botaoRastros, "Mostrar a trajetória dos corpos (T)");
                barra.Controls.Add(botaoRastros);

                botaoNomes = CriarBotao("Nomes", 70);
                botaoNomes.Click += (s, e) => AlternarNomes();
                dicas.SetToolTip(botaoNomes, "Mostrar o nome de cada corpo (N)");
                barra.Controls.Add(botaoNomes);

                barra.Controls.Add(CriarSeparador());

                // --- Velocidade ---

                barra.Controls.Add(new Label
                {
                    Text = "Velocidade",
                    ForeColor = CorTextoSuave,
                    Font = fonteHud,
                    AutoSize = true,
                    Margin = new Padding(0, 8, 4, 0)
                });

                // Quantas iterações são avançadas (ou voltadas) a cada
                // atualização da tela. Acelera a animação sem mudar o Δt.
                var barraVelocidade = new TrackBar
                {
                    Minimum = 1,
                    Maximum = 50,
                    Value = 1,
                    TickStyle = TickStyle.None,
                    AutoSize = false,
                    Width = 150,
                    Height = 30,
                    BackColor = CorPainel,
                    TabStop = false,
                    Margin = new Padding(0, 2, 0, 0)
                };
                barraVelocidade.Scroll += (s, e) =>
                {
                    iteracoesPorQuadro = barraVelocidade.Value;
                    rotuloVelocidade!.Text = $"{iteracoesPorQuadro}×";
                };
                dicas.SetToolTip(barraVelocidade, "Iterações por quadro");
                barra.Controls.Add(barraVelocidade);

                rotuloVelocidade = new Label
                {
                    Text = "1×",
                    ForeColor = CorDestaque,
                    Font = fonteHudNegrito,
                    AutoSize = true,
                    Margin = new Padding(2, 7, 0, 0)
                };
                barra.Controls.Add(rotuloVelocidade);

                AtualizarBotoes();

                // ========================================================
                // MOUSE
                // ========================================================

                // Zoom pela roda do mouse.
                MouseWheel += (s, e) => AplicarZoom(e.Delta > 0 ? 1.15 : 1 / 1.15);

                MouseDown += (s, e) =>
                {
                    if (e.Button != MouseButtons.Left)
                    {
                        return;
                    }

                    // Clique na linha do tempo: pula para aquele ponto
                    // e continua acompanhando enquanto o botão estiver pressionado.
                    if (AreaClicavelLinhaTempo().Contains(e.Location))
                    {
                        arrastandoLinhaTempo = true;
                        IrParaPosicaoNaLinha(e.X);
                        return;
                    }

                    // Clique na cena: começa a mover a câmera.
                    if (e.Y > AlturaBarra && e.Y < ClientSize.Height - AlturaLinhaTempo)
                    {
                        inicioArraste = e.Location;
                        Cursor = Cursors.SizeAll;
                    }
                };

                MouseMove += (s, e) =>
                {
                    posicaoMouse = e.Location;
                    mouseSobreCena = e.Y > AlturaBarra && e.Y < ClientSize.Height - AlturaLinhaTempo;
                    mouseSobreLinhaTempo = AreaClicavelLinhaTempo().Contains(e.Location);

                    if (arrastandoLinhaTempo)
                    {
                        IrParaPosicaoNaLinha(e.X);
                    }
                    else if (inicioArraste.HasValue && cameraEscala > 0)
                    {
                        double escala = cameraEscala * zoom;
                        deslocamentoX -= (e.X - inicioArraste.Value.X) / escala;
                        deslocamentoY -= (e.Y - inicioArraste.Value.Y) / escala;
                        inicioArraste = e.Location;
                    }

                    Cursor = arrastandoLinhaTempo || mouseSobreLinhaTempo
                        ? Cursors.Hand
                        : inicioArraste.HasValue ? Cursors.SizeAll : Cursors.Default;
                };

                MouseUp += (s, e) =>
                {
                    arrastandoLinhaTempo = false;
                    inicioArraste = null;
                    Cursor = Cursors.Default;
                };

                MouseLeave += (s, e) =>
                {
                    mouseSobreCena = false;
                    mouseSobreLinhaTempo = false;
                };

                MouseDoubleClick += (s, e) =>
                {
                    if (e.Y < ClientSize.Height - AlturaLinhaTempo)
                    {
                        Recentrar();
                    }
                };

                // ========================================================
                // TEMPORIZADOR
                // ========================================================

                // Intervalo entre atualizações da tela. Não usa
                // TempoEntreIteracoes, que é tempo físico simulado e pode
                // valer centenas de segundos.
                // O ritmo depende das iterações CONFIGURADAS (e não das
                // gravadas), para o replay andar na mesma velocidade da
                // simulação original.
                intervaloBaseMs = CalcularIntervalo(fonte.QuadrosConfigurados);
                timer = new System.Windows.Forms.Timer { Interval = IntervaloTelaMs };
                timer.Tick += Timer_Tick;
                Shown += (s, e) => timer.Start();
                FormClosed += (s, e) => timer.Stop();
            }

            // Duração de cada iteração na animação (velocidade 1×).
            // Simulações curtas terminariam rápido demais a 30 ms por
            // iteração (20 iterações = 0,6 s). Por isso o intervalo é
            // escolhido para a animação durar pelo menos ~8 segundos,
            // entre 30 ms e 400 ms por iteração. Simulações longas ficam
            // em 30 ms e podem ser aceleradas pela barra de velocidade.
            private static int CalcularIntervalo(int totalQuadros)
            {
                const double duracaoMinimaMs = 8000;

                if (totalQuadros <= 1)
                {
                    return 30;
                }

                return (int)Math.Clamp(duracaoMinimaMs / totalQuadros, 30, 400);
            }

            // ============================================================
            // CRIAÇÃO DE CONTROLES
            // ============================================================

            private Button CriarBotao(string texto, int largura)
            {
                var botao = new Button
                {
                    Text = texto,
                    Width = largura,
                    Height = 32,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = CorBotao,
                    ForeColor = CorTexto,
                    Font = new Font("Segoe UI", 9.5f),
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 0, 6, 0),

                    // Sem foco pelo teclado: assim a barra de espaço pausa a
                    // simulação em vez de "clicar" o último botão usado.
                    TabStop = false
                };

                botao.FlatAppearance.BorderSize = 0;
                botao.FlatAppearance.MouseOverBackColor = CorBotaoHover;
                botao.FlatAppearance.MouseDownBackColor = CorBotaoAtivo;

                return botao;
            }

            private static Control CriarSeparador()
            {
                return new Panel
                {
                    Width = 1,
                    Height = 26,
                    BackColor = Color.FromArgb(45, 55, 80),
                    Margin = new Padding(6, 3, 12, 0)
                };
            }

            // ============================================================
            // NAVEGAÇÃO NA LINHA DO TEMPO
            // ============================================================

            // Move o quadro atual "delta" posições (+1 avança, -1 volta).
            // Avançar além dos quadros existentes pede à fonte para calcular
            // mais um (só a simulação ao vivo consegue). Retorna false quando
            // chega a uma das pontas.
            private bool Mover(int delta)
            {
                int alvo = indice + delta;

                if (alvo < 0)
                {
                    return false;
                }

                if (alvo >= fonte.QuadrosDisponiveis && !fonte.GerarProximo())
                {
                    return false;
                }

                indice = alvo;
                return true;
            }

            private void IrPara(int novoIndice)
            {
                indice = Math.Clamp(novoIndice, 0, fonte.QuadrosDisponiveis - 1);
                progresso = 0;
            }

            private void IrParaInicio()
            {
                reproduzindo = false;
                IrPara(0);
                AtualizarBotoes();
            }

            // Vai para o último quadro EXISTENTE. Na simulação ao vivo, o
            // restante continua sendo calculado ao reproduzir.
            private void IrParaFim()
            {
                reproduzindo = false;
                IrPara(fonte.QuadrosDisponiveis - 1);
                AtualizarBotoes();
            }

            // Converte a posição X do mouse em um quadro da linha do tempo.
            // Não é possível pular para quadros que ainda não foram calculados.
            private void IrParaPosicaoNaLinha(int x)
            {
                RectangleF trilha = RetanguloTrilha();
                double fracao = Math.Clamp((x - trilha.Left) / trilha.Width, 0, 1);
                int alvo = (int)Math.Round(fracao * (fonte.TotalQuadrosPrevistos - 1));

                IrPara(alvo);
            }

            private void AlternarReproducao()
            {
                // Mudar de estado no meio de uma interpolação causaria um salto.
                progresso = 0;

                // Se estava rebobinando, o botão volta a tocar para frente.
                if (reproduzindo && direcao < 0)
                {
                    direcao = 1;
                }
                else
                {
                    reproduzindo = !reproduzindo;
                    direcao = 1;

                    // No fim da linha do tempo, "reproduzir" recomeça do início.
                    if (reproduzindo && NoFim())
                    {
                        IrPara(0);
                    }
                }

                AtualizarBotoes();
            }

            private void AlternarRebobinar()
            {
                progresso = 0;

                if (reproduzindo && direcao < 0)
                {
                    reproduzindo = false;
                }
                else
                {
                    reproduzindo = true;
                    direcao = -1;
                }

                AtualizarBotoes();
            }

            private void SolicitarPasso(int delta)
            {
                reproduzindo = false;
                passoPendente = delta;
                AtualizarBotoes();
            }

            private bool NoFim()
            {
                return indice >= fonte.TotalQuadrosPrevistos - 1;
            }

            // ============================================================
            // OUTRAS AÇÕES
            // ============================================================

            private void AplicarZoom(double fator)
            {
                zoom = Math.Clamp(zoom * fator, 0.02, 500);
            }

            private void Recentrar()
            {
                zoom = 1;
                deslocamentoX = 0;
                deslocamentoY = 0;
            }

            private void AlternarRastros()
            {
                mostrarRastros = !mostrarRastros;
                AtualizarBotoes();
            }

            private void AlternarNomes()
            {
                mostrarNomes = !mostrarNomes;
                AtualizarBotoes();
            }

            // Ajusta texto e cor dos botões conforme o estado atual.
            private void AtualizarBotoes()
            {
                bool tocandoParaFrente = reproduzindo && direcao > 0;
                bool rebobinando = reproduzindo && direcao < 0;

                botaoReproduzir.Text = tocandoParaFrente ? "‖  Pausar" : "▶  Reproduzir";
                botaoRebobinar.BackColor = rebobinando ? Color.FromArgb(91, 70, 170) : CorBotao;
                botaoRastros.BackColor = mostrarRastros ? CorBotaoAtivo : CorBotao;
                botaoNomes.BackColor = mostrarNomes ? CorBotaoAtivo : CorBotao;
            }

            // Atalhos de teclado. ProcessCmdKey é chamado antes de os
            // controles tratarem a tecla, então funciona com qualquer foco.
            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                switch (keyData)
                {
                    case Keys.Space:
                        AlternarReproducao();
                        return true;
                    case Keys.Right:
                        SolicitarPasso(+1);
                        return true;
                    case Keys.Left:
                        SolicitarPasso(-1);
                        return true;
                    case Keys.Home:
                        IrParaInicio();
                        return true;
                    case Keys.End:
                        IrParaFim();
                        return true;
                    case Keys.B:
                        AlternarRebobinar();
                        return true;
                    case Keys.Add:
                    case Keys.Oemplus:
                        AplicarZoom(1.25);
                        return true;
                    case Keys.Subtract:
                    case Keys.OemMinus:
                        AplicarZoom(1 / 1.25);
                        return true;
                    case Keys.R:
                        Recentrar();
                        return true;
                    case Keys.T:
                        AlternarRastros();
                        return true;
                    case Keys.N:
                        AlternarNomes();
                        return true;
                }

                return base.ProcessCmdKey(ref msg, keyData);
            }

            // ============================================================
            // ANIMAÇÃO
            // ============================================================

            private void Timer_Tick(object? sender, EventArgs e)
            {
                // Enquanto o usuário arrasta a linha do tempo, quem
                // controla o quadro é o mouse.
                if (!arrastandoLinhaTempo)
                {
                    if (passoPendente != 0)
                    {
                        Mover(passoPendente);
                        passoPendente = 0;
                        progresso = 0;
                    }
                    else if (reproduzindo)
                    {
                        // Avança a fração de iteração correspondente a este
                        // quadro de tela (mais rápido com a barra de velocidade).
                        progresso += (double)IntervaloTelaMs / intervaloBaseMs * iteracoesPorQuadro;

                        while (progresso >= 1)
                        {
                            progresso -= 1;

                            // Chegou ao início (rebobinando) ou ao fim: para.
                            if (!Mover(direcao))
                            {
                                reproduzindo = false;
                                progresso = 0;
                                AtualizarBotoes();
                                break;
                            }
                        }

                        // Na simulação ao vivo, calcula com antecedência a
                        // próxima iteração, para ter para onde interpolar.
                        if (reproduzindo && direcao > 0 && indice + 1 >= fonte.QuadrosDisponiveis)
                        {
                            fonte.GerarProximo();
                        }
                    }
                }

                AtualizarMarcadoresColisao();

                Text =
                    $"Holograma Gravitacional — {fonte.Titulo} — " +
                    $"Iteração {fonte.ObterQuadro(indice).Iteracao}";

                // O desenho continua mesmo pausado: a câmera ainda se
                // move suavemente e o mouse ainda mostra detalhes.
                Invalidate();
            }

            // Posições a desenhar neste instante: interpoladas entre o quadro
            // atual e o próximo (ou o anterior, ao rebobinar).
            //
            // Cada corpo é localizado no quadro vizinho pelo nome. Corpos que
            // não existem no vizinho (porque se fundiram) ficam parados: assim
            // os dois corpos aparecem encostados até o instante da fusão.
            private IReadOnlyList<EstadoCorpo> ObterCorposExibidos()
            {
                IReadOnlyList<EstadoCorpo> atuais = fonte.ObterQuadro(indice).Corpos;
                int vizinho = indice + direcao;

                if (!reproduzindo || progresso <= 0 || vizinho < 0 || vizinho >= fonte.QuadrosDisponiveis)
                {
                    return atuais;
                }

                var proximos = new Dictionary<string, EstadoCorpo>();
                foreach (EstadoCorpo corpo in fonte.ObterQuadro(vizinho).Corpos)
                {
                    proximos[corpo.Nome] = corpo;
                }

                var resultado = new List<EstadoCorpo>(atuais.Count);

                foreach (EstadoCorpo corpo in atuais)
                {
                    if (proximos.TryGetValue(corpo.Nome, out EstadoCorpo? proximo))
                    {
                        // Interpolação linear: posição = atual + (próxima − atual) × progresso.
                        // "with" cria uma cópia do record alterando só esses campos.
                        resultado.Add(corpo with
                        {
                            PosX = corpo.PosX + (proximo.PosX - corpo.PosX) * progresso,
                            PosY = corpo.PosY + (proximo.PosY - corpo.PosY) * progresso
                        });
                    }
                    else
                    {
                        resultado.Add(corpo);
                    }
                }

                return resultado;
            }

            // Procura, nos quadros novos, os pontos em que o número de
            // corpos diminuiu — ou seja, onde aconteceram fusões.
            private void AtualizarMarcadoresColisao()
            {
                for (int i = ultimoQuadroAnalisado + 1; i < fonte.QuadrosDisponiveis; i++)
                {
                    if (fonte.ObterQuadro(i).Corpos.Count < fonte.ObterQuadro(i - 1).Corpos.Count)
                    {
                        marcadoresColisao.Add(i);
                    }

                    ultimoQuadroAnalisado = i;
                }
            }

            // ============================================================
            // CÂMERA
            // ============================================================

            // Calcula o enquadramento ideal (centro e escala que mostram
            // todos os corpos) e aproxima a câmera dele aos poucos.
            private void AtualizarCamera(IReadOnlyList<EstadoCorpo> corpos, int largura, int altura)
            {
                double minX = corpos.Min(c => c.PosX);
                double maxX = corpos.Max(c => c.PosX);
                double minY = corpos.Min(c => c.PosY);
                double maxY = corpos.Max(c => c.PosY);

                // Evita escala infinita quando há um único corpo
                // (ou todos no mesmo ponto).
                double maiorRaio = corpos.Max(c => c.Raio);
                double extensaoMinima = Math.Max(maiorRaio * 10, 1.0);

                double extensaoX = Math.Max(maxX - minX, extensaoMinima) * 1.3;
                double extensaoY = Math.Max(maxY - minY, extensaoMinima) * 1.3;

                double alvoX = (minX + maxX) / 2;
                double alvoY = (minY + maxY) / 2;
                double alvoEscala = Math.Min(largura / extensaoX, altura / extensaoY);

                if (!cameraIniciada)
                {
                    cameraX = alvoX;
                    cameraY = alvoY;
                    cameraEscala = alvoEscala;
                    cameraIniciada = true;
                    return;
                }

                // Interpolação: a cada quadro anda 12% do caminho até o alvo.
                // A escala é interpolada em escala logarítmica para o zoom
                // parecer uniforme tanto ao aproximar quanto ao afastar.
                const double suavidade = 0.12;
                cameraX += (alvoX - cameraX) * suavidade;
                cameraY += (alvoY - cameraY) * suavidade;
                cameraEscala = Math.Exp(
                    Math.Log(cameraEscala) +
                    (Math.Log(alvoEscala) - Math.Log(cameraEscala)) * suavidade);
            }

            // Converte coordenadas do mundo (metros) para pixels da tela.
            private PointF ParaTela(double x, double y, double escala, int largura, int altura)
            {
                return new PointF(
                    (float)((x - cameraX - deslocamentoX) * escala + largura / 2.0),
                    (float)((y - cameraY - deslocamentoY) * escala + altura / 2.0 + AlturaBarra)
                );
            }

            // ============================================================
            // DESENHO
            // ============================================================

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(CorFundo);

                // Área da cena: entre a barra de botões e a linha do tempo.
                int largura = ClientSize.Width;
                int altura = ClientSize.Height - AlturaBarra - AlturaLinhaTempo;

                if (largura <= 0 || altura <= 0)
                {
                    return;
                }

                int baseCena = AlturaBarra + altura;

                DesenharEstrelas(g, largura, altura);

                IReadOnlyList<EstadoCorpo> corpos = ObterCorposExibidos();

                if (corpos.Count > 0)
                {
                    AtualizarCamera(corpos, largura, altura);

                    double escala = Math.Clamp(cameraEscala * zoom, 1e-12, 1e9);

                    // Espaçamento "redondo" (1, 2 ou 5 × 10^n metros) próximo
                    // de 120 px; usado pela grade e pela régua de escala.
                    double passoGrade = NumeroRedondo(120 / escala);

                    DesenharGrade(g, escala, passoGrade, largura, altura);

                    if (mostrarRastros)
                    {
                        DesenharRastros(g, corpos, escala, largura, altura);
                    }

                    EstadoCorpo? corpoSobMouse = DesenharCorpos(g, corpos, escala, largura, altura);

                    DesenharPainelInformacoes(g, corpos.Count, baseCena);
                    DesenharLegenda(g, escala, passoGrade, largura, baseCena);

                    if (corpoSobMouse != null)
                    {
                        PointF centro = ParaTela(corpoSobMouse.PosX, corpoSobMouse.PosY, escala, largura, altura);
                        float raio = Math.Max((float)(corpoSobMouse.Raio * escala), 3f);
                        DesenharDetalhesCorpo(g, corpoSobMouse, centro, raio, largura);
                    }
                }

                DesenharAjuda(g);
                DesenharSelo(g, largura);
                DesenharLinhaTempo(g);
            }

            private void DesenharEstrelas(Graphics g, int largura, int altura)
            {
                using var pincel = new SolidBrush(Color.White);

                foreach (var estrela in estrelas)
                {
                    pincel.Color = Color.FromArgb(estrela.Brilho, 200, 215, 255);
                    g.FillEllipse(
                        pincel,
                        estrela.X * largura,
                        AlturaBarra + estrela.Y * altura,
                        estrela.Tamanho,
                        estrela.Tamanho);
                }
            }

            // Grade translúcida em ciano, como a de um holograma.
            private void DesenharGrade(Graphics g, double escala, double passo, int largura, int altura)
            {
                using var caneta = new Pen(Color.FromArgb(18, CorDestaque), 1f);

                // Coordenadas do mundo visíveis nas bordas da cena.
                double mundoEsquerda = cameraX + deslocamentoX - (largura / 2.0) / escala;
                double mundoDireita = cameraX + deslocamentoX + (largura / 2.0) / escala;
                double mundoTopo = cameraY + deslocamentoY - (altura / 2.0) / escala;
                double mundoBase = cameraY + deslocamentoY + (altura / 2.0) / escala;

                // Proteção: nunca desenhar mais que ~200 linhas.
                if ((mundoDireita - mundoEsquerda) / passo > 200)
                {
                    return;
                }

                for (double x = Math.Floor(mundoEsquerda / passo) * passo; x <= mundoDireita; x += passo)
                {
                    float px = ParaTela(x, 0, escala, largura, altura).X;
                    g.DrawLine(caneta, px, AlturaBarra, px, AlturaBarra + altura);
                }

                for (double y = Math.Floor(mundoTopo / passo) * passo; y <= mundoBase; y += passo)
                {
                    float py = ParaTela(0, y, escala, largura, altura).Y;
                    g.DrawLine(caneta, 0, py, largura, py);
                }
            }

            // O rastro é montado a partir dos quadros anteriores ao atual.
            // Por isso ele também "volta" corretamente quando o usuário
            // rebobina ou pula para outro ponto da linha do tempo.
            private void DesenharRastros(
                Graphics g, IReadOnlyList<EstadoCorpo> corpos, double escala, int largura, int altura)
            {
                var pontosPorCorpo = corpos.ToDictionary(c => c.Nome, c => new List<PointF>());

                for (int i = Math.Max(0, indice - TamanhoRastro); i <= indice; i++)
                {
                    foreach (EstadoCorpo anterior in fonte.ObterQuadro(i).Corpos)
                    {
                        if (pontosPorCorpo.TryGetValue(anterior.Nome, out var lista))
                        {
                            lista.Add(ParaTela(anterior.PosX, anterior.PosY, escala, largura, altura));
                        }
                    }
                }

                foreach (EstadoCorpo corpo in corpos)
                {
                    PointF[] pontos = pontosPorCorpo[corpo.Nome].ToArray();

                    if (pontos.Length < 2)
                    {
                        continue;
                    }

                    Color cor = CorPorDensidade(corpo.Densidade);

                    // O rastro é dividido em 3 trechos, do mais antigo
                    // (mais transparente) ao mais recente (mais visível),
                    // criando o efeito de "cauda" que some aos poucos.
                    const int trechos = 3;
                    int tamanhoTrecho = Math.Max(2, pontos.Length / trechos);

                    for (int t = 0; t < trechos; t++)
                    {
                        int inicio = t * tamanhoTrecho;
                        int fim = t == trechos - 1
                            ? pontos.Length
                            : Math.Min(pontos.Length, inicio + tamanhoTrecho + 1);

                        if (fim - inicio < 2)
                        {
                            continue;
                        }

                        using var caneta = new Pen(Color.FromArgb(35 + t * 45, cor), 1.5f);
                        g.DrawLines(caneta, pontos[inicio..fim]);
                    }
                }
            }

            // Desenha todos os corpos e devolve o que está sob o cursor (se houver).
            private EstadoCorpo? DesenharCorpos(
                Graphics g, IReadOnlyList<EstadoCorpo> corpos, double escala, int largura, int altura)
            {
                EstadoCorpo? corpoSobMouse = null;
                float menorDistancia = float.MaxValue;
                bool nomesVisiveis = mostrarNomes && corpos.Count <= LimiteCorposDetalhes;

                using var pincelNome = new SolidBrush(Color.FromArgb(170, CorTexto));

                // Corpos maiores primeiro, para os pequenos ficarem por cima.
                foreach (EstadoCorpo corpo in corpos.OrderByDescending(c => c.Raio))
                {
                    PointF centro = ParaTela(corpo.PosX, corpo.PosY, escala, largura, altura);
                    float raio = Math.Max((float)(corpo.Raio * escala), 3f);

                    DesenharCorpo(g, corpo, centro, raio);

                    if (nomesVisiveis)
                    {
                        g.DrawString(corpo.Nome, fonteNome, pincelNome, centro.X + raio + 4, centro.Y - 7);
                    }

                    // Procura o corpo mais próximo do cursor.
                    if (mouseSobreCena && !inicioArraste.HasValue)
                    {
                        float dx = posicaoMouse.X - centro.X;
                        float dy = posicaoMouse.Y - centro.Y;
                        float distancia = MathF.Sqrt(dx * dx + dy * dy);

                        if (distancia <= Math.Max(raio, 10) && distancia < menorDistancia)
                        {
                            menorDistancia = distancia;
                            corpoSobMouse = corpo;
                        }
                    }
                }

                return corpoSobMouse;
            }

            private static void DesenharCorpo(Graphics g, EstadoCorpo corpo, PointF centro, float raio)
            {
                Color cor = CorPorDensidade(corpo.Densidade);

                // Brilho externo (halo): círculo maior e quase transparente.
                float raioHalo = raio * 2.4f;
                using (var pincelHalo = new SolidBrush(Color.FromArgb(28, cor)))
                {
                    g.FillEllipse(pincelHalo, centro.X - raioHalo, centro.Y - raioHalo, raioHalo * 2, raioHalo * 2);
                }

                var retangulo = new RectangleF(centro.X - raio, centro.Y - raio, raio * 2, raio * 2);

                if (raio >= 4)
                {
                    // Gradiente radial: centro claro, borda na cor do corpo,
                    // dando aparência de esfera iluminada.
                    using var caminho = new GraphicsPath();
                    caminho.AddEllipse(retangulo);

                    using var gradiente = new PathGradientBrush(caminho)
                    {
                        CenterColor = Misturar(cor, Color.White, 0.65),
                        SurroundColors = new[] { Misturar(cor, Color.Black, 0.25) },
                        // Ponto de luz levemente deslocado para cima/esquerda.
                        CenterPoint = new PointF(centro.X - raio * 0.3f, centro.Y - raio * 0.3f)
                    };

                    g.FillEllipse(gradiente, retangulo);
                }
                else
                {
                    using var pincel = new SolidBrush(Misturar(cor, Color.White, 0.3));
                    g.FillEllipse(pincel, retangulo);
                }
            }

            // Painel no canto inferior esquerdo da cena.
            private void DesenharPainelInformacoes(Graphics g, int quantidadeCorpos, int baseCena)
            {
                var area = new RectangleF(16, baseCena - 118, 300, 102);
                DesenharCartao(g, area);

                float x = area.X + 16;
                float y = area.Y + 12;

                using var pincelTitulo = new SolidBrush(CorTexto);
                using var pincelSuave = new SolidBrush(CorTextoSuave);

                g.DrawString(Encurtar(fonte.Titulo, 34), fonteTitulo, pincelTitulo, x, y);
                y += 30;

                int quadrosCalculados = fonte.QuadrosDisponiveis - 1;
                int quadrosTotais = fonte.TotalQuadrosPrevistos - 1;

                DesenharLinhaInfo(g, "Corpos", quantidadeCorpos.ToString(), x, ref y, pincelSuave, pincelTitulo);
                DesenharLinhaInfo(g, "Colisões até aqui", marcadoresColisao.Count(m => m <= indice).ToString(), x, ref y, pincelSuave, pincelTitulo);

                if (quadrosCalculados < quadrosTotais)
                {
                    DesenharLinhaInfo(g, "Calculado", $"{quadrosCalculados} de {quadrosTotais} iterações", x, ref y, pincelSuave, pincelTitulo);
                }
            }

            private void DesenharLinhaInfo(
                Graphics g, string rotulo, string valor, float x, ref float y,
                Brush pincelRotulo, Brush pincelValor)
            {
                g.DrawString(rotulo, fonteHud, pincelRotulo, x, y);
                g.DrawString(valor, fonteHudNegrito, pincelValor, x + 130, y - 2);
                y += 20;
            }

            // Selo de estado no canto superior direito.
            private void DesenharSelo(Graphics g, int largura)
            {
                string texto;
                Color cor;

                if (reproduzindo && direcao < 0)
                {
                    texto = "◀◀  REBOBINANDO";
                    cor = CorRebobinar;
                }
                else if (NoFim())
                {
                    texto = "●  CONCLUÍDO";
                    cor = CorSucesso;
                }
                else if (!reproduzindo)
                {
                    texto = "‖  PAUSADO";
                    cor = CorAviso;
                }
                else if (fonte is FonteReplay)
                {
                    texto = "▶  REPLAY DO BANCO";
                    cor = Color.FromArgb(56, 189, 248);
                }
                else if (indice < fonte.QuadrosDisponiveis - 1)
                {
                    texto = "▶  REVENDO HISTÓRICO";
                    cor = Color.FromArgb(56, 189, 248);
                }
                else
                {
                    texto = "●  SIMULANDO";
                    cor = CorDestaque;
                }

                SizeF tamanho = g.MeasureString(texto, fonteBadge);
                var area = new RectangleF(largura - tamanho.Width - 40, AlturaBarra + 16, tamanho.Width + 24, 26);

                using (var fundo = new SolidBrush(Color.FromArgb(40, cor)))
                {
                    PreencherArredondado(g, fundo, area, 13);
                }

                using (var borda = new Pen(Color.FromArgb(150, cor)))
                {
                    ContornarArredondado(g, borda, area, 13);
                }

                using var pincel = new SolidBrush(cor);
                g.DrawString(texto, fonteBadge, pincel, area.X + 12, area.Y + 5);
            }

            // Legenda de cores (densidade) e régua de escala, no canto inferior direito.
            private void DesenharLegenda(Graphics g, double escala, double passo, int largura, int baseCena)
            {
                var area = new RectangleF(largura - 276, baseCena - 112, 260, 96);
                DesenharCartao(g, area);

                float x = area.X + 16;
                float y = area.Y + 12;

                using var pincelTexto = new SolidBrush(CorTexto);
                using var pincelSuave = new SolidBrush(CorTextoSuave);

                g.DrawString("Densidade", fonteHudNegrito, pincelTexto, x, y);
                y += 24;

                // Barra com o mesmo degradê usado para colorir os corpos.
                var barra = new RectangleF(x, y, area.Width - 32, 8);
                using (var gradiente = new LinearGradientBrush(barra, Color.Black, Color.Black, LinearGradientMode.Horizontal))
                {
                    gradiente.InterpolationColors = new ColorBlend
                    {
                        Positions = new[] { 0f, 0.35f, 0.6f, 0.8f, 1f },
                        Colors = new[]
                        {
                            CorPorFracao(0), CorPorFracao(0.35), CorPorFracao(0.6),
                            CorPorFracao(0.8), CorPorFracao(1)
                        }
                    };
                    PreencherArredondado(g, gradiente, barra, 4);
                }

                y += 12;
                g.DrawString("gás", fonteHud, pincelSuave, x, y);
                SizeF tamanhoDenso = g.MeasureString("metal denso", fonteHud);
                g.DrawString("metal denso", fonteHud, pincelSuave, barra.Right - tamanhoDenso.Width, y);
                y += 22;

                // Régua: um segmento de comprimento "redondo" em metros.
                float comprimento = (float)(passo * escala);
                using (var caneta = new Pen(CorDestaque, 2f))
                {
                    g.DrawLine(caneta, x, y + 6, x + comprimento, y + 6);
                    g.DrawLine(caneta, x, y + 2, x, y + 10);
                    g.DrawLine(caneta, x + comprimento, y + 2, x + comprimento, y + 10);
                }

                g.DrawString(FormatarDistancia(passo), fonteHud, pincelTexto, x + comprimento + 8, y - 2);
            }

            private void DesenharAjuda(Graphics g)
            {
                const string texto =
                    "Espaço reproduzir/pausar  ·  ← → passo  ·  B rebobinar  ·  Home/End início/fim  ·  " +
                    "roda zoom  ·  arrastar mover  ·  T rastros  ·  N nomes";

                using var pincel = new SolidBrush(Color.FromArgb(110, CorTextoSuave));
                g.DrawString(texto, fonteHud, pincel, 16, AlturaBarra + 20);
            }

            // ============================================================
            // LINHA DO TEMPO
            // ============================================================

            // Trilho da linha do tempo, entre o rótulo da iteração (esquerda)
            // e o tempo simulado (direita).
            private RectangleF RetanguloTrilha()
            {
                float y = ClientSize.Height - AlturaLinhaTempo / 2f - 3;
                return new RectangleF(170, y, Math.Max(100, ClientSize.Width - 170 - 190), 6);
            }

            // Área que aceita clique: o trilho com uma margem vertical
            // generosa, para ser fácil de acertar.
            private Rectangle AreaClicavelLinhaTempo()
            {
                RectangleF trilha = RetanguloTrilha();
                return Rectangle.Round(RectangleF.Inflate(trilha, 10, 16));
            }

            private float XNaLinha(int indiceQuadro)
            {
                RectangleF trilha = RetanguloTrilha();
                int total = Math.Max(1, fonte.TotalQuadrosPrevistos - 1);
                return trilha.Left + trilha.Width * indiceQuadro / total;
            }

            private void DesenharLinhaTempo(Graphics g)
            {
                int topo = ClientSize.Height - AlturaLinhaTempo;

                using (var fundo = new SolidBrush(CorPainel))
                {
                    g.FillRectangle(fundo, 0, topo, ClientSize.Width, AlturaLinhaTempo);
                }

                using (var linhaSuperior = new Pen(Color.FromArgb(45, 55, 80)))
                {
                    g.DrawLine(linhaSuperior, 0, topo, ClientSize.Width, topo);
                }

                QuadroSimulacao quadroAtual = fonte.ObterQuadro(indice);

                using var pincelTexto = new SolidBrush(CorTexto);
                using var pincelSuave = new SolidBrush(CorTextoSuave);
                using var pincelDestaque = new SolidBrush(CorDestaque);

                // Esquerda: iteração atual.
                int iteracaoFinal = fonte.ObterQuadro(0).Iteracao + fonte.TotalQuadrosPrevistos - 1;
                g.DrawString("ITERAÇÃO", fonteBadge, pincelSuave, 18, topo + 12);
                g.DrawString($"{quadroAtual.Iteracao} / {iteracaoFinal}", fonteIteracao, pincelTexto, 16, topo + 26);

                // Direita: tempo simulado até este quadro.
                string tempo = FormatarTempo(quadroAtual.Iteracao * fonte.DeltaTempo);
                g.DrawString("TEMPO SIMULADO", fonteBadge, pincelSuave, ClientSize.Width - 172, topo + 12);
                g.DrawString(tempo, fonteIteracao, pincelDestaque, ClientSize.Width - 174, topo + 26);

                RectangleF trilha = RetanguloTrilha();

                // 1) Trilho completo (tudo o que a simulação terá).
                using (var pincelTrilho = new SolidBrush(Color.FromArgb(32, 42, 70)))
                {
                    PreencherArredondado(g, pincelTrilho, trilha, 3);
                }

                // 2) Parte já calculada (na simulação ao vivo pode ser menor
                //    que o total; no replay é sempre tudo).
                float xDisponivel = XNaLinha(fonte.QuadrosDisponiveis - 1);
                if (xDisponivel > trilha.Left)
                {
                    using var pincelDisponivel = new SolidBrush(Color.FromArgb(58, 72, 112));
                    PreencherArredondado(g, pincelDisponivel,
                        new RectangleF(trilha.Left, trilha.Top, xDisponivel - trilha.Left, trilha.Height), 3);
                }

                // 3) Parte já reproduzida, até o quadro atual.
                float xAtual = XNaLinha(indice);
                if (xAtual > trilha.Left + 1)
                {
                    using var gradiente = new LinearGradientBrush(
                        trilha, Color.FromArgb(56, 189, 248), CorDestaque, LinearGradientMode.Horizontal);
                    PreencherArredondado(g, gradiente,
                        new RectangleF(trilha.Left, trilha.Top, xAtual - trilha.Left, trilha.Height), 3);
                }

                // 4) Marcadores de colisão (fusões).
                using (var pincelColisao = new SolidBrush(CorColisao))
                {
                    foreach (int marcador in marcadoresColisao)
                    {
                        float xm = XNaLinha(marcador);
                        g.FillPolygon(pincelColisao, new[]
                        {
                            new PointF(xm - 4, trilha.Top - 10),
                            new PointF(xm + 4, trilha.Top - 10),
                            new PointF(xm, trilha.Top - 4)
                        });
                    }
                }

                // 5) Botão deslizante (posição atual). Cresce ao passar o mouse.
                float raio = arrastandoLinhaTempo || mouseSobreLinhaTempo ? 9f : 7f;
                float yCentro = trilha.Top + trilha.Height / 2;

                using (var halo = new SolidBrush(Color.FromArgb(50, CorDestaque)))
                {
                    g.FillEllipse(halo, xAtual - raio - 4, yCentro - raio - 4, (raio + 4) * 2, (raio + 4) * 2);
                }

                using (var pincelBotao = new SolidBrush(Color.White))
                {
                    g.FillEllipse(pincelBotao, xAtual - raio, yCentro - raio, raio * 2, raio * 2);
                }

                using (var bordaBotao = new Pen(CorDestaque, 2f))
                {
                    g.DrawEllipse(bordaBotao, xAtual - raio, yCentro - raio, raio * 2, raio * 2);
                }

                // Dica com a iteração sob o mouse, ao passar sobre a linha.
                if (mouseSobreLinhaTempo && !arrastandoLinhaTempo)
                {
                    double fracao = Math.Clamp((posicaoMouse.X - trilha.Left) / trilha.Width, 0, 1);
                    int alvo = (int)Math.Round(fracao * (fonte.TotalQuadrosPrevistos - 1));
                    string dica = alvo < fonte.QuadrosDisponiveis
                        ? $"Ir para iteração {fonte.ObterQuadro(alvo).Iteracao}"
                        : "Ainda não calculado";

                    SizeF tamanho = g.MeasureString(dica, fonteHud);
                    var area = new RectangleF(posicaoMouse.X - tamanho.Width / 2 - 8, trilha.Top - 40, tamanho.Width + 16, 24);
                    DesenharCartao(g, area);
                    g.DrawString(dica, fonteHud, pincelTexto, area.X + 8, area.Y + 4);
                }
            }

            // Cartão com os dados do corpo sob o cursor.
            private void DesenharDetalhesCorpo(Graphics g, EstadoCorpo corpo, PointF centro, float raio, int largura)
            {
                // Anel de seleção.
                using (var anel = new Pen(Color.FromArgb(220, CorDestaque), 1.5f) { DashStyle = DashStyle.Dash })
                {
                    float r = raio + 6;
                    g.DrawEllipse(anel, centro.X - r, centro.Y - r, r * 2, r * 2);
                }

                double velocidade = Math.Sqrt(corpo.VelX * corpo.VelX + corpo.VelY * corpo.VelY);

                string[] linhas =
                {
                    $"Massa:       {corpo.Massa:E3} kg",
                    $"Densidade:  {corpo.Densidade:N1} kg/m³",
                    $"Raio:         {FormatarDistancia(corpo.Raio)}",
                    $"Velocidade: {velocidade:E3} m/s",
                    $"Posição:     ({FormatarDistancia(corpo.PosX)}, {FormatarDistancia(corpo.PosY)})"
                };

                float larguraCartao = 280;
                float alturaCartao = 34 + linhas.Length * 19;

                // Posiciona à direita do corpo, ou à esquerda se não couber.
                float x = centro.X + raio + 16;
                if (x + larguraCartao > largura - 10)
                {
                    x = centro.X - raio - 16 - larguraCartao;
                }

                float y = Math.Max(AlturaBarra + 10, centro.Y - alturaCartao / 2);

                var area = new RectangleF(x, y, larguraCartao, alturaCartao);
                DesenharCartao(g, area, Color.FromArgb(120, CorPorDensidade(corpo.Densidade)));

                using var pincelTitulo = new SolidBrush(CorPorDensidade(corpo.Densidade));
                using var pincelTexto = new SolidBrush(CorTexto);

                g.DrawString(Encurtar(corpo.Nome, 32), fonteHudNegrito, pincelTitulo, x + 12, y + 8);

                float linhaY = y + 32;
                foreach (string linha in linhas)
                {
                    g.DrawString(linha, fonteHud, pincelTexto, x + 12, linhaY);
                    linhaY += 19;
                }
            }

            // Cartão semitransparente com cantos arredondados.
            private static void DesenharCartao(Graphics g, RectangleF area, Color? corBorda = null)
            {
                using (var fundo = new SolidBrush(Color.FromArgb(200, 13, 19, 36)))
                {
                    PreencherArredondado(g, fundo, area, 10);
                }

                using var borda = new Pen(corBorda ?? Color.FromArgb(60, 94, 234, 212));
                ContornarArredondado(g, borda, area, 10);
            }

            // ============================================================
            // UTILITÁRIOS DE DESENHO
            // ============================================================

            private static GraphicsPath CaminhoArredondado(RectangleF r, float raio)
            {
                float d = Math.Min(raio * 2, Math.Min(r.Width, r.Height));
                var caminho = new GraphicsPath();

                if (d <= 0)
                {
                    caminho.AddRectangle(r);
                    return caminho;
                }

                caminho.AddArc(r.X, r.Y, d, d, 180, 90);
                caminho.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                caminho.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                caminho.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                caminho.CloseFigure();
                return caminho;
            }

            private static void PreencherArredondado(Graphics g, Brush pincel, RectangleF r, float raio)
            {
                using var caminho = CaminhoArredondado(r, raio);
                g.FillPath(pincel, caminho);
            }

            private static void ContornarArredondado(Graphics g, Pen caneta, RectangleF r, float raio)
            {
                using var caminho = CaminhoArredondado(r, raio);
                g.DrawPath(caneta, caminho);
            }

            // Converte a densidade em uma cor. Usa escala logarítmica
            // porque a faixa vai de 0,09 a 22.590 kg/m³ (5 ordens de
            // grandeza): numa escala linear quase tudo teria a mesma cor.
            private static Color CorPorDensidade(double densidade)
            {
                double minimo = Math.Log10(DensidadeMinima);
                double maximo = Math.Log10(DensidadeMaxima);
                double fracao = (Math.Log10(Math.Max(densidade, DensidadeMinima)) - minimo) / (maximo - minimo);

                return CorPorFracao(Math.Clamp(fracao, 0, 1));
            }

            // Degradê: azul (gás) → turquesa → âmbar → laranja → vermelho (metal denso).
            private static Color CorPorFracao(double t)
            {
                (double Posicao, Color Cor)[] paradas =
                {
                    (0.00, Color.FromArgb(96, 165, 250)),
                    (0.35, Color.FromArgb(94, 234, 212)),
                    (0.60, Color.FromArgb(250, 204, 21)),
                    (0.80, Color.FromArgb(251, 146, 60)),
                    (1.00, Color.FromArgb(244, 63, 94))
                };

                for (int i = 1; i < paradas.Length; i++)
                {
                    if (t <= paradas[i].Posicao)
                    {
                        double local = (t - paradas[i - 1].Posicao) / (paradas[i].Posicao - paradas[i - 1].Posicao);
                        return Misturar(paradas[i - 1].Cor, paradas[i].Cor, local);
                    }
                }

                return paradas[^1].Cor;
            }

            // Mistura linear entre duas cores (0 = a, 1 = b).
            private static Color Misturar(Color a, Color b, double t)
            {
                t = Math.Clamp(t, 0, 1);
                return Color.FromArgb(
                    (int)(a.R + (b.R - a.R) * t),
                    (int)(a.G + (b.G - a.G) * t),
                    (int)(a.B + (b.B - a.B) * t));
            }

            // Arredonda para 1, 2 ou 5 × 10^n (valores fáceis de ler numa régua).
            private static double NumeroRedondo(double valor)
            {
                if (valor <= 0 || double.IsNaN(valor) || double.IsInfinity(valor))
                {
                    return 1;
                }

                double potencia = Math.Pow(10, Math.Floor(Math.Log10(valor)));
                double fracao = valor / potencia;

                double redondo = fracao < 1.5 ? 1 : fracao < 3.5 ? 2 : fracao < 7.5 ? 5 : 10;
                return redondo * potencia;
            }

            private static string FormatarDistancia(double metros)
            {
                double absoluto = Math.Abs(metros);

                if (absoluto >= 1e6)
                {
                    return $"{metros / 1000:N0} km";
                }

                if (absoluto >= 1000)
                {
                    return $"{metros / 1000:0.##} km";
                }

                return $"{metros:0.##} m";
            }

            private static string FormatarTempo(double segundos)
            {
                var tempo = TimeSpan.FromSeconds(segundos);

                if (tempo.TotalDays >= 1)
                {
                    return $"{(int)tempo.TotalDays} d {tempo.Hours} h {tempo.Minutes} min";
                }

                if (tempo.TotalHours >= 1)
                {
                    return $"{tempo.Hours} h {tempo.Minutes} min";
                }

                if (tempo.TotalMinutes >= 1)
                {
                    return $"{tempo.Minutes} min {tempo.Seconds} s";
                }

                return $"{segundos:0.##} s";
            }

            private static string Encurtar(string texto, int maximo)
            {
                return texto.Length <= maximo ? texto : texto[..(maximo - 1)] + "…";
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    timer.Dispose();
                    fonteHud.Dispose();
                    fonteHudNegrito.Dispose();
                    fonteTitulo.Dispose();
                    fonteBadge.Dispose();
                    fonteNome.Dispose();
                    fonteIteracao.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
