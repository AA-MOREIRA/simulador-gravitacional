using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HologramaGravitacional
{
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

        public Universo()
        {
            corpos = new List<Corpo>();
        }
        public void AdicionarCorpo(Corpo corpo)
        {
            corpos.Add(corpo);
        }
        public (double X, double Y) CalcularAceleracaoResultante(Corpo corpoAlvo)
        {
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

            return CalculosFisicos.CalcularAceleracao(
                corpoAlvo,
                forcaTotalX,
                forcaTotalY
            );
        }
        public void ExecutarIteracao(double deltaTempo)
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

            // Verifica e trata possíveis colisões após
            // a atualização das posições.
            TratarColisoes();
        }
        public bool ExisteColisao(Corpo corpoA, Corpo corpoB)
        {
            double distancia =
                CalculosFisicos.CalcularDistancia(corpoA, corpoB);

            double distanciaColisao =
                corpoA.Raio + corpoB.Raio;

            return distancia <= distanciaColisao;
        }
        public (Corpo A, Corpo B)? EncontrarColisao()
        {
            for (int i = 0; i < corpos.Count; i++)
            {
                for (int j = i + 1; j < corpos.Count; j++)
                {
                    Corpo corpoA = corpos[i];
                    Corpo corpoB = corpos[j];

                    if (ExisteColisao(corpoA, corpoB))
                    {
                        return (corpoA, corpoB);
                    }
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

            // Registra que ocorreu uma colisão com fusão.
            // Essa informação será utilizada pelo Simulador
            // para interpretar corretamente a variação da energia.
            OcorreuFusao = true;
            QuantidadeFusoes++;

        }
        public void TratarColisoes()
        {
            while (true)
            {
                // Procura dois corpos que estejam em situação de colisão.
                var colisao = EncontrarColisao();

                // Se não houver colisão, não há mais nada para tratar.
                if (!colisao.HasValue)
                {
                    break;
                }

                Corpo corpoA = colisao.Value.A;
                Corpo corpoB = colisao.Value.B;

                Console.WriteLine(
                    $"Colisão detectada: {corpoA.Nome} + {corpoB.Nome}"
                );

                // Os dois corpos são substituídos por um único
                // corpo resultante da conservação da massa e
                // da quantidade de movimento.
                FundirCorpos(corpoA, corpoB);
            }
        }        
            public (double X, double Y) CalcularForcaResultante(Corpo corpoAlvo)
        {
            double forcaTotalX = 0;
            double forcaTotalY = 0;

            foreach (Corpo outroCorpo in corpos)
            {
                if (outroCorpo == corpoAlvo)
                    continue;

                var forca = CalculosFisicos.CalcularForcaComponentes(
                    corpoAlvo,
                    outroCorpo
                );

                forcaTotalX += forca.X;
                forcaTotalY += forca.Y;
            }

            return (forcaTotalX, forcaTotalY);
        }

    }
}