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

        // Classe responsável por gerar um conjunto de corpos com
        // propriedades físicas plausíveis e distribuir suas posições
        // de forma a evitar sobreposição inicial. Esta classe é usada
        // quando o usuário solicita criação randômica de corpos.

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
            // Gera propriedades físicas básicas para o corpo.
            // A massa e densidade seguem faixas definidas abaixo;
            // a posição é sorteada dentro de uma área quadrada centrada
            // na origem e as velocidades são geradas aleatoriamente
            // (ou zeradas se solicitado).
            double massa = GerarMassa();

            double densidade = GerarDensidade();

            double posX = GerarPosicao(tamanhoArea);
            double posY = GerarPosicao(tamanhoArea);

            double velX;
            double velY;

            if (velocidadeInicialZero)
            {
                // Cenários estáticos iniciais podem ser úteis
                // para testar atração puramente gravitacional.
                velX = 0;
                velY = 0;
            }
            else
            {
                // Velocidades iniciais aleatórias em ambos os eixos.
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
            // Valores grandes facilitam a observação dos efeitos
            // gravitacionais em escalas práticas do simulador.
            return random.NextDouble() * 9e12 + 1e12;
        }

        private double GerarDensidade()
        {
            // Gera uma densidade dentro de uma faixa
            // válida para os corpos do simulador.
            // A faixa começa em valores muito baixos (gases) e sobe
            // até materiais extremamente densos. O construtor de
            // Corpo rejeita densidades acima do limite definido.
            return random.NextDouble() * (22590 - 0.0899) + 0.0899;
        }

        private double GerarPosicao(double tamanhoArea)
        {
            // Distribui os corpos ao redor da origem.
            // O valor retornado fica no intervalo [-tamanhoArea, +tamanhoArea].
            return (random.NextDouble() * 2 - 1) * tamanhoArea;
        }

        private double GerarVelocidade()
        {
            // Velocidade inicial entre -1000 e +1000 m/s.
            return (random.NextDouble() * 2000.0) - 1000.0;
        }
    }
}