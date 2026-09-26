using HologramaGravitacional;
using System.Collections.Generic;

namespace HologramaGravitacional
{
    internal class Program
    {
        static void Main(string[] args)
        {
// ============================================================
// CONFIGURAÇÃO INICIAL
// ============================================================

        string caminhoArquivo = "universo.txt";

            PersistenciaAbstrata persistencia =
                new PersistenciaTexto();

            Universo universo;

            // ============================================================
            // ESCOLHA DO USUÁRIO
            // ============================================================

            Console.WriteLine("==============================================");
            Console.WriteLine("          HOLOGRAMA GRAVITACIONAL");
            Console.WriteLine("==============================================");

            Console.WriteLine();
            Console.WriteLine("1 - Criar novo Universo");
            Console.WriteLine("2 - Carregar Universo existente");
            Console.WriteLine("3 - Executar cenário de teste de colisão");

            Console.WriteLine();
            Console.Write("Escolha uma opção: ");

            string? opcao = Console.ReadLine();

            // ============================================================
            // CRIAÇÃO DE UM NOVO UNIVERSO
            // ============================================================

            if (opcao == "1")
            {
                Console.WriteLine();
                Console.Write(
                "Digite a quantidade de corpos que deseja gerar: "
                );

                string? entradaQuantidade =
                Console.ReadLine();

                if (!int.TryParse(
                entradaQuantidade,
                out int quantidadeCorpos
                ) || quantidadeCorpos <= 0)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                    "Quantidade de corpos inválida."
                    );

                return;

                    }

                Console.Write(
                "Digite a quantidade de iterações: "
                );

                string? entradaIteracoes =
                Console.ReadLine();

                if (!int.TryParse(
                entradaIteracoes,
                out int quantidadeIteracoes
                ) || quantidadeIteracoes <= 0)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                    "Quantidade de iterações inválida."
                    );

