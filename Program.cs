using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace HologramaGravitacional
{
    internal class Program
    {
        // O Windows Forms exige que a thread principal use o modelo
        // STA (Single-Threaded Apartment). Sem este atributo, o
        // visualizador 2D pode travar ou lançar exceções.
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Tela.Configurar();

            // Programa principal: gerencia o menu, a criação do Universo,
            // a simulação (com gravação no MySQL), o replay e o
            // comparativo de performance.

            IRepositorioSimulacao? repositorio = ConectarBanco(out string statusBanco);

            while (true)
            {
                // Loop principal do programa. Mantém o menu ativo até que
                // o usuário escolha sair.
                Tela.Banner(statusBanco);

                Tela.OpcaoMenu("1", "Nova simulação", "Gera corpos aleatórios, simula e grava no MySQL");
                Tela.OpcaoMenu("2", "Replay", "Reproduz uma simulação gravada no banco");
                Tela.OpcaoMenu("3", "Teste de colisão", "Dois corpos em rota de colisão");
                Tela.OpcaoMenu("4", "Comparar performance", "Sequencial (N1) x Paralelo (N2)");
                Tela.OpcaoMenu("5", "Sair", "");
                Console.WriteLine();

                string? opcao = Tela.Perguntar("Escolha uma opção: ")?.Trim();

                if (opcao == "1")
                {
                    Universo? universo = CriarUniversoAleatorio();

                    if (universo != null)
                    {
                        universo.Modo = LerModoProcessamento();

                        ExecutarSimulacao(
                            universo,
                            $"Aleatório - {universo.QuantidadeCorpos} corpos",
                            repositorio
                        );
                    }

                    Tela.Pausar();
                }
                else if (opcao == "2")
                {
                    if (repositorio == null)
                    {
                        Tela.Erro("Replay indisponível: sem conexão com o MySQL.");
                    }
                    else
                    {
                        ExecutarReplay(repositorio);
                    }

                    Tela.Pausar();
                }
                else if (opcao == "3")
                {
                    Universo universo = CriarCenarioColisao();
                    universo.Modo = LerModoProcessamento();

                    ExecutarSimulacao(universo, "Teste de colisão", repositorio);

                    Tela.Pausar();
                }
                else if (opcao == "4")
                {
                    // Quantidades crescentes de corpos: o ganho do paralelismo
                    // aparece conforme o trabalho O(n²) cresce. Com poucos
                    // corpos, o custo de criar e sincronizar threads pode até
                    // tornar a versão paralela mais lenta.
                    int[] quantidades = { 50, 100, 250, 500, 1000, 2000 };
                    int iteracoes = 10;

                    ComparadorPerformance.Executar(
                        quantidades,
                        iteracoes,
                        "comparativo_performance.csv"
                    );

                    Tela.Pausar();
                }
                else if (opcao == "5")
                {
                    Console.WriteLine();
                    Tela.Info("Até a próxima!");
                    break;
                }
                else
                {
                    Tela.Erro("Opção inválida.");
                    Tela.Pausar();
                }
            }
        }

        // ================================================================
        // CONEXÃO COM O BANCO
        // ================================================================

        // Na N2 a persistência em arquivo texto foi substituída pelo MySQL.
        // Tenta conectar; se falhar (por exemplo, numa máquina nova em que
        // a senha do MySQL é outra), pergunta os dados de conexão e tenta
        // de novo, até conectar ou o usuário desistir. Sem banco, o
        // programa continua funcionando, mas sem gravar e sem replay.
        private static IRepositorioSimulacao? ConectarBanco(out string status)
        {
            string stringConexao = ConfiguracaoBanco.ObterStringConexao();

            Tela.Info("Conectando ao MySQL...");

            while (true)
            {
                var repositorio = new RepositorioMySql(stringConexao);

                try
                {
                    repositorio.PrepararBanco();

                    // Lembra a conexão que funcionou para as próximas execuções.
                    ConfiguracaoBanco.SalvarStringConexao(stringConexao);

                    status = $"conectado ({ConfiguracaoBanco.NomeBanco})";
                    return repositorio;
                }
                catch (Exception erro)
                {
                    Console.WriteLine();
                    Tela.Erro("Não foi possível conectar ao MySQL.");
                    Tela.Info($"Motivo: {erro.Message}");
                    Console.WriteLine();

                    if (!LerSimNao("Deseja informar os dados de conexão? (s/n): "))
                    {
                        Tela.Aviso("As simulações vão rodar SEM gravação e o replay ficará indisponível.");
                        status = "desconectado (sem gravação)";
                        return null;
                    }

                    stringConexao = ConfiguracaoBanco.PerguntarStringConexao();
                }
            }
        }

        // ================================================================
        // CRIAÇÃO DOS UNIVERSOS
        // ================================================================

        // Opção 1: Universo aleatório. O usuário define quantos corpos
        // e quantas iterações a simulação deve executar.
        private static Universo? CriarUniversoAleatorio()
        {
            Tela.Secao("NOVA SIMULAÇÃO");

            int? quantidadeCorpos =
                LerInteiroPositivo("Quantidade de corpos: ");

            if (quantidadeCorpos == null)
            {
                Tela.Erro("Quantidade de corpos inválida.");
                return null;
            }

            int? quantidadeIteracoes =
                LerInteiroPositivo("Quantidade de iterações: ");

            if (quantidadeIteracoes == null)
            {
                Tela.Erro("Quantidade de iterações inválida.");
                return null;
            }

            // Tempo físico representado por cada iteração
            // (Δt em segundos). Não tem relação com o tempo real
            // de execução: a velocidade da animação é controlada
            // separadamente no visualizador.
            //
            // Com as massas geradas (1e12 a 1e13 kg) em uma área
            // de 100 km, as acelerações gravitacionais são da
            // ordem de 1e-7 m/s². Um Δt de 100 s permite observar
            // a atração entre os corpos em poucas centenas de
            // iterações.
            double tempoEntreIteracoes = 100; // segundos

            // Define o tamanho da região onde os corpos serão
            // distribuídos inicialmente. Unidade: metros.
            double tamanhoArea = 100_000;

            var universo = new Universo();
            universo.QuantidadeIteracoes = quantidadeIteracoes.Value;
            universo.TempoEntreIteracoes = tempoEntreIteracoes;

            // Gera os corpos aleatoriamente usando regras definidas em GeradorCorpos
            try
            {
                GeradorCorpos gerador = new GeradorCorpos();

                foreach (Corpo corpo in gerador.Gerar(quantidadeCorpos.Value, tamanhoArea))
                {
                    universo.AdicionarCorpo(corpo);
                }
            }
            catch (InvalidOperationException erro)
            {
                // Área pequena demais para a quantidade pedida.
                Tela.Erro(erro.Message);
                return null;
            }

            return universo;
        }

        // Opção 3: cenário de teste controlado com dois corpos
        // configurados para colidir. Serve para validar o tratamento
        // de colisões e fusões do simulador.
        private static Universo CriarCenarioColisao()
        {
            // Passos curtos (Δt = 0,1 s) deixam o movimento contínuo na
            // animação: a cada iteração cada corpo anda 10 m, menos de 2%
            // do próprio raio (~620 m). A colisão acontece perto da
            // iteração 38; as demais mostram o corpo resultante.
            var universo = new Universo();
            universo.QuantidadeIteracoes = 100;
            universo.TempoEntreIteracoes = 0.1;

            // Dois corpos com mesmas massas e densidades, movendo-se
            // em direções opostas ao longo do eixo X.
            Corpo corpoA = new Corpo("Teste-A", 1e12, 1000, -1000, 0, 100, 0);
            Corpo corpoB = new Corpo("Teste-B", 1e12, 1000, 1000, 0, -100, 0);

            universo.AdicionarCorpo(corpoA);
            universo.AdicionarCorpo(corpoB);

            Tela.Secao("TESTE DE COLISÃO");
            Tela.Info("Dois corpos iguais movendo-se um em direção ao outro no eixo X.");
            Console.WriteLine();
            Tela.Campo($"{corpoA.Nome}", $"X = {corpoA.PosX:F0} m   VX = {corpoA.VelX:F0} m/s   raio = {corpoA.Raio:F0} m");
            Tela.Campo($"{corpoB.Nome}", $"X = {corpoB.PosX:F0} m   VX = {corpoB.VelX:F0} m/s   raio = {corpoB.Raio:F0} m");

            return universo;
        }

        // ================================================================
        // SIMULAÇÃO COM GRAVAÇÃO NO BANCO
        // ================================================================

        private static void ExecutarSimulacao(
            Universo universo,
            string descricao,
            IRepositorioSimulacao? repositorio)
        {
            Tela.Secao("CONFIGURAÇÃO");
            Tela.Campo("Corpos", universo.QuantidadeCorpos.ToString());
            Tela.Campo("Iterações", universo.QuantidadeIteracoes.ToString());
            Tela.Campo("Tempo entre iterações (Δt)", $"{universo.TempoEntreIteracoes} s");
            Tela.Campo("Modo de processamento", universo.Modo.ToString());

            GravadorSimulacao? gravador = null;

            if (repositorio != null)
            {
                try
                {
                    gravador = new GravadorSimulacao(repositorio, descricao, universo);
                    Tela.Campo("Gravação no banco", $"simulação nº {gravador.SimulacaoId}");
                }
                catch (Exception erro)
                {
                    Tela.Erro("Não foi possível registrar a simulação no banco.");
                    Tela.Info($"Motivo: {erro.Message}");
                    Tela.Aviso("A simulação vai rodar sem gravação.");
                }
            }
            else
            {
                Tela.Aviso("Sem conexão com o MySQL: esta simulação não será gravada.");
            }

            // "using" garante que a thread de gravação seja encerrada
            // mesmo se ocorrer uma exceção no meio da simulação.
            using (gravador)
            {
                Console.WriteLine();
                bool abrirVisual = LerSimNao("Abrir a visualização 2D? (s/n): ");

                // Mede o tempo total (física + tela + espera do banco),
                // para comparar com o tempo só de física.
                var cronometroTotal = Stopwatch.StartNew();
                TimeSpan tempoFisica;

                try
                {
                    if (abrirVisual)
                    {
                        Tela.Info("Visualização aberta. Feche a janela para voltar ao console.");
                        var fonte = new FonteSimulacaoAoVivo(universo, gravador);
                        new Visualizador2D(fonte).Run();

                        // A janela é só uma forma de ACOMPANHAR a simulação.
                        // Se ela for fechada antes do fim, as iterações que
                        // faltam são calculadas aqui, para que a simulação
                        // gravada no banco fique sempre completa.
                        ConcluirIteracoesRestantes(fonte);

                        tempoFisica = fonte.TempoProcessamento;
                    }
                    else
                    {
                        var simulador = new Simulador(universo, gravador);
                        simulador.Executar();
                        tempoFisica = simulador.TempoProcessamento;
                    }

                    if (gravador != null)
                    {
                        Tela.Info("Concluindo a gravação no banco...");
                    }

                    // Espera a fila de gravação esvaziar.
                    gravador?.Finalizar((long)tempoFisica.TotalMilliseconds);
                }
                catch (Exception erro)
                {
                    Console.WriteLine();
                    Tela.Erro("A simulação foi interrompida por um erro.");
                    Tela.Info($"Motivo: {erro.Message}");
                    return;
                }

                cronometroTotal.Stop();

                Tela.Secao("SIMULAÇÃO FINALIZADA");
                Tela.Campo("Tempo de física", $"{tempoFisica.TotalMilliseconds:F1} ms");

                if (gravador != null)
                {
                    Tela.Campo("Tempo de gravação no banco", $"{gravador.TempoGravacao.TotalMilliseconds:F1} ms (em paralelo)");
                    Tela.Campo("Tempo total", $"{cronometroTotal.Elapsed.TotalMilliseconds:F1} ms");
                    Console.WriteLine();
                    Tela.Sucesso(
                        $"Simulação gravada com id {gravador.SimulacaoId} " +
                        $"({gravador.UltimaIteracaoRegistrada} iterações + estado inicial).");
                }
            }
        }

        private static void ConcluirIteracoesRestantes(FonteSimulacaoAoVivo fonte)
        {
            int total = fonte.TotalQuadrosPrevistos - 1;
            int faltando = fonte.TotalQuadrosPrevistos - fonte.QuadrosDisponiveis;

            if (faltando <= 0)
            {
                return;
            }

            Tela.Info($"Janela fechada antes do fim: calculando as {faltando} iterações restantes...");

            int ultimoPercentual = -1;

            while (fonte.GerarProximo())
            {
                int percentual = (fonte.QuadrosDisponiveis - 1) * 100 / total;

                // Atualiza a mesma linha ("\r") só quando o número muda.
                if (percentual != ultimoPercentual)
                {
                    Tela.Escrever(ConsoleColor.Cyan, $"\r  {percentual,3}% ({fonte.QuadrosDisponiveis - 1}/{total})");
                    ultimoPercentual = percentual;
                }
            }

            Console.WriteLine();
        }

        // ================================================================
        // REPLAY
        // ================================================================

        private static void ExecutarReplay(IRepositorioSimulacao repositorio)
        {
            List<ResumoSimulacao> simulacoes;

            try
            {
                simulacoes = repositorio.ListarSimulacoes();
            }
            catch (Exception erro)
            {
                Tela.Erro($"Erro ao listar simulações: {erro.Message}");
                return;
            }

            Tela.Secao("SIMULAÇÕES GRAVADAS");

            if (simulacoes.Count == 0)
            {
                Tela.Aviso("Nenhuma simulação gravada ainda. Use a opção 1 ou 3 primeiro.");
                return;
            }

            Tela.Escrever(ConsoleColor.DarkGray,
                $"  {"Id",4}  {"Data",-19}  {"Descrição",-26}  {"Corpos",6}  " +
                $"{"Iterações",9}  {"Modo",-10}  {"Física",9}\n");

            foreach (ResumoSimulacao s in simulacoes)
            {
                string tempo = s.TempoProcessamentoMs.HasValue ? $"{s.TempoProcessamentoMs} ms" : "-";
                string descricao = s.Descricao.Length > 26 ? s.Descricao[..26] : s.Descricao;

                Tela.Escrever(ConsoleColor.Cyan, $"  {s.Id,4}  ");
                Tela.Escrever(ConsoleColor.Gray,
                    $"{s.DataInicio:dd/MM/yyyy HH:mm:ss}  {descricao,-26}  " +
                    $"{s.QuantidadeCorposInicial,6}  ");

                // Simulações interrompidas (gravadas antes desta correção)
                // aparecem em amarelo, com "gravadas/configuradas".
                bool incompleta = s.IteracoesGravadas < s.QuantidadeIteracoes;
                Tela.Escrever(
                    incompleta ? ConsoleColor.Yellow : ConsoleColor.Gray,
                    (incompleta ? $"{s.IteracoesGravadas}/{s.QuantidadeIteracoes}" : s.IteracoesGravadas.ToString()).PadLeft(9) + "  ");
                Tela.Escrever(
                    s.ModoProcessamento == nameof(ModoProcessamento.Paralelo) ? ConsoleColor.Green : ConsoleColor.Yellow,
                    $"{s.ModoProcessamento,-10}");
                Tela.Escrever(ConsoleColor.Gray, $"  {tempo,9}\n");
            }

            Console.WriteLine();
            int? id = LerInteiroPositivo("Id da simulação para o replay: ");

            ResumoSimulacao? escolhida = simulacoes.Find(s => s.Id == id);

            if (escolhida == null)
            {
                Tela.Erro("Simulação não encontrada.");
                return;
            }

            List<QuadroSimulacao> quadros;
            var cronometroCarga = Stopwatch.StartNew();

            try
            {
                Tela.Info("Carregando do banco...");
                quadros = repositorio.CarregarQuadros(escolhida.Id);
            }
            catch (Exception erro)
            {
                Tela.Erro($"Erro ao carregar a simulação: {erro.Message}");
                return;
            }

            cronometroCarga.Stop();

            if (quadros.Count == 0)
            {
                Tela.Aviso("Esta simulação não possui estados gravados.");
                return;
            }

            Tela.Sucesso(
                $"{quadros.Count} quadros carregados em " +
                $"{cronometroCarga.Elapsed.TotalMilliseconds:F0} ms.");
            Console.WriteLine();

            bool abrirVisual = LerSimNao("Ver o replay na visualização 2D? (s/n): ");

            if (abrirVisual)
            {
                Tela.Info("Replay aberto. Feche a janela para voltar ao console.");

                var fonte = new FonteReplay(
                    quadros,
                    $"Replay nº {escolhida.Id} · {escolhida.Descricao}",
                    escolhida.TempoEntreIteracoes,
                    escolhida.QuantidadeIteracoes
                );

                new Visualizador2D(fonte).Run();
            }
            else
            {
                ExibirReplayConsole(quadros);
            }
        }

        // Replay em texto: posição de cada corpo em cada iteração,
        // exatamente como foi gravada no banco.
        private static void ExibirReplayConsole(List<QuadroSimulacao> quadros)
        {
            foreach (QuadroSimulacao quadro in quadros)
            {
                Tela.Secao($"ITERAÇÃO {quadro.Iteracao}  ·  {quadro.Corpos.Count} corpos");

                foreach (EstadoCorpo corpo in quadro.Corpos)
                {
                    Tela.Escrever(ConsoleColor.Cyan, $"  {corpo.Nome,-14}");
                    Tela.Escrever(ConsoleColor.Gray,
                        $"X: {corpo.PosX,14:E6} m   " +
                        $"Y: {corpo.PosY,14:E6} m   " +
                        $"VX: {corpo.VelX,14:E6} m/s   " +
                        $"VY: {corpo.VelY,14:E6} m/s\n");
                }
            }

            Console.WriteLine();
            Tela.Sucesso("Fim do replay.");
        }

        // ================================================================
        // LEITURA DE DADOS DO USUÁRIO
        // ================================================================

        private static ModoProcessamento LerModoProcessamento()
        {
            Console.WriteLine();
            Tela.Info("Modo de processamento:");
            Tela.OpcaoMenu("1", "Paralelo (N2)", "padrão - usa todos os núcleos");
            Tela.OpcaoMenu("2", "Sequencial (N1)", "um núcleo, algoritmo original");

            string? resposta = Tela.Perguntar("Escolha [1]: ");

            return resposta?.Trim() == "2"
                ? ModoProcessamento.Sequencial
                : ModoProcessamento.Paralelo;
        }

        // Retorna null se o valor não for um inteiro maior que zero.
        private static int? LerInteiroPositivo(string mensagem)
        {
            string? entrada = Tela.Perguntar(mensagem);

            if (int.TryParse(entrada, out int valor) && valor > 0)
            {
                return valor;
            }

            return null;
        }

        // Lê resposta obrigatória sim/não (s/n).
        private static bool LerSimNao(string mensagem)
        {
            while (true)
            {
                string? resp = Tela.Perguntar(mensagem);

                if (string.IsNullOrWhiteSpace(resp))
                {
                    Tela.Aviso("Digite 's' para sim ou 'n' para não.");
                    continue;
                }

                resp = resp.Trim().ToLowerInvariant();

                if (resp == "s" || resp == "y")
                    return true;

                if (resp == "n")
                    return false;

                Tela.Aviso("Digite 's' para sim ou 'n' para não.");
            }
        }
    }
}
