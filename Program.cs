using System;
using System.Collections.Generic;
using System.IO;

namespace HologramaGravitacional
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Programa principal: gerencia menu, criação/carregamento
            // do Universo e invocação do simulador ou visualizador.
            // O arquivo "universo.txt" é usado por padrão para
            // salvar e carregar o estado da simulação.
            string caminhoArquivo = "universo.txt";

            PersistenciaAbstrata persistencia = new PersistenciaTexto();

            while (true)
            {
                // Loop principal do programa. Mantém o menu ativo até que
                // o usuário escolha sair (opção 4) ou confirme que não
                // deseja retornar ao menu após uma simulação.

                Console.WriteLine("==============================================");
                Console.WriteLine("          HOLOGRAMA GRAVITACIONAL");
                Console.WriteLine("==============================================");
                Console.WriteLine();
                Console.WriteLine("1 - Criar novo Universo");
                Console.WriteLine("2 - Carregar Universo existente");
                Console.WriteLine("3 - Executar cenário de teste de colisão");
                Console.WriteLine("4 - Sair");
                Console.WriteLine();
                Console.Write("Escolha uma opção: ");

                string? opcao = Console.ReadLine();
                Universo? universo = null;

                // Opção 1: criar um novo Universo aleatório.
                // O usuário define quantos corpos e quantas iterações
                // a simulação deve executar. A configuração inicial é
                // salva imediatamente em arquivo para permitir recarregamento
                // posterior.
                if (opcao == "1")
                {
                    Console.WriteLine();
                    Console.Write("Digite a quantidade de corpos que deseja gerar: ");
                    string? entradaQuantidade = Console.ReadLine();

                    if (!int.TryParse(entradaQuantidade, out int quantidadeCorpos) || quantidadeCorpos <= 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Quantidade de corpos inválida.");
                        continue;
                    }

                    Console.Write("Digite a quantidade de iterações: ");
                    string? entradaIteracoes = Console.ReadLine();

                    if (!int.TryParse(entradaIteracoes, out int quantidadeIteracoes) || quantidadeIteracoes <= 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Quantidade de iterações inválida.");
                        continue;
                    }

                    // Tempo físico representado por cada iteração
                    // (Δt em segundos). Esse valor afeta a evolução
                    // numérica do sistema e a aparência da animação.
                    double tempoEntreIteracoes = 0.05; // seconds
                    // Define o tamanho da região onde os corpos serão
                    // distribuídos inicialmente.
                    // Unidade: metros.
                    // Usar uma área menor por padrão para que as
                    // interações gravitacionais fiquem mais fortes
                    // e visíveis no visualizador.
                    double tamanhoArea = 100_000; // meters

                    universo = new Universo();
                    universo.QuantidadeIteracoes = quantidadeIteracoes;
                    universo.TempoEntreIteracoes = tempoEntreIteracoes;

                    // Gera os corpos aleatoriamente usando regras definidas em GeradorCorpos
                    GeradorCorpos gerador = new GeradorCorpos();
                    List<Corpo> corpos = gerador.Gerar(quantidadeCorpos, tamanhoArea);

                    foreach (Corpo corpo in corpos)
                        universo.AdicionarCorpo(corpo);

                    // Persiste a configuração inicial em texto (formato especificado)
                    persistencia.Salvar(universo, caminhoArquivo);
                    Console.WriteLine();
                    Console.WriteLine($"Universo salvo em: {caminhoArquivo}");
                }
                // Opção 2: carregar um Universo previamente salvo em arquivo.
                // Validações são feitas no arquivo e o usuário pode ajustar
                // a quantidade de iterações antes de executar.
                else if (opcao == "2")
                {
                    if (!File.Exists(caminhoArquivo))
                    {
                        Console.WriteLine();
                        Console.WriteLine($"O arquivo '{caminhoArquivo}' não foi encontrado.");
                        Console.WriteLine("Crie um novo Universo antes de tentar carregá-lo.");
                        continue;
                    }

                    try
                    {
                        // Tenta reconstruir o Universo lendo o arquivo texto.
                        // A persistência usa InvariantCulture para números,
                        // então o arquivo é portátil entre ambientes.
                        universo = persistencia.Carregar(caminhoArquivo);

                        Console.WriteLine();
                        Console.WriteLine("Universo carregado com sucesso.");
                        Console.WriteLine($"Quantidade de corpos: {universo.QuantidadeCorpos}");
                        Console.WriteLine($"Quantidade de iterações configuradas: {universo.QuantidadeIteracoes}");

                        // Permite ao usuário informar um novo número de iterações
                        // para continuar a simulação a partir do estado carregado.
                        Console.Write("Digite a quantidade de novas iterações: ");
                        string? entrada = Console.ReadLine();

                        if (int.TryParse(entrada, out int novasIteracoes) && novasIteracoes > 0)
                        {
                            universo.QuantidadeIteracoes = novasIteracoes;
                        }
                        else
                        {
                            Console.WriteLine("Quantidade de iterações inválida.");
                            continue;
                        }
                    }
                    catch (Exception erro)
                    {
                        // Em caso de erro na leitura ou formato inválido do arquivo,
                        // informamos o motivo e retornamos ao menu sem travar o programa.
                        Console.WriteLine();
                        Console.WriteLine("Não foi possível carregar o Universo.");
                        Console.WriteLine($"Motivo: {erro.Message}");
                        continue;
                    }
                }
                else if (opcao == "3")
                {
                    // Opção 3: cria um cenário de teste controlado com dois corpos
                    // configurados para colidir. Serve para validar o tratamento
                    // de colisões e fusões do simulador.
                    universo = new Universo();
                    universo.QuantidadeIteracoes = 20;
                    universo.TempoEntreIteracoes = 1.0;

                    // Dois corpos com mesmas massas e densidades, movendo-se
                    // em direções opostas ao longo do eixo X.
                    Corpo corpoA = new Corpo("Teste-A", 1e12, 1000, -1000, 0, 100, 0);
                    Corpo corpoB = new Corpo("Teste-B", 1e12, 1000, 1000, 0, -100, 0);

                    universo.AdicionarCorpo(corpoA);
                    universo.AdicionarCorpo(corpoB);

                    Console.WriteLine();
                    Console.WriteLine("Cenário criado com dois corpos.");
                    Console.WriteLine("Os corpos estão se movendo um em direção ao outro.");
                    Console.WriteLine($"Raio do corpo A: {corpoA.Raio:E3} m");
                    Console.WriteLine($"Raio do corpo B: {corpoB.Raio:E3} m");
                    Console.WriteLine();
                    Console.WriteLine("Estado inicial:");
                    Console.WriteLine($"{corpoA.Nome} | X: {corpoA.PosX:F2} m | VX: {corpoA.VelX:F2} m/s");
                    Console.WriteLine($"{corpoB.Nome} | X: {corpoB.PosX:F2} m | VX: {corpoB.VelX:F2} m/s");
                }
                else if (opcao == "4")
                {
                    Console.WriteLine();
                    Console.WriteLine("Saindo...");
                    break;
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Opção inválida.");
                    continue;
                }

                // Se por algum motivo não foi criado ou carregado um Universo
                // válido (por exemplo, erro de entrada), voltamos ao menu.
                if (universo == null)
                    continue;

                // Informações do universo
                Console.WriteLine();
                Console.WriteLine($"Quantidade de corpos: {universo.QuantidadeCorpos}");
                Console.WriteLine($"Quantidade de iterações: {universo.QuantidadeIteracoes}");
                Console.WriteLine($"Tempo entre iterações: {universo.TempoEntreIteracoes} s");
                Simulador simulador = new Simulador(universo);

                // Função local para ler resposta obrigatória sim/não (s/n)
                static bool LerSimNao(string mensagem)
                {
                    while (true)
                    {
                        Console.Write(mensagem);
                        string? resp = Console.ReadLine();

                        if (string.IsNullOrWhiteSpace(resp))
                        {
                            Console.WriteLine("Resposta inválida. Digite 's' para sim ou 'n' para não.");
                            continue;
                        }

                        resp = resp.Trim().ToLowerInvariant();

                        if (resp == "s" || resp == "y")
                            return true;

                        if (resp == "n")
                            return false;

                        Console.WriteLine("Resposta inválida. Digite 's' para sim ou 'n' para não.");
                    }
                }

                Console.WriteLine();
                bool abrirVisual = LerSimNao("Deseja abrir a visualização 2D do Universo? (s/n): ");

                if (abrirVisual)
                {
                    try
                    {
                        var visual = new Visualizador2D(universo);
                        visual.Run();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Não foi possível abrir a visualização 2D.");
                        Console.WriteLine($"Motivo: {ex.Message}");
                        Console.WriteLine("Executando simulação no modo console...");
                        simulador.Executar();
                    }
                }
                else
                {
                    simulador.Executar();
                }

                persistencia.Salvar(universo, caminhoArquivo);
                Console.WriteLine();
                Console.WriteLine($"Estado atual do Universo salvo em: {caminhoArquivo}");

                Console.WriteLine();
                Console.WriteLine("==============================================");
                Console.WriteLine("        SIMULAÇÃO FINALIZADA");
                Console.WriteLine("==============================================");

                Console.WriteLine();
                bool voltarMenu = LerSimNao("Deseja voltar ao menu principal? (s/n): ");
                if (!voltarMenu)
                {
                    break;
                }
            }
        }
    }
}
