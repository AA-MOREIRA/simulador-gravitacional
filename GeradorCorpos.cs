using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HologramaGravitacional
{
    public class GeradorCorpos
    {
        private readonly Random random;

        public GeradorCorpos()
        {
            random = new Random();
        }

        public List<Corpo> Gerar(
            int quantidade,
            double tamanhoArea,
            bool velocidadeInicialZero = false)
        {
            List<Corpo> corpos = new List<Corpo>();

            for (int i = 1; i <= quantidade; i++)
            {
                Corpo novoCorpo;

                int tentativas = 0;

                do
                {
                    novoCorpo = CriarCorpo(
                                 $"Corpo-{i}",
                                 tamanhoArea,
                                 velocidadeInicialZero
                             );

                    tentativas++;

                    // Evita uma tentativa infinita caso
                    // a área seja pequena demais para acomodar
                    // a quantidade solicitada de corpos.
                    if (tentativas > 10000)
                    {
                        throw new InvalidOperationException(
                            "Não foi possível distribuir os corpos " +
                            "sem sobreposição na área informada."
                        );
                    }

                } while (ExisteSobreposicao(novoCorpo, corpos));

                corpos.Add(novoCorpo);
            }

            return corpos;
        }

        private Corpo CriarCorpo(
             string nome,
             double tamanhoArea,
             bool velocidadeInicialZero)
        {
            double massa = GerarMassa();

            double densidade = GerarDensidade();

            double posX = GerarPosicao(tamanhoArea);
            double posY = GerarPosicao(tamanhoArea);

            double velX;
            double velY;

            if (velocidadeInicialZero)
            {
                velX = 0;
                velY = 0;
            }
            else
            {
                velX = GerarVelocidade();
                velY = GerarVelocidade();
            }

            return new Corpo(
                nome,
                massa,
                densidade,
                posX,
                posY,
                velX,
                velY
            );
        }

        private bool ExisteSobreposicao(
            Corpo novoCorpo,
            List<Corpo> corpos)
        {
            foreach (Corpo corpo in corpos)
            {
                double distancia =
                    CalculosFisicos.CalcularDistancia(
                        novoCorpo,
                        corpo
                    );

                double distanciaMinima =
                    novoCorpo.Raio + corpo.Raio;

                if (distancia <= distanciaMinima)
                {
                    return true;
                }
            }

            return false;
        }

        private double GerarMassa()
        {
            // Massa aleatória entre 1 e 10 trilhões de kg.
            return random.NextDouble() * 9e12 + 1e12;
        }

        private double GerarDensidade()
        {
            // Gera uma densidade dentro de uma faixa
            // válida para os corpos do simulador.
            return random.NextDouble() * (22590 - 0.0899) + 0.0899;
        }

        private double GerarPosicao(double tamanhoArea)
        {
            // Distribui os corpos ao redor da origem.
            return (random.NextDouble() * 2 - 1)
                   * tamanhoArea;
        }

        private double GerarVelocidade()
        {
            // Velocidade inicial entre -1000 e +1000 m/s.
            return (random.NextDouble() * 2) - 1;
        }
    }
}