using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HologramaGravitacional
{
    // Visualizador 2D simples usando Windows Forms.
    //
    // Para depuração e demonstração, este formulário executa a
    // simulação usando a mesma instância de Universo e desenha
    // os corpos na tela. Há controles para pausar, avançar um
    // passo, dar zoom e ajustar a velocidade de reprodução visual.
    // O visualizador é propositalmente simples e não altera a
    // lógica física; apenas chama Universo.ExecutarIteracao para
    // avançar o estado da simulação.
    public class Visualizador2D
    {
        private readonly Universo universo;

        public Visualizador2D(Universo universo)
        {
            this.universo = universo;
        }

        public void Run()
        {
            using (var form = new VisualizadorForm(universo))
            {
                Application.Run(form);
            }
        }

        private class VisualizadorForm : Form
        {
            private readonly Universo universo;
            private readonly System.Windows.Forms.Timer timer;
            private int iteracaoAtual = 0;
            private bool isPaused = false;
            private bool stepRequested = false;
            private double zoomFactor = 1.0;
            private int iterationsPerTick = 1;

            public VisualizadorForm(Universo universo)
            {
                this.universo = universo;

                Text = "Holograma Gravitacional - Visualizador 2D";
                DoubleBuffered = true;
                Width = 1000;
                Height = 800;

                // Configura controles de interface simples
                var topPanel = new Panel();
                topPanel.Dock = DockStyle.Top;
                topPanel.Height = 36;
                topPanel.Padding = new Padding(4);
                Controls.Add(topPanel);

                var pauseButton = new Button();
                pauseButton.Text = "Pause";
                pauseButton.AutoSize = true;
                pauseButton.Click += (s, e) => TogglePause(pauseButton);
                topPanel.Controls.Add(pauseButton);

                var stepButton = new Button();
                stepButton.Text = "Step";
                stepButton.AutoSize = true;
                stepButton.Left = pauseButton.Right + 6;
                stepButton.Click += (s, e) => RequestStep();
                topPanel.Controls.Add(stepButton);

                var zoomIn = new Button();
                zoomIn.Text = "+";
                zoomIn.AutoSize = true;
                zoomIn.Left = stepButton.Right + 6;
                zoomIn.Click += (s, e) => { zoomFactor *= 1.25; Invalidate(); };
                topPanel.Controls.Add(zoomIn);

                var zoomOut = new Button();
                zoomOut.Text = "-";
                zoomOut.AutoSize = true;
                zoomOut.Left = zoomIn.Right + 6;
                zoomOut.Click += (s, e) => { zoomFactor /= 1.25; Invalidate(); };
                topPanel.Controls.Add(zoomOut);

                var speedLabel = new Label();
                speedLabel.Text = "Speed:";
                speedLabel.AutoSize = true;
                speedLabel.Left = zoomOut.Right + 12;
                speedLabel.Top = 8;
                topPanel.Controls.Add(speedLabel);

                var speedTrack = new TrackBar();
                speedTrack.Minimum = 1;
                speedTrack.Maximum = 1000;
                speedTrack.Value = 100;
                speedTrack.TickStyle = TickStyle.None;
                speedTrack.Left = speedLabel.Right + 6;
                // Aumenta a largura visual do controle de velocidade para uso mais fácil
                speedTrack.Width = 320;
                speedTrack.Top = 2;
                speedTrack.SmallChange = 1;
                speedTrack.LargeChange = 10;
                topPanel.Controls.Add(speedTrack);

                speedTrack.Scroll += (s, e) =>
                {
                    // Mapeia o valor da barra [1..1000] para intervalo [5..1000] ms
                    // (inverso: valor maior -> maior velocidade visual)
                    int interval = Math.Max(5, 1000 - speedTrack.Value + 5);
                    timer.Interval = interval;
                };

                void TogglePause(Button btn)
                {
                    isPaused = !isPaused;
                    btn.Text = isPaused ? "Resume" : "Pause";
                }

                void RequestStep()
                {
                    if (!isPaused)
                    {
                        isPaused = true;
                        pauseButton.Text = "Resume";
                    }

                    stepRequested = true;
                }

                timer = new System.Windows.Forms.Timer();
                // Converte segundos para milissegundos. Mínimo 10 ms.
                int intervaloMs = Math.Max(10, (int)(universo.TempoEntreIteracoes * 1000));
                timer.Interval = intervaloMs;
                timer.Tick += Timer_Tick;
                Shown += (s, e) => timer.Start();
                FormClosed += (s, e) => timer.Stop();

                // Zoom via roda do mouse
                MouseWheel += (s, e) =>
                {
                    if (e.Delta > 0) zoomFactor *= 1.1;
                    else zoomFactor /= 1.1;
                    Invalidate();
                };
            }

            private void Timer_Tick(object? sender, EventArgs e)
            {
                if (iteracaoAtual >= universo.QuantidadeIteracoes)
                {
                    timer.Stop();
                    return;
                }

                if (isPaused && !stepRequested)
                {
                    return;
                }

                // Se foi solicitada uma única etapa (step), executa exatamente uma iteração.
                if (stepRequested)
                {
                    universo.ExecutarIteracao(universo.TempoEntreIteracoes);
                    iteracaoAtual++;
                    stepRequested = false;
                }
                else
                {
                    // Executa múltiplas iterações físicas por tick para acelerar
                    // a simulação mantendo a taxa de atualização da interface.
                    for (int i = 0; i < iterationsPerTick; i++)
                    {
                        if (iteracaoAtual >= universo.QuantidadeIteracoes)
                        {
                            break;
                        }

                        universo.ExecutarIteracao(universo.TempoEntreIteracoes);
                        iteracaoAtual++;
                    }
                }

                Text = $"Holograma Gravitacional - Iteração {iteracaoAtual}/{universo.QuantidadeIteracoes} | Corpos: {universo.QuantidadeCorpos}";

                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                var g = e.Graphics;
                g.Clear(Color.Black);

                var corpos = universo.Corpos.ToList();
                if (corpos.Count == 0)
                    return;

                // Calcula a caixa delimitadora (bounding box) dos corpos
                double minX = corpos.Min(c => c.PosX);
                double maxX = corpos.Max(c => c.PosX);
                double minY = corpos.Min(c => c.PosY);
                double maxY = corpos.Max(c => c.PosY);

                // Se todos os corpos estiverem na mesma posição, fornece uma
                // pequena caixa para evitar divisão por zero na escala
                if (Math.Abs(maxX - minX) < 1e-6) { maxX = minX + 1; }
                if (Math.Abs(maxY - minY) < 1e-6) { maxY = minY + 1; }

                // Adiciona margem ao retângulo para melhor visualização
                double marginFactor = 0.15;
                double dx = maxX - minX;
                double dy = maxY - minY;
                minX -= dx * marginFactor;
                maxX += dx * marginFactor;
                minY -= dy * marginFactor;
                maxY += dy * marginFactor;

                dx = maxX - minX;
                dy = maxY - minY;

                // Determina a escala necessária para ajustar à área de cliente
                double scaleX = ClientSize.Width / dx;
                double scaleY = (ClientSize.Height - 40) / dy; // considera o painel superior
                double baseScale = Math.Min(scaleX, scaleY);
                double scale = baseScale * zoomFactor;

                // Se a escala for muito pequena ou muito grande, limita para
                // um intervalo razoável para manter a visibilidade
                scale = Math.Max(1e-6, Math.Min(scale, 1e6));

                // Calcula deslocamentos (offsets) para centralizar o desenho
                double offsetX = -minX * scale + (ClientSize.Width - dx * scale) / 2.0;
                double offsetY = -minY * scale + ((ClientSize.Height - 40) - dy * scale) / 2.0 + 40;

                // Desenha cada corpo
                foreach (var corpo in corpos)
                {
                    float cx = (float)(corpo.PosX * scale + offsetX);
                    float cy = (float)(corpo.PosY * scale + offsetY);

                    // Converte o raio (metros) para pixels
                    float radiusPx = (float)(corpo.Raio * scale);

                    // Garante um tamanho mínimo visível em pixels
                    float minVisible = 3f;
                    if (radiusPx < minVisible) radiusPx = minVisible;

                    var rect = new RectangleF(
                        cx - radiusPx,
                        cy - radiusPx,
                        radiusPx * 2,
                        radiusPx * 2);

                    using (var brush = new SolidBrush(Color.Cyan))
                    using (var pen = new Pen(Color.White, 1f))
                    {
                        g.FillEllipse(brush, rect);
                        g.DrawEllipse(pen, rect);
                    }

                    // Exibição do nome removida (simplificação visual)
                }
            }
        }
    }
}
