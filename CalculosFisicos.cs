using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HologramaGravitacional
{
    public static class CalculosFisicos
    {
        public const double G = 6.67430e-11;

        public static double CalcularDistancia(Corpo corpoA, Corpo corpoB)
        {
            double dx = corpoB.PosX - corpoA.PosX;
            double dy = corpoB.PosY - corpoA.PosY;

            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        public static double CalcularForcaGravitacional(
            Corpo corpoA,
            Corpo corpoB)
        {
            double distancia = CalcularDistancia(corpoA, corpoB);

            if (distancia == 0)
            {
                return 0;
            }

            return G * corpoA.Massa * corpoB.Massa /
                   (distancia * distancia);
        }

        public static (double X, double Y) CalcularForcaComponentes(
            Corpo corpoA,
            Corpo corpoB)
        {
            double dx = corpoB.PosX - corpoA.PosX;
            double dy = corpoB.PosY - corpoA.PosY;

            double distancia = CalcularDistancia(corpoA, corpoB);

            if (distancia == 0)
            {
                return (0, 0);
            }

            double forca = CalcularForcaGravitacional(corpoA, corpoB);

            double forcaX = forca * dx / distancia;
            double forcaY = forca * dy / distancia;

            return (forcaX, forcaY);
        }

        public static (double X, double Y) CalcularAceleracao(
            Corpo corpo,
            double forcaX,
            double forcaY)
        {
            if (corpo.Massa <= 0)
            {
                return (0, 0);
            }

            double aceleracaoX = forcaX / corpo.Massa;
            double aceleracaoY = forcaY / corpo.Massa;

            return (aceleracaoX, aceleracaoY);
        }

        public static double CalcularVolume(Corpo corpo)
        {
            return corpo.Massa / corpo.Densidade;
        }
        public static double CalcularDensidadeResultante(
            Corpo corpoA,
            Corpo corpoB)
        {
            double massaTotal = corpoA.Massa + corpoB.Massa;

            double volumeA = CalcularVolume(corpoA);
            double volumeB = CalcularVolume(corpoB);

            double volumeTotal = volumeA + volumeB;

            return massaTotal / volumeTotal;
        }
        public static (double X, double Y) CalcularVelocidadeResultante(
            Corpo corpoA,
            Corpo corpoB)
        {
            double massaTotal = corpoA.Massa + corpoB.Massa;

            if (massaTotal <= 0)
            {
                return (0, 0);
            }

            double quantidadeMovimentoX =
                (corpoA.Massa * corpoA.VelX) +
                (corpoB.Massa * corpoB.VelX);

            double quantidadeMovimentoY =
                (corpoA.Massa * corpoA.VelY) +
                (corpoB.Massa * corpoB.VelY);

            double velocidadeX =
                quantidadeMovimentoX / massaTotal;

            double velocidadeY =
                quantidadeMovimentoY / massaTotal;

            return (velocidadeX, velocidadeY);
        }
        public static (double X, double Y) CalcularCentroDeMassa(
            Corpo corpoA,
            Corpo corpoB)
        {
            double massaTotal = corpoA.Massa + corpoB.Massa;

            if (massaTotal <= 0)
            {
                return (0, 0);
            }

            double posicaoX =
                ((corpoA.Massa * corpoA.PosX) +
                 (corpoB.Massa * corpoB.PosX))
                / massaTotal;

            double posicaoY =
                ((corpoA.Massa * corpoA.PosY) +
                 (corpoB.Massa * corpoB.PosY))
                / massaTotal;

            return (posicaoX, posicaoY);
        }
        public static Corpo CriarCorpoResultante(
            Corpo corpoA,
            Corpo corpoB)
        {
            double massaTotal = corpoA.Massa + corpoB.Massa;

            double densidadeResultante =
                CalcularDensidadeResultante(corpoA, corpoB);

            var centroDeMassa =
                CalcularCentroDeMassa(corpoA, corpoB);

            var velocidadeResultante =
                CalcularVelocidadeResultante(corpoA, corpoB);

            string nome =
                corpoA.Nome + "-" + corpoB.Nome;

            return new Corpo(
                nome,
                massaTotal,
                densidadeResultante,
                centroDeMassa.X,
                centroDeMassa.Y,
                velocidadeResultante.X,
                velocidadeResultante.Y
            );
        }
        public static (double X, double Y) CalcularQuantidadeMovimentoTotal(
    IReadOnlyList<Corpo> corpos)
        {
            double momentoX = 0;
            double momentoY = 0;

            foreach (Corpo corpo in corpos)
            {
                momentoX += corpo.Massa * corpo.VelX;
                momentoY += corpo.Massa * corpo.VelY;
            }

            return (momentoX, momentoY);
        }
        public static (double X, double Y) CalcularCentroDeMassaTotal(
    IReadOnlyList<Corpo> corpos)
        {
            double massaTotal = 0;
            double posicaoX = 0;
            double posicaoY = 0;

            foreach (Corpo corpo in corpos)
            {
                massaTotal += corpo.Massa;

                posicaoX += corpo.Massa * corpo.PosX;
                posicaoY += corpo.Massa * corpo.PosY;
            }

            if (massaTotal <= 0)
            {
                return (0, 0);
            }

            return (
                posicaoX / massaTotal,
                posicaoY / massaTotal
            );
        }
        public static double CalcularEnergiaCineticaTotal(
    IReadOnlyList<Corpo> corpos)
        {
            double energia = 0;

            foreach (Corpo corpo in corpos)
            {
                double velocidadeQuadrado =
                    (corpo.VelX * corpo.VelX) +
                    (corpo.VelY * corpo.VelY);

                energia +=
                    0.5 *
                    corpo.Massa *
                    velocidadeQuadrado;
            }

            return energia;
        }
        public static double CalcularEnergiaPotencialGravitacional(
    IReadOnlyList<Corpo> corpos)
        {
            double energia = 0;

            for (int i = 0; i < corpos.Count; i++)
            {
                for (int j = i + 1; j < corpos.Count; j++)
                {
                    double distancia =
                        CalcularDistancia(
                            corpos[i],
                            corpos[j]
                        );

                    if (distancia > 0)
                    {
                        energia -=
                            G *
                            corpos[i].Massa *
                            corpos[j].Massa /
                            distancia;
                    }
                }
            }

            return energia;
        }
        public static double CalcularEnergiaMecanicaTotal(
    IReadOnlyList<Corpo> corpos)
        {
            double energiaCinetica =
                CalcularEnergiaCineticaTotal(corpos);

            double energiaPotencial =
                CalcularEnergiaPotencialGravitacional(corpos);

            return energiaCinetica + energiaPotencial;
        }
    }
}