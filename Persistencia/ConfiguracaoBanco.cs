using System;
using System.IO;
using System.Text;
using MySqlConnector;

namespace HologramaGravitacional
{
    // Decide qual string de conexão com o MySQL o programa vai usar.
    //
    // O projeto é distribuído pelo GitHub, e cada máquina tem o seu
    // próprio MySQL, com usuário e senha diferentes. Por isso a senha
    // NÃO fica fixa no código: ela é pedida na primeira execução e
    // salva em um arquivo local da máquina, fora da pasta do projeto
    // (portanto nunca vai para o repositório).
    //
    // Ordem de prioridade:
    //   1. variável de ambiente HOLOGRAMA_MYSQL (string de conexão completa);
    //   2. arquivo salvo em %APPDATA%\HologramaGravitacional\conexao.txt;
    //   3. padrão: localhost:3306, usuário root, senha root.
    public static class ConfiguracaoBanco
    {
        public const string NomeBanco = "holograma_gravitacional";

        private const string StringConexaoPadrao =
            "Server=localhost;Port=3306;Database=" + NomeBanco + ";" +
            "Uid=root;Pwd=root;";

        // %APPDATA% é uma pasta por usuário do Windows
        // (ex.: C:\Users\fulano\AppData\Roaming).
        private static readonly string CaminhoArquivo = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HologramaGravitacional",
            "conexao.txt"
        );

        public static string ObterStringConexao()
        {
            string? variavel =
                Environment.GetEnvironmentVariable("HOLOGRAMA_MYSQL");

            if (!string.IsNullOrWhiteSpace(variavel))
            {
                return variavel;
            }

            if (File.Exists(CaminhoArquivo))
            {
                string salva = File.ReadAllText(CaminhoArquivo).Trim();

                if (salva.Length > 0)
                {
                    return salva;
                }
            }

            return StringConexaoPadrao;
        }

        // Guarda a string de conexão que funcionou, para não perguntar de novo.
        public static void SalvarStringConexao(string stringConexao)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CaminhoArquivo)!);
            File.WriteAllText(CaminhoArquivo, stringConexao);
        }

        // Pergunta ao usuário os dados do servidor MySQL desta máquina.
        // Enter sem digitar nada aceita o valor padrão entre colchetes.
        public static string PerguntarStringConexao()
        {
            Console.WriteLine();
            Console.WriteLine("Informe os dados do seu servidor MySQL (Enter = valor padrão):");

            string servidor = LerComPadrao("Servidor", "localhost");
            string porta = LerComPadrao("Porta", "3306");
            string usuario = LerComPadrao("Usuário", "root");

            Console.Write("Senha: ");
            string senha = LerSenhaOculta();

            // O builder monta a string corretamente mesmo se a senha
            // tiver caracteres especiais como ";" ou "=".
            var construtor = new MySqlConnectionStringBuilder
            {
                Server = servidor,
                Port = uint.TryParse(porta, out uint p) ? p : 3306,
                UserID = usuario,
                Password = senha,
                Database = NomeBanco
            };

            return construtor.ConnectionString;
        }

        private static string LerComPadrao(string rotulo, string padrao)
        {
            Console.Write($"{rotulo} [{padrao}]: ");
            string? valor = Console.ReadLine();

            return string.IsNullOrWhiteSpace(valor) ? padrao : valor.Trim();
        }

        // Lê a senha sem mostrá-la na tela (exibe "*" no lugar).
        private static string LerSenhaOculta()
        {
            var senha = new StringBuilder();

            while (true)
            {
                ConsoleKeyInfo tecla = Console.ReadKey(intercept: true);

                if (tecla.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }

                if (tecla.Key == ConsoleKey.Backspace)
                {
                    if (senha.Length > 0)
                    {
                        senha.Length--;
                        Console.Write("\b \b");
                    }

                    continue;
                }

                if (!char.IsControl(tecla.KeyChar))
                {
                    senha.Append(tecla.KeyChar);
                    Console.Write('*');
                }
            }

            return senha.ToString();
        }
    }
}
