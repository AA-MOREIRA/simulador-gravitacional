using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HologramaGravitacional
{
    public class Simulador
    {
        private readonly Universo universo;

        public Simulador(Universo universo)
        {
            this.universo = universo;
        }

        // Classe responsável por orquestrar a execução temporal da simulação.
        // Calcula estados iniciais (momento, centro de massa, energia), executa
        // as iterações configuradas no Universo e imprime informações
        // físicas e de diagnóstico a cada passo.

        public void Executar()
        {
            // ============================================================
            // ESTADO INICIAL
            // ============================================================

            var momentoInicial =
                CalculosFisicos.CalcularQuantidadeMovimentoTotal(
                    universo.Corpos
                );

            var centroMassaInicial =
                CalculosFisicos.CalcularCentroDeMassaTotal(
                    universo.Corpos
                );

            double energiaInicial =
                CalculosFisicos.CalcularEnergiaMecanicaTotal(
                    universo.Corpos
                );

            // ============================================================
            // EXIBE ESTADO INICIAL
            // ============================================================

            Console.WriteLine();
            Console.WriteLine("========== MOMENTO INICIAL ==========");

            Console.WriteLine(
                $"Momento X: {momentoInicial.X:E15} kg.m/s"
            );

            Console.WriteLine(
                $"Momento Y: {momentoInicial.Y:E15} kg.m/s"
            );

            Console.WriteLine();
            Console.WriteLine("========== CENTRO DE MASSA INICIAL ==========");

            Console.WriteLine(
                $"X: {centroMassaInicial.X:E15} m"
            );

            Console.WriteLine(
                $"Y: {centroMassaInicial.Y:E15} m"
            );

            Console.WriteLine();
            Console.WriteLine("========== ENERGIA INICIAL ==========");

            Console.WriteLine(
                $"Energia mecânica: {energiaInicial:E15} J"
            );

            Console.WriteLine(
                "==============================================");

            // ============================================================
            // SIMULAÇÃO
            // ============================================================

            for (int iteracao = 1;
                 iteracao <= universo.QuantidadeIteracoes;
                 iteracao++)
            {
                universo.ExecutarIteracao(
                    universo.TempoEntreIteracoes
                );

                Console.WriteLine();
                Console.WriteLine(
                    $"========== ITERAÇÃO {iteracao} =========="
                );

                Console.WriteLine(
                    $"Quantidade de corpos: " +
                    $"{universo.QuantidadeCorpos}"
                );

                foreach (Corpo corpo in universo.Corpos)
                {
                    Console.WriteLine(
                        $"{corpo.Nome} | " +
                        $"X: {corpo.PosX:E15} m | " +
                        $"Y: {corpo.PosY:E15} m | " +
                        $"VX: {corpo.VelX:E15} m/s | " +
                        $"VY: {corpo.VelY:E15} m/s"
                    );
                }

                // ========================================================
                // ANÁLISE FÍSICA DA ITERAÇÃO
                // ========================================================

                var momentoAtual =
                    CalculosFisicos.CalcularQuantidadeMovimentoTotal(
                        universo.Corpos
                    );

                var centroMassaAtual =
                    CalculosFisicos.CalcularCentroDeMassaTotal(
                        universo.Corpos
                    );

                double energiaAtual =
                    CalculosFisicos.CalcularEnergiaMecanicaTotal(
                        universo.Corpos
                    );

                double erroMomentoX =
                    momentoAtual.X - momentoInicial.X;

                double erroMomentoY =
                    momentoAtual.Y - momentoInicial.Y;

                double deslocamentoCentroX =
                    centroMassaAtual.X - centroMassaInicial.X;

                double deslocamentoCentroY =
                    centroMassaAtual.Y - centroMassaInicial.Y;

                double erroEnergia =
                    energiaAtual - energiaInicial;

                double erroEnergiaPercentual = 0;

                if (energiaInicial != 0)
                {
                    erroEnergiaPercentual =
                        (erroEnergia /
                         Math.Abs(energiaInicial)) * 100;
                }

                // ========================================================
                // MOSTRA ANÁLISE
                // ========================================================

                Console.WriteLine();

                Console.WriteLine(
                    "----- CONSERVAÇÃO FÍSICA -----"
                );

                Console.WriteLine(
                    $"Momento X: {momentoAtual.X:E15}"
                );

                Console.WriteLine(
                    $"Erro Momento X: {erroMomentoX:E15}"
                );

                Console.WriteLine(
                    $"Momento Y: {momentoAtual.Y:E15}"
                );

                Console.WriteLine(
                    $"Erro Momento Y: {erroMomentoY:E15}"
                );

                Console.WriteLine();

                Console.WriteLine(
                    $"Centro de Massa X: " +
                    $"{centroMassaAtual.X:E15} m"
                );

                Console.WriteLine(
                    $"Centro de Massa Y: " +
                    $"{centroMassaAtual.Y:E15} m"
                );

                Console.WriteLine(
                    $"Deslocamento CM X: " +
                    $"{deslocamentoCentroX:E15} m"
                );

                Console.WriteLine(
                    $"Deslocamento CM Y: " +
                    $"{deslocamentoCentroY:E15} m"
                );

                Console.WriteLine();

                Console.WriteLine(
                $"Energia mecânica: " +
                $"{energiaAtual} J"
                );

                if (universo.OcorreuFusao)
                {
                    Console.WriteLine();

                    Console.WriteLine(
                        "Observação: ocorreu uma colisão com fusão de corpos."
                    );

                    Console.WriteLine(
                        "A quantidade de movimento é conservada, porém a"
                    );

                    Console.WriteLine(
                        "energia mecânica pode ser convertida em outras formas"
                    );

                    Console.WriteLine(
                        "de energia durante uma colisão inelástica."
                    );

                }
                else
                {
                    Console.WriteLine(
                    $"Erro de energia: " +
                    $"{erroEnergia} J"
                    );

                    Console.WriteLine(
                        $"Erro percentual: " +
                        $"{erroEnergiaPercentual:E15} %"
                    );

                }

                Console.WriteLine(
                    "------------------------------"
                );

                // ========================================================
                // AGUARDA PRÓXIMA ITERAÇÃO
                // ========================================================

                if (universo.TempoEntreIteracoes > 0)
                {
                    int tempoEspera =
                        (int)(
                            universo.TempoEntreIteracoes *
                            1000
                        );

                    Thread.Sleep(tempoEspera);
                }
            }

            // ============================================================
            // RESULTADO FINAL
            // ============================================================

            var momentoFinal =
                CalculosFisicos.CalcularQuantidadeMovimentoTotal(
                    universo.Corpos
                );

            var centroMassaFinal =
                CalculosFisicos.CalcularCentroDeMassaTotal(
                    universo.Corpos
                );

            double energiaFinal =
                CalculosFisicos.CalcularEnergiaMecanicaTotal(
                    universo.Corpos
                );

            Console.WriteLine();
            Console.WriteLine(
                "=============================================="
            );

            Console.WriteLine(
                "          RESULTADO DA CONSERVAÇÃO"
            );

            Console.WriteLine(
                "=============================================="
            );

            Console.WriteLine();

            Console.WriteLine("MOMENTO LINEAR");

            Console.WriteLine(
                $"Inicial X: {momentoInicial.X:E15}"
            );

            Console.WriteLine(
                $"Final X:   {momentoFinal.X:E15}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Inicial Y: {momentoInicial.Y:E15}"
            );

            Console.WriteLine(
                $"Final Y:   {momentoFinal.Y:E15}"
            );

            Console.WriteLine();

            Console.WriteLine("CENTRO DE MASSA");

            Console.WriteLine(
                $"Inicial X: {centroMassaInicial.X:E15}"
            );

            Console.WriteLine(
                $"Final X:   {centroMassaFinal.X:E15}"
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Inicial Y: {centroMassaInicial.Y:E15}"
            );

            Console.WriteLine(
                $"Final Y:   {centroMassaFinal.Y:E15}"
            );

            Console.WriteLine();

            Console.WriteLine("ENERGIA MECÂNICA");

            Console.WriteLine(
                $"Inicial: {energiaInicial:E15} J"
            );

            Console.WriteLine(
                $"Final:   {energiaFinal:E15} J"
            );

            Console.WriteLine(
                $"Variação: " +
                $"{energiaFinal - energiaInicial:E15} J"
            );

            if (universo.OcorreuFusao)
            {
                Console.WriteLine();

                Console.WriteLine(
                    "Observação: durante a simulação ocorreu fusão de corpos."
                );

                Console.WriteLine(
                    "Neste modelo de colisão inelástica, a quantidade"
                );

                Console.WriteLine(
                    "de movimento é conservada, porém a energia mecânica"
                );

                Console.WriteLine(
                    "pode ser convertida em outras formas de energia."
                );

            }
            else if (energiaInicial != 0)
            {
                double erroFinal =
                ((energiaFinal - energiaInicial) /
                Math.Abs(energiaInicial)) * 100;

                Console.WriteLine(
                    $"Erro percentual: {erroFinal:E15} %"
                );

            }

            Console.WriteLine();
            Console.WriteLine(
                "=============================================="
            );
        }
    }
}