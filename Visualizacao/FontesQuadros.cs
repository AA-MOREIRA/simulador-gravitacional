using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HologramaGravitacional
{
    // O visualizador 2D trabalha com uma sequência de "quadros"
    // (posições dos corpos em cada iteração). Ele não precisa saber
    // se esses quadros estão sendo calculados agora ou se vieram do banco.
    //
    // Esta interface separa as duas origens possíveis:
    //   - FonteSimulacaoAoVivo: calcula a física sob demanda e guarda
    //     cada quadro calculado, para permitir voltar no tempo;
    //   - FonteReplay: todos os quadros já foram lidos do MySQL.
    //
    // Como as duas fontes guardam os quadros em uma lista, o visualizador
    // pode avançar, voltar e pular para qualquer ponto da linha do tempo.
    public interface IFonteQuadros
    {
        string Titulo { get; }

        // Δt físico de cada iteração, usado para mostrar o tempo simulado.
        double DeltaTempo { get; }

        // Quantos quadros a simulação terá ao final (tamanho da linha do tempo).
        int TotalQuadrosPrevistos { get; }

        // Quantos quadros já existem (calculados ou carregados).
        int QuadrosDisponiveis { get; }

        // Quantas iterações a simulação foi CONFIGURADA para ter (+1 do
        // estado inicial). Define o ritmo da animação, para que a simulação
        // ao vivo e o seu replay andem na mesma velocidade.
        int QuadrosConfigurados { get; }

        QuadroSimulacao ObterQuadro(int indice);

        // Calcula mais um quadro. Retorna false se não houver mais
        // nada a calcular (simulação concluída ou replay).
        bool GerarProximo();
    }

    // Executa a simulação de verdade enquanto desenha.
    public class FonteSimulacaoAoVivo : IFonteQuadros
    {
        private readonly Universo universo;
        private readonly GravadorSimulacao? gravador;
        private readonly Stopwatch cronometroFisica = new Stopwatch();

        // Todos os quadros já calculados. A física só anda para frente;
        // voltar no tempo é apenas reexibir quadros desta lista.
        // Custo: memória proporcional a corpos × iterações.
        private readonly List<QuadroSimulacao> historico = new List<QuadroSimulacao>();

        public FonteSimulacaoAoVivo(Universo universo, GravadorSimulacao? gravador)
        {
            this.universo = universo;
            this.gravador = gravador;

            // Estado inicial (iteração 0).
            var inicial = new QuadroSimulacao(0, universo.ObterEstados());
            historico.Add(inicial);
            gravador?.Registrar(inicial);
        }

        public string Titulo => $"Simulação ({universo.Modo})";

        public double DeltaTempo => universo.TempoEntreIteracoes;

        public int TotalQuadrosPrevistos => universo.QuantidadeIteracoes + 1;

        public int QuadrosDisponiveis => historico.Count;

        public int QuadrosConfigurados => universo.QuantidadeIteracoes + 1;

        // Tempo gasto só com a física, para o resumo de desempenho.
        public TimeSpan TempoProcessamento => cronometroFisica.Elapsed;

        public QuadroSimulacao ObterQuadro(int indice) => historico[indice];

        public bool GerarProximo()
        {
            if (historico.Count >= TotalQuadrosPrevistos)
            {
                return false;
            }

            cronometroFisica.Start();
            universo.ExecutarIteracao(universo.TempoEntreIteracoes);
            cronometroFisica.Stop();

            // A mesma cópia imutável serve para desenhar e para gravar.
            var quadro = new QuadroSimulacao(historico.Count, universo.ObterEstados());
            historico.Add(quadro);
            gravador?.Registrar(quadro);

            return true;
        }
    }

    // Reproduz uma simulação gravada no banco. Nenhuma física é
    // calculada: as posições são exatamente as que foram gravadas.
    public class FonteReplay : IFonteQuadros
    {
        private readonly IReadOnlyList<QuadroSimulacao> quadros;

        public FonteReplay(
            IReadOnlyList<QuadroSimulacao> quadros,
            string titulo,
            double deltaTempo,
            int iteracoesConfiguradas)
        {
            if (quadros.Count == 0)
            {
                throw new ArgumentException("A simulação não possui quadros gravados.");
            }

            this.quadros = quadros;
            Titulo = titulo;
            DeltaTempo = deltaTempo;
            QuadrosConfigurados = Math.Max(iteracoesConfiguradas + 1, quadros.Count);
        }

        public string Titulo { get; }

        public double DeltaTempo { get; }

        public int TotalQuadrosPrevistos => quadros.Count;

        public int QuadrosDisponiveis => quadros.Count;

        public int QuadrosConfigurados { get; }

        public QuadroSimulacao ObterQuadro(int indice) => quadros[indice];

        // No replay tudo já foi carregado; não há o que calcular.
        public bool GerarProximo() => false;
    }
}
