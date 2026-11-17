using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace HologramaGravitacional
{
    // Mede o tempo de processamento da física nas duas versões:
    //   - N1: Universo em modo Sequencial (algoritmo original);
    //   - N2: Universo em modo Paralelo.
    //
    // Para a comparação ser justa:
    //   1. os dois Universos são gerados com a MESMA semente,
    //      portanto começam com corpos idênticos;
    //   2. mede-se apenas Universo.ExecutarIteracao (sem console e
    //      sem banco, que dependem de fatores externos);
    //   3. antes das medições é feito um "aquecimento" para que a
    //      compilação JIT do .NET não seja contada no primeiro teste;
    //   4. no final confere-se se as duas versões chegaram às mesmas
    //      posições: paralelizar não pode mudar o resultado físico.
    public static class ComparadorPerformance
    {
        private const int Semente = 2026;
        private const double TamanhoArea = 1_000_000; // metros
        private const double DeltaTempo = 100;        // segundos

        // Cada medição é repetida e usa-se a MEDIANA: ela descarta o efeito
        // de interrupções ocasionais (outro programa usando a CPU, coleta de
        // lixo do .NET), que distorceriam uma medição única ou a média.
        private const int Repeticoes = 3;

#if DEBUG
        private const string Configuracao = "Debug";
#else
        private const string Configuracao = "Release";
#endif

        public static void Executar(
            int[] quantidadesCorpos,
            int iteracoes,
            string caminhoCsv)
        {
            int processadores = Environment.ProcessorCount;

            Tela.Secao("COMPARATIVO DE PERFORMANCE: N1 (sequencial) x N2 (paralelo)");
            Tela.Campo("Processadores lógicos", processadores.ToString());
            Tela.Campo("Processador", NomeProcessador());
            Tela.Campo("Iterações por teste", iteracoes.ToString());
            Tela.Campo("Repetições (mediana)", Repeticoes.ToString());
            Tela.Campo("Configuração", Configuracao);
            Tela.Campo("Δt", $"{DeltaTempo} s");
            Console.WriteLine();

            // Com o depurador conectado (F5) ou em modo Debug, o .NET desliga
            // otimizações do compilador JIT: tudo fica várias vezes mais lento
            // e as medições deixam de representar o desempenho real.
            if (Debugger.IsAttached)
            {
                Tela.Aviso("Depurador conectado: os tempos ficam bem mais lentos e menos confiáveis.");
                Tela.Aviso("Para o relatório, use a configuração Release e rode com Ctrl+F5.");
                Console.WriteLine();
            }

            Tela.Info("Pode levar alguns minutos. Aquecendo (compilação JIT)...");

            Aquecer();

            var csv = new StringBuilder();
            // Separador ";" e vírgula decimal: abre direto no Excel em português.
            var culturaBr = new CultureInfo("pt-BR");
            csv.AppendLine(
                "Corpos;Iteracoes;Sequencial (ms);Paralelo (ms);" +
                "Speedup;Eficiencia (%);Resultados identicos;Processadores");

            Console.WriteLine();
            Tela.Escrever(ConsoleColor.DarkGray,
                $"  {"Corpos",8}   {"Sequencial (ms)",16}   {"Paralelo (ms)",14}   " +
                $"{"Speedup",8}   {"Eficiência",10}   {"Iguais",6}\n");
            Tela.Escrever(ConsoleColor.DarkCyan, "  " + new string('─', 82) + "\n");

            foreach (int quantidade in quantidadesCorpos)
            {
                var temposSequencial = new List<double>();
                var temposParalelo = new List<double>();
                bool iguais = true;

                for (int r = 1; r <= Repeticoes; r++)
                {
                    // Universos novos a cada repetição: todos começam idênticos.
                    Universo sequencial = CriarUniverso(quantidade, ModoProcessamento.Sequencial);
                    Universo paralelo = CriarUniverso(quantidade, ModoProcessamento.Paralelo);

                    // Mostra o andamento na própria linha; "\r" volta ao início
                    // da linha, e o resultado final é escrito por cima.
                    Tela.Escrever(ConsoleColor.DarkGray, $"  {quantidade,8}   medindo sequencial ({r}/{Repeticoes})...   \r");
                    temposSequencial.Add(Medir(sequencial, iteracoes));

                    Tela.Escrever(ConsoleColor.DarkGray, $"  {quantidade,8}   medindo paralelo ({r}/{Repeticoes})...     \r");
                    temposParalelo.Add(Medir(paralelo, iteracoes));

                    iguais &= MesmoResultado(sequencial, paralelo);
                }

                double tempoSequencial = Mediana(temposSequencial);
                double tempoParalelo = Mediana(temposParalelo);

                // Speedup = quantas vezes a versão paralela foi mais rápida.
                double speedup = tempoSequencial / tempoParalelo;

                // Eficiência = speedup / nº de processadores.
                // 100% significaria aproveitar todos os núcleos perfeitamente;
                // na prática é menor por causa do custo de dividir o trabalho
                // entre threads e sincronizá-las a cada etapa.
                double eficiencia = speedup / processadores * 100;

                // Verde quando o paralelo ganhou, amarelo quando perdeu.
                Tela.Escrever(ConsoleColor.Cyan, $"  {quantidade,8}   ");
                Tela.Escrever(ConsoleColor.Gray, $"{tempoSequencial,16:F1}   {tempoParalelo,14:F1}   ");
                Tela.Escrever(speedup >= 1 ? ConsoleColor.Green : ConsoleColor.Yellow, $"{speedup,7:F2}x");
                Tela.Escrever(ConsoleColor.Gray, $"   {eficiencia,9:F1}%   ");
                Tela.Escrever(iguais ? ConsoleColor.Green : ConsoleColor.Red, $"{(iguais ? "Sim" : "NÃO"),6}\n");

                csv.AppendLine(string.Join(";",
                    quantidade.ToString(culturaBr),
                    iteracoes.ToString(culturaBr),
                    tempoSequencial.ToString("F1", culturaBr),
                    tempoParalelo.ToString("F1", culturaBr),
                    speedup.ToString("F2", culturaBr),
                    eficiencia.ToString("F1", culturaBr),
                    iguais ? "Sim" : "Nao",
                    processadores.ToString(culturaBr)));
            }

            File.WriteAllText(caminhoCsv, csv.ToString(), Encoding.UTF8);

            // Ambiente do teste, para o relatório (os tempos só fazem sentido
            // junto com a máquina em que foram medidos).
            string caminhoAmbiente = Path.ChangeExtension(caminhoCsv, null) + "_ambiente.txt";
            File.WriteAllText(caminhoAmbiente, string.Join(Environment.NewLine,
                $"Data: {DateTime.Now:dd/MM/yyyy HH:mm}",
                $"Processador: {NomeProcessador()}",
                $"Processadores lógicos: {processadores}",
                $"Sistema: {RuntimeInformation.OSDescription}",
                $".NET: {RuntimeInformation.FrameworkDescription}",
                $"Configuração: {Configuracao}",
                $"Depurador conectado: {(Debugger.IsAttached ? "sim" : "não")}",
                $"Iterações por teste: {iteracoes}",
                $"Repetições por medição (mediana): {Repeticoes}",
                $"Δt: {DeltaTempo} s",
                $"Área inicial: ±{TamanhoArea:N0} m",
                $"Semente aleatória: {Semente}"), Encoding.UTF8);

            Console.WriteLine();
            Tela.Sucesso($"Resultados salvos em: {Path.GetFullPath(caminhoCsv)}");
            Tela.Info("Use esse arquivo para montar as tabelas e gráficos do relatório.");
        }

        private static double Mediana(List<double> valores)
        {
            var ordenados = valores.OrderBy(v => v).ToList();
            int meio = ordenados.Count / 2;

            return ordenados.Count % 2 == 1
                ? ordenados[meio]
                : (ordenados[meio - 1] + ordenados[meio]) / 2;
        }

        // Nome do processador, lido do registro do Windows.
        private static string NomeProcessador()
        {
            try
            {
                using var chave = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");

                return (chave?.GetValue("ProcessorNameString") as string)?.Trim() ?? "desconhecido";
            }
            catch (Exception)
            {
                return "desconhecido";
            }
        }

        private static Universo CriarUniverso(int quantidade, ModoProcessamento modo)
        {
            var universo = new Universo
            {
                Modo = modo,
                TempoEntreIteracoes = DeltaTempo,
                ExibirColisoes = false
            };

            // Mesma semente => mesmos corpos nas duas versões.
            var gerador = new GeradorCorpos(Semente);

            foreach (Corpo corpo in gerador.Gerar(quantidade, TamanhoArea))
            {
                universo.AdicionarCorpo(corpo);
            }

            return universo;
        }

        private static double Medir(Universo universo, int iteracoes)
        {
            var cronometro = Stopwatch.StartNew();

            for (int i = 0; i < iteracoes; i++)
            {
                universo.ExecutarIteracao(universo.TempoEntreIteracoes);
            }

            cronometro.Stop();

            return cronometro.Elapsed.TotalMilliseconds;
        }

        // Roda um teste pequeno nas duas versões e descarta o tempo.
        // Na primeira execução de cada método, o .NET compila o código
        // (JIT) e cria as threads do pool; sem o aquecimento, esse custo
        // entraria na primeira medição e distorceria o resultado.
        //
        // O .NET também usa "compilação em camadas": o código começa pouco
        // otimizado e só é recompilado com otimização completa depois de
        // muito usado. Por isso o aquecimento roda bastante trabalho
        // (algumas centenas de milissegundos) antes de medir.
        private static void Aquecer()
        {
            for (int i = 0; i < 3; i++)
            {
                Medir(CriarUniverso(500, ModoProcessamento.Sequencial), 20);
                Medir(CriarUniverso(500, ModoProcessamento.Paralelo), 20);
            }
        }

        // Compara corpo a corpo as duas simulações após as iterações.
        private static bool MesmoResultado(Universo a, Universo b)
        {
            if (a.QuantidadeCorpos != b.QuantidadeCorpos)
            {
                return false;
            }

            for (int i = 0; i < a.QuantidadeCorpos; i++)
            {
                Corpo ca = a.Corpos[i];
                Corpo cb = b.Corpos[i];

                // Comparação exata: cada força é somada na mesma ordem nas
                // duas versões (o laço interno não foi dividido), então os
                // números devem ser idênticos bit a bit.
                if (ca.Nome != cb.Nome ||
                    ca.PosX != cb.PosX || ca.PosY != cb.PosY ||
                    ca.VelX != cb.VelX || ca.VelY != cb.VelY)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
