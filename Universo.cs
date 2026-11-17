using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HologramaGravitacional
{
    // Define como o Universo processa cada iteração.
    // O modo Sequencial é o algoritmo original da N1 e foi mantido
    // para servir de referência no comparativo de performance.
    public enum ModoProcessamento
    {
        Sequencial,
        Paralelo
    }

    public class Universo
    {
        // Representa o conjunto de corpos e operações que definem
        // o estado do 'Universo' simulado. Esta classe mantém a
        // lista de corpos e fornece métodos para calcular acelerações,
        // executar iterações de tempo e tratar colisões.
        private readonly List<Corpo> corpos;
        public IReadOnlyList<Corpo> Corpos => corpos;
        public int QuantidadeFusoes { get; private set; }
        public int QuantidadeCorpos
        {
            get { return corpos.Count; }
        }

        public int QuantidadeIteracoes { get; set; }

        public double TempoEntreIteracoes { get; set; }

        public bool OcorreuFusao { get; private set; }

        // Por padrão a N2 usa processamento paralelo.
        public ModoProcessamento Modo { get; set; } = ModoProcessamento.Paralelo;

        // Pares que se encostaram na iteração anterior. A fusão acontece
        // só no início da iteração seguinte, para que exista um quadro
        // mostrando os dois corpos exatamente encostados.
        private readonly List<(Corpo A, Corpo B)> fusoesPendentes = new List<(Corpo A, Corpo B)>();
        private readonly HashSet<Corpo> corposComFusaoPendente = new HashSet<Corpo>();

        // Permite desligar a mensagem de colisão no console.
        // Usado no comparativo de performance, onde escrever no
        // console distorceria a medição de tempo.
        public bool ExibirColisoes { get; set; } = true;

        public Universo()
        {
            corpos = new List<Corpo>();
        }
        public void AdicionarCorpo(Corpo corpo)
        {
            corpos.Add(corpo);
        }

        // Cria uma "fotografia" imutável do estado atual de todos os corpos.
        // É essa cópia que vai para o banco de dados: como ela não muda mais,
        // a thread de gravação pode lê-la enquanto a simulação já calcula
        // a próxima iteração, sem risco de ler valores pela metade.
        public List<EstadoCorpo> ObterEstados()
        {
            return corpos.Select(EstadoCorpo.De).ToList();
        }

        public (double X, double Y) CalcularForcaResultante(Corpo corpoAlvo)
        {
            // Soma vetorial das forças gravitacionais que todos os
            // outros corpos exercem sobre o corpo alvo.
            // Este método só LÊ posições e massas, nunca altera nada,
            // por isso pode ser chamado por várias threads ao mesmo tempo.
            double forcaTotalX = 0;
            double forcaTotalY = 0;

            foreach (Corpo outroCorpo in corpos)
            {
                if (outroCorpo == corpoAlvo)
                {
                    continue;
                }

                var forca = CalculosFisicos.CalcularForcaComponentes(
                    corpoAlvo,
                    outroCorpo
                );

                forcaTotalX += forca.X;
                forcaTotalY += forca.Y;
            }

            return (forcaTotalX, forcaTotalY);
        }
        public (double X, double Y) CalcularAceleracaoResultante(Corpo corpoAlvo)
        {
            // Segunda lei de Newton: a = F / m.
            var forca = CalcularForcaResultante(corpoAlvo);

            return CalculosFisicos.CalcularAceleracao(
                corpoAlvo,
                forca.X,
                forca.Y
            );
        }
        public void ExecutarIteracao(double deltaTempo)
        {
            // Indica se houve fusão nesta iteração específica.
            // O total acumulado fica em QuantidadeFusoes.
            OcorreuFusao = false;

            // Funde os pares que se encostaram na iteração anterior.
            ExecutarFusoesPendentes();

            if (Modo == ModoProcessamento.Paralelo)
            {
                AtualizarCorposParalelo(deltaTempo);
            }
            else
            {
                AtualizarCorposSequencial(deltaTempo);
            }

            // Verifica colisões que aconteceram DURANTE o passo de tempo.
            TratarColisoes(deltaTempo);
        }

        // Versão original (N1): um corpo de cada vez, em uma única thread.
        private void AtualizarCorposSequencial(double deltaTempo)
        {
            // Calcula a aceleração de cada corpo considerando
            // a atração gravitacional dos demais corpos.
            foreach (Corpo corpo in corpos)
            {
                var aceleracao = CalcularAceleracaoResultante(corpo);

                corpo.AtualizarVelocidade(
                    aceleracao.X,
                    aceleracao.Y,
                    deltaTempo
                );
            }

            // Atualiza a posição de todos os corpos depois
            // que as velocidades foram calculadas.
            foreach (Corpo corpo in corpos)
            {
                corpo.AtualizarPosicao(deltaTempo);
            }
        }

        // Versão paralela (N2).
        //
        // O cálculo da força sobre cada corpo é O(n) e o total por
        // iteração é O(n²) — é aqui que está quase todo o custo da
        // simulação. Como a força sobre o corpo i depende apenas das
        // POSIÇÕES e MASSAS dos outros corpos (que não mudam nesta
        // etapa), cada corpo pode ser calculado de forma independente,
        // em núcleos diferentes do processador.
        private void AtualizarCorposParalelo(double deltaTempo)
        {
            // ETAPA 1: aceleração e velocidade.
            // Parallel.For divide os índices 0..n-1 entre as threads
            // do pool. Cada thread escreve SOMENTE na velocidade do
            // próprio corpo i e lê somente posições/massas, então
            // não existe condição de corrida e não é preciso usar lock.
            Parallel.For(0, corpos.Count, i =>
            {
                Corpo corpo = corpos[i];

                var aceleracao = CalcularAceleracaoResultante(corpo);

                corpo.AtualizarVelocidade(
                    aceleracao.X,
                    aceleracao.Y,
                    deltaTempo
                );
            });

            // Parallel.For só retorna depois que TODAS as threads terminaram.
            // Isso funciona como uma barreira: nenhuma posição é alterada
            // antes de todas as acelerações terem sido calculadas com as
            // posições antigas (se misturássemos as etapas, alguns corpos
            // seriam atraídos por posições já "do futuro").

            // ETAPA 2: posição. Cada corpo altera apenas a si mesmo.
            Parallel.For(0, corpos.Count, i =>
            {
                corpos[i].AtualizarPosicao(deltaTempo);
            });
        }

        public bool ExisteColisao(Corpo corpoA, Corpo corpoB)
        {
            double distancia =
                CalculosFisicos.CalcularDistancia(corpoA, corpoB);

            double distanciaColisao =
                corpoA.Raio + corpoB.Raio;

            return distancia <= distanciaColisao;
        }
        // ================================================================
        // DETECÇÃO CONTÍNUA DE COLISÃO
        // ================================================================
        //
        // Verificar apenas se os corpos estão sobrepostos NO FIM do passo
        // tem dois problemas:
        //   1. no passo em que se tocam, eles já estão um pouco "um dentro
        //      do outro" — nunca aparecem exatamente encostados;
        //   2. com passos grandes, dois corpos rápidos podem atravessar um
        //      ao outro entre duas verificações e a colisão é perdida
        //      (efeito conhecido como "tunelamento").
        //
        // Solução: durante o passo cada corpo se move em linha reta
        // (posição += velocidade × Δt). Então dá para calcular o INSTANTE
        // exato em que a distância entre os centros fica igual à soma dos
        // raios e "recuar" os dois corpos até esse instante.
        //
        // Recuando um tempo s a partir do fim do passo:
        //   d(s) = d − v·s     (d = posição relativa, v = velocidade relativa)
        //   |d(s)|² = R²  →  (v·v)s² − 2(d·v)s + (d·d − R²) = 0
        // As raízes s1 ≤ s2 delimitam o intervalo em que os corpos estariam
        // sobrepostos. s2 é o primeiro contato (o mais antigo); há colisão
        // se esse intervalo cruza o passo [0, Δt].
        public static bool DetectarColisaoNoPasso(
            Corpo corpoA, Corpo corpoB, double deltaTempo, out double recuo)
        {
            recuo = 0;

            double dx = corpoB.PosX - corpoA.PosX;
            double dy = corpoB.PosY - corpoA.PosY;
            double vx = corpoB.VelX - corpoA.VelX;
            double vy = corpoB.VelY - corpoA.VelY;
            double somaRaios = corpoA.Raio + corpoB.Raio;

            double c = (dx * dx) + (dy * dy) - (somaRaios * somaRaios);
            double vv = (vx * vx) + (vy * vy);

            // Sem movimento relativo: só há colisão se já estão sobrepostos.
            if (vv == 0 || deltaTempo <= 0)
            {
                return c <= 0;
            }

            double dv = (dx * vx) + (dy * vy);
            double discriminante = (dv * dv) - (vv * c);

            // Discriminante negativo: a distância nunca chega à soma dos raios.
            if (discriminante < 0)
            {
                return false;
            }

            double raiz = Math.Sqrt(discriminante);
            double s1 = (dv - raiz) / vv;
            double s2 = (dv + raiz) / vv;

            // O intervalo de sobreposição [s1, s2] não cruza este passo.
            if (s2 < 0 || s1 > deltaTempo)
            {
                return false;
            }

            recuo = Math.Clamp(s2, 0, deltaTempo);
            return true;
        }

        public (Corpo A, Corpo B, double Recuo)? EncontrarColisao(double deltaTempo)
        {
            if (Modo == ModoProcessamento.Paralelo)
            {
                return EncontrarColisaoParalelo(deltaTempo);
            }

            for (int i = 0; i < corpos.Count; i++)
            {
                if (corposComFusaoPendente.Contains(corpos[i]))
                {
                    continue;
                }

                for (int j = i + 1; j < corpos.Count; j++)
                {
                    if (corposComFusaoPendente.Contains(corpos[j]))
                    {
                        continue;
                    }

                    if (DetectarColisaoNoPasso(corpos[i], corpos[j], deltaTempo, out double recuo))
                    {
                        return (corpos[i], corpos[j], recuo);
                    }
                }
            }

            return null;
        }

        // A verificação de colisões também é O(n²) e é feita a cada
        // iteração, então também vale a pena paralelizar.
        //
        // Cuidado importante: queremos encontrar EXATAMENTE o mesmo par
        // que a versão sequencial encontraria (o de menor i e, para esse
        // i, o menor j). Assim o resultado da simulação paralela é
        // idêntico ao da sequencial, o que permite validar a N2
        // comparando as duas no relatório.
        private (Corpo A, Corpo B, double Recuo)? EncontrarColisaoParalelo(double deltaTempo)
        {
            int quantidade = corpos.Count;
            double[] recuoDoPar = new double[quantidade];

            // Cada posição i guarda o primeiro j que colide com i.
            // Cada thread escreve apenas no seu próprio índice,
            // então o vetor pode ser compartilhado sem lock.
            // Inicializado com -1 = "nenhuma colisão encontrada".
            int[] primeiroParDe = new int[quantidade];
            Array.Fill(primeiroParDe, -1);

            Parallel.For(0, quantidade, (i, estado) =>
            {
                // Corpos que já vão se fundir não participam de outra colisão.
                // (O HashSet só é LIDO aqui, o que é seguro entre threads.)
                if (corposComFusaoPendente.Contains(corpos[i]))
                {
                    return;
                }

                for (int j = i + 1; j < quantidade; j++)
                {
                    if (corposComFusaoPendente.Contains(corpos[j]))
                    {
                        continue;
                    }

                    if (DetectarColisaoNoPasso(corpos[i], corpos[j], deltaTempo, out double recuo))
                    {
                        primeiroParDe[i] = j;
                        recuoDoPar[i] = recuo;

                        // Break(): garante que todos os índices MENORES que i
                        // ainda serão processados, mas os maiores podem ser
                        // descartados. Como só nos interessa o menor i,
                        // isso economiza trabalho sem perder o resultado.
                        estado.Break();
                        return;
                    }
                }
            });

            // O menor i com colisão é exatamente o par que a
            // versão sequencial encontraria primeiro.
            for (int i = 0; i < quantidade; i++)
            {
                int j = primeiroParDe[i];

                if (j > i)
                {
                    return (corpos[i], corpos[j], recuoDoPar[i]);
                }
            }

            return null;
        }

        public void FundirCorpos(Corpo corpoA, Corpo corpoB)
        {
            // Cria o novo corpo resultante da fusão.
            Corpo corpoResultante =
            CalculosFisicos.CriarCorpoResultante(
            corpoA,
            corpoB
            );

            // Remove os dois corpos que colidiram.
            corpos.Remove(corpoA);
            corpos.Remove(corpoB);

            // Adiciona o corpo resultante ao Universo.
            corpos.Add(corpoResultante);

            // Registra que ocorreu uma colisão com fusão nesta iteração.
            // Essa informação é utilizada pelo Simulador
            // para interpretar corretamente a variação da energia.
            OcorreuFusao = true;
            QuantidadeFusoes++;

        }
        public void TratarColisoes(double deltaTempo)
        {
            // A busca por colisões (a parte cara, O(n²)) pode ser paralela.
            // O tratamento de cada colisão é sequencial, porque altera o
            // estado compartilhado (posições e lista de fusões pendentes).
            while (true)
            {
                var colisao = EncontrarColisao(deltaTempo);

                // Se não houver colisão, não há mais nada para tratar.
                if (!colisao.HasValue)
                {
                    break;
                }

                var (corpoA, corpoB, recuo) = colisao.Value;

                // Leva os dois corpos de volta ao instante exato do contato:
                // eles ficam encostados, sem sobreposição.
                corpoA.RecuarPosicao(recuo);
                corpoB.RecuarPosicao(recuo);

                if (ExibirColisoes)
                {
                    Console.WriteLine(
                        $"Contato: {corpoA.Nome} + {corpoB.Nome} " +
                        "(fusão na próxima iteração)"
                    );
                }

                fusoesPendentes.Add((corpoA, corpoB));
                corposComFusaoPendente.Add(corpoA);
                corposComFusaoPendente.Add(corpoB);
            }
        }

        // Os dois corpos de cada par são substituídos por um único corpo,
        // resultante da conservação da massa e da quantidade de movimento.
        private void ExecutarFusoesPendentes()
        {
            foreach (var (corpoA, corpoB) in fusoesPendentes)
            {
                FundirCorpos(corpoA, corpoB);
            }

            fusoesPendentes.Clear();
            corposComFusaoPendente.Clear();
        }
    }
}
