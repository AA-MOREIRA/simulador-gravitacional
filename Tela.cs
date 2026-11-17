using System;
using System.Text;

namespace HologramaGravitacional
{
    // Funções de apresentação do console: cores, títulos e menu.
    //
    // Centralizar isso em uma classe evita espalhar mudanças de cor
    // (Console.ForegroundColor) pelo programa inteiro e garante que a
    // cor sempre volte ao normal depois de cada mensagem.
    public static class Tela
    {
        private const int Largura = 72;

        public static void Configurar()
        {
            // UTF-8 para exibir acentos e caracteres de moldura (═ ║ ╔).
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Title = "Holograma Gravitacional";
        }

        public static void Banner(string statusBanco)
        {
            LimparTela();

            Escrever(ConsoleColor.DarkCyan, "  ╔" + new string('═', Largura - 4) + "╗\n");
            LinhaMoldura("", ConsoleColor.White);
            LinhaMoldura("H O L O G R A M A    G R A V I T A C I O N A L", ConsoleColor.Cyan);
            LinhaMoldura("Simulador gravitacional de N corpos  ·  versão N2", ConsoleColor.Gray);
            LinhaMoldura("", ConsoleColor.White);
            Escrever(ConsoleColor.DarkCyan, "  ╚" + new string('═', Largura - 4) + "╝\n");

            Console.WriteLine();
            Escrever(ConsoleColor.DarkGray, "   Banco: ");
            Escrever(statusBanco.StartsWith("conectado") ? ConsoleColor.Green : ConsoleColor.Yellow, statusBanco);
            Escrever(ConsoleColor.DarkGray, "     Núcleos: ");
            Escrever(ConsoleColor.Cyan, Environment.ProcessorCount.ToString());
            Console.WriteLine();
            Console.WriteLine();
        }

        // Linha centralizada dentro da moldura.
        private static void LinhaMoldura(string texto, ConsoleColor cor)
        {
            int interno = Largura - 4;
            int esquerda = Math.Max(0, (interno - texto.Length) / 2);
            int direita = Math.Max(0, interno - texto.Length - esquerda);

            Escrever(ConsoleColor.DarkCyan, "  ║");
            Escrever(cor, new string(' ', esquerda) + texto + new string(' ', direita));
            Escrever(ConsoleColor.DarkCyan, "║\n");
        }

        public static void OpcaoMenu(string tecla, string titulo, string descricao)
        {
            Escrever(ConsoleColor.DarkCyan, "   [");
            Escrever(ConsoleColor.Cyan, tecla);
            Escrever(ConsoleColor.DarkCyan, "]  ");
            Escrever(ConsoleColor.White, titulo.PadRight(24));
            Escrever(ConsoleColor.DarkGray, descricao + "\n");
        }

        // Título de seção: "── TEXTO ─────────".
        public static void Secao(string titulo)
        {
            Console.WriteLine();
            string inicio = $"── {titulo} ";
            Escrever(ConsoleColor.Cyan, inicio);
            Escrever(ConsoleColor.DarkCyan, new string('─', Math.Max(4, Largura - inicio.Length)) + "\n");
        }

        public static void Sucesso(string mensagem) => Escrever(ConsoleColor.Green, $"  ✔ {mensagem}\n");

        public static void Erro(string mensagem) => Escrever(ConsoleColor.Red, $"  ✖ {mensagem}\n");

        public static void Aviso(string mensagem) => Escrever(ConsoleColor.Yellow, $"  ! {mensagem}\n");

        public static void Info(string mensagem) => Escrever(ConsoleColor.Gray, $"  {mensagem}\n");

        // Par "rótulo: valor" alinhado.
        public static void Campo(string rotulo, string valor)
        {
            Escrever(ConsoleColor.DarkGray, $"  {rotulo,-28}");
            Escrever(ConsoleColor.White, valor + "\n");
        }

        // Pergunta com destaque; devolve o que o usuário digitou.
        public static string? Perguntar(string pergunta)
        {
            Escrever(ConsoleColor.Cyan, "  › ");
            Escrever(ConsoleColor.White, pergunta);
            return Console.ReadLine();
        }

        public static void Pausar()
        {
            Console.WriteLine();
            Escrever(ConsoleColor.DarkGray, "  Pressione Enter para voltar ao menu...");
            Console.ReadLine();
        }

        // Escreve em uma cor e restaura a cor anterior.
        public static void Escrever(ConsoleColor cor, string texto)
        {
            ConsoleColor anterior = Console.ForegroundColor;
            Console.ForegroundColor = cor;
            Console.Write(texto);
            Console.ForegroundColor = anterior;
        }

        private static void LimparTela()
        {
            // Console.Clear falha quando a saída está redirecionada
            // (por exemplo, em alguns modos de execução do Visual Studio).
            try
            {
                Console.Clear();
            }
            catch (System.IO.IOException)
            {
            }
        }
    }
}