                return;


                }


                // Tempo físico representado por cada iteração.
                // Unidade: segundos.
                double tempoEntreIteracoes = 0.05;

                // Define o tamanho da região onde os corpos
                // serão distribuídos inicialmente.
                // Unidade: metros.
                double tamanhoArea = 1_000_000;

                universo = new Universo();

                universo.QuantidadeIteracoes =
                    quantidadeIteracoes;

                universo.TempoEntreIteracoes =
                    tempoEntreIteracoes;

                // ========================================================
                // GERAÇÃO DOS CORPOS
                // ========================================================

                GeradorCorpos gerador =
                    new GeradorCorpos();

                List<Corpo> corpos =
                    gerador.Gerar(
                        quantidadeCorpos,
                        tamanhoArea
                    );

                foreach (Corpo corpo in corpos)
                {
                    universo.AdicionarCorpo(corpo);
                }

                // Salva a configuração inicial do Universo.
                persistencia.Salvar(
                    universo,
                    caminhoArquivo
                );

                Console.WriteLine();
                Console.WriteLine(
                    $"Universo salvo em: {caminhoArquivo}"
                );
            }

            // ============================================================
            // CARREGAMENTO DE UM UNIVERSO EXISTENTE
            // ============================================================

            else if (opcao == "2")
            {
                // Verifica se o arquivo existe antes de tentar
                // carregar os dados do Universo.
                if (!File.Exists(caminhoArquivo))
                {
                    Console.WriteLine();
                    Console.WriteLine(
                    $"O arquivo '{caminhoArquivo}' não foi encontrado."
                    );

             Console.WriteLine("Crie um novo Universo antes de tentar carregá-lo.");

                    return;
                }

                try
                {
                    universo = persistencia.Carregar(
                        caminhoArquivo
                    );

                    Console.WriteLine();
                    Console.WriteLine(
                        "Universo carregado com sucesso."
                    );

                    Console.WriteLine(
                        $"Quantidade de corpos: " +
                        $"{universo.QuantidadeCorpos}"
                    );

                    Console.WriteLine(
                        $"Quantidade de iterações configuradas: " +
                        $"{universo.QuantidadeIteracoes}"
                    );

                    Console.WriteLine();

                    Console.Write(
                        "Digite a quantidade de novas iterações: "
                    );

                    string? entrada =
                        Console.ReadLine();

                    if (int.TryParse(
                        entrada,
                        out int novasIteracoes
                    ) && novasIteracoes > 0)
                    {
                        universo.QuantidadeIteracoes =
                            novasIteracoes;
                    }
                    else
                    {
                        Console.WriteLine(
                            "Quantidade de iterações inválida."
                        );

                        return;
                    }
                }
                catch (Exception erro)
                {
                    // Caso o arquivo exista, mas possua dados inválidos
                    // ou não possa ser lido, o erro é informado ao usuário.
                    Console.WriteLine();
                    Console.WriteLine(
                        "Não foi possível carregar o Universo."
                    );

                    Console.WriteLine(
                        $"Motivo: {erro.Message}"
                    );

                    return;
                }

                }
            else if (opcao == "3")
            {
                Console.WriteLine();
                Console.WriteLine("==============================================");
                Console.WriteLine("      CENÁRIO DE TESTE DE COLISÃO");
                Console.WriteLine("==============================================");

                universo = new Universo();

                // Define uma quantidade suficiente de iterações
                // para permitir a aproximação dos corpos.
                universo.QuantidadeIteracoes = 20;

                // Cada iteração representa 1 segundo.
                universo.TempoEntreIteracoes = 1.0;

                // Os corpos possuem a mesma massa e densidade para
                // facilitar a análise do comportamento da colisão.
                Corpo corpoA = new Corpo(
                    "Teste-A",
                    1e12,
                    1000,
                    -1000,
                    0,
                    100,
                    0
                );

                Corpo corpoB = new Corpo(
                    "Teste-B",
                    1e12,
                    1000,
                    1000,
                    0,
                    -100,
                    0
                );

                universo.AdicionarCorpo(corpoA);
                universo.AdicionarCorpo(corpoB);

                Console.WriteLine();
                Console.WriteLine("Cenário criado com dois corpos.");
                Console.WriteLine(
                    "Os corpos estão se movendo um em direção ao outro."
                );

                Console.WriteLine(
                    $"Raio do corpo A: {corpoA.Raio:E3} m"
                );

                Console.WriteLine(
                    $"Raio do corpo B: {corpoB.Raio:E3} m"
                );

                Console.WriteLine();
                Console.WriteLine("Estado inicial:");

                Console.WriteLine(
                    $"{corpoA.Nome} | X: {corpoA.PosX:F2} m | " +
                    $"VX: {corpoA.VelX:F2} m/s"
                );

                Console.WriteLine(
                    $"{corpoB.Nome} | X: {corpoB.PosX:F2} m | " +
                    $"VX: {corpoB.VelX:F2} m/s"
                );
            }




            // ============================================================
            // OPÇÃO INVÁLIDA
            // ============================================================

            else
            {
                Console.WriteLine();
                Console.WriteLine("Opção inválida.");

                return;
            }



            // ============================================================
            // INFORMAÇÕES DO UNIVERSO
            // ============================================================

            Console.WriteLine();

            Console.WriteLine(
                $"Quantidade de corpos: " +
                $"{universo.QuantidadeCorpos}"
            );

            Console.WriteLine(
                $"Quantidade de iterações: " +
                $"{universo.QuantidadeIteracoes}"
            );

            Console.WriteLine(
                $"Tempo entre iterações: " +
                $"{universo.TempoEntreIteracoes} s"
            );

            // ============================================================
            // EXECUÇÃO DA SIMULAÇÃO
            // ============================================================

            Simulador simulador =
                new Simulador(universo);

            simulador.Executar();

            // ============================================================
            // SALVAMENTO DO ESTADO ATUAL
            // ============================================================

            // Após a execução, as posições e velocidades dos corpos
            // podem ter sido alteradas pela interação gravitacional.
            // Salvamos novamente o Universo para que esse estado possa
            // ser carregado posteriormente e a simulação continue.
            persistencia.Salvar(
                universo,
                caminhoArquivo
            );

            Console.WriteLine();

            Console.WriteLine(
                $"Estado atual do Universo salvo em: {caminhoArquivo}"
            );

            // ============================================================
            // FINALIZAÇÃO
            // ============================================================

            Console.WriteLine();
            Console.WriteLine("==============================================");
            Console.WriteLine("        SIMULAÇÃO FINALIZADA");
            Console.WriteLine("==============================================");
        }
    }

}

