using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HologramaGravitacional
{
    public class Simulador
    {
        // Acima desta quantidade de corpos, a lista detalhada de
        // posições por iteração não é impressa: escrever milhares de
        // linhas no console por iteração é muito mais lento que a
        // própria física e esconderia o ganho do paralelismo.
        // Os dados completos continuam sendo gravados no banco.
        private const int LimiteCorposDetalhados = 20;

        private readonly Universo universo;

        // Pode ser nulo: nesse caso a simulação roda sem gravar
        // (por exemplo, se o MySQL não estiver disponível).
        private readonly GravadorSimulacao? gravador;

        // Tempo gasto somente com Universo.ExecutarIteracao (física).
        // Não inclui console, análise de conservação nem banco.
        private readonly Stopwatch cronometroFisica = new Stopwatch();

        public TimeSpan TempoProcessamento => cronometroFisica.Elapsed;

        public Simulador(Universo universo, GravadorSimulacao? gravador = null)
        {
            this.universo = universo;
            this.gravador = gravador;
        }

        // Classe responsável por orquestrar a execução temporal da simulação.
        // Calcula estados iniciais (momento, centro de massa, energia), executa
        // as iterações configuradas no Universo, envia cada iteração para
        // gravação no banco e imprime informações físicas e de diagnóstico.

        public void Executar()
        {
            bool paralelo = universo.Modo == ModoProcessamento.Paralelo;

            // Estado inicial (iteração 0) também é gravado, para que o
            // replay comece exatamente de onde a simulação começou.
            gravador?.Registrar(0, universo);

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
                    universo.Corpos,
                    paralelo
                );

            // Guarda quantas fusões já existiam antes desta execução.
            int fusoesIniciais = universo.QuantidadeFusoes;

            // ============================================================
            // EXIBE ESTADO INICIAL
            // ============================================================

            Console.WriteLine();
            Console.WriteLine($"Modo de processamento: {universo.Modo}");
            Console.WriteLine($"Processadores lógicos disponíveis: {Environment.ProcessorCount}");

            if (gravador != null)
            {
                Console.WriteLine($"Gravando no banco como simulação nº {gravador.SimulacaoId}");
            }
            else
            {
                Tela.Aviso("Esta simulação NÃO está sendo gravada no banco.");
            }

            Console.WriteLine();
            Tela.Secao("MOMENTO INICIAL");

            Console.WriteLine(
                $"Momento X: {momentoInicial.X:E15} kg.m/s"
            );

            Console.WriteLine(
                $"Momento Y: {momentoInicial.Y:E15} kg.m/s"
            );

            Console.WriteLine();
            Tela.Secao("CENTRO DE MASSA INICIAL");

            Console.WriteLine(
                $"X: {centroMassaInicial.X:E15} m"
            );

            Console.WriteLine(
                $"Y: {centroMassaInicial.Y:E15} m"
            );

            Console.WriteLine();
            Tela.Secao("ENERGIA INICIAL");

            Console.WriteLine(
                $"Energia mecânica: {energiaInicial:E15} J"
            );

            
            // ============================================================
            // SIMULAÇÃO
            // ============================================================

            for (int iteracao = 1;
                 iteracao <= universo.QuantidadeIteracoes;
                 iteracao++)
            {
                // Só a chamada da física fica dentro do cronômetro.
                cronometroFisica.Start();

                universo.ExecutarIteracao(
                    universo.TempoEntreIteracoes
                );

                cronometroFisica.Stop();

                // Envia o novo estado para a fila de gravação.
                // Retorna quase imediatamente: quem grava é outra thread.
                gravador?.Registrar(iteracao, universo);

                Console.WriteLine();
                Tela.Secao($"ITERAÇÃO {iteracao}");

                Console.WriteLine(
                    $"Quantidade de corpos: " +
                    $"{universo.QuantidadeCorpos}"
                );

                if (universo.QuantidadeCorpos <= LimiteCorposDetalhados)
                {
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
                }
                else
                {
                    Console.WriteLine(
                        $"(posições individuais omitidas: mais de " +
                        $"{LimiteCorposDetalhados} corpos; consulte o replay)"
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
                        universo.Corpos,
                        paralelo
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

                Tela.Escrever(ConsoleColor.DarkCyan, "  · Conservação física\n");

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
                    $"{energiaAtual:E15} J"
                );

                bool houveFusaoNaSimulacao =
                    universo.QuantidadeFusoes > fusoesIniciais;

                if (universo.OcorreuFusao)
                {
                    // Mensagem exibida apenas na iteração em que a fusão ocorreu.
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
                else if (houveFusaoNaSimulacao)
                {
                    // Após uma fusão, comparar com a energia inicial deixa
                    // de medir erro numérico, pois parte da energia foi
                    // dissipada na colisão.
                    Console.WriteLine(
                        "Erro de energia não calculado: houve fusão " +
                        "em iteração anterior."
                    );
                }
                else
                {
                    Console.WriteLine(
                        $"Erro de energia: " +
                        $"{erroEnergia:E15} J"
                    );

                    Console.WriteLine(
                        $"Erro percentual: " +
                        $"{erroEnergiaPercentual:E15} %"
                    );
                }

                

                // O Δt (TempoEntreIteracoes) é tempo físico simulado,
                // não tempo real. Por isso não há espera entre iterações.
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
                    universo.Corpos,
                    paralelo
                );

            Console.WriteLine();
            
            Tela.Secao("RESULTADO DA CONSERVAÇÃO");

            
            Console.WriteLine();

            Tela.Escrever(ConsoleColor.Cyan, "  MOMENTO LINEAR\n");

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

            Tela.Escrever(ConsoleColor.Cyan, "  CENTRO DE MASSA\n");

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

            Tela.Escrever(ConsoleColor.Cyan, "  ENERGIA MECÂNICA\n");

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

            if (universo.QuantidadeFusoes > fusoesIniciais)
            {
                Console.WriteLine();

                Console.WriteLine(
                    $"Observação: durante a simulação ocorreram " +
                    $"{universo.QuantidadeFusoes - fusoesIniciais} fusão(ões) de corpos."
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

            // ============================================================
            // DESEMPENHO
            // ============================================================

            Console.WriteLine();
            Tela.Secao("DESEMPENHO");
            Console.WriteLine($"Modo: {universo.Modo}");
            Console.WriteLine(
                $"Tempo de processamento físico: " +
                $"{TempoProcessamento.TotalMilliseconds:F1} ms"
            );

            if (universo.QuantidadeIteracoes > 0)
            {
                Console.WriteLine(
                    $"Média por iteração: " +
                    $"{TempoProcessamento.TotalMilliseconds / universo.QuantidadeIteracoes:F3} ms"
                );
            }

            Console.WriteLine();
        }
    }
}
