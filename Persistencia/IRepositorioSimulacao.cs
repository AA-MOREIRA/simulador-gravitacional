using System;
using System.Collections.Generic;

namespace HologramaGravitacional
{
    // Contrato de armazenamento das simulações.
    //
    // O restante do programa (Program, GravadorSimulacao) conhece apenas
    // esta interface, não o MySQL. Se no futuro quisermos outro banco
    // (SQL Server, PostgreSQL, SQLite), basta criar outra classe que
    // implemente esta interface, sem mexer no simulador.
    // É a mesma ideia da PersistenciaAbstrata da N1, agora adaptada para
    // gravar TODAS as iterações e não apenas um estado.
    public interface IRepositorioSimulacao
    {
        // Cria as tabelas, se ainda não existirem.
        void PrepararBanco();

        // Registra uma nova simulação e devolve o id gerado pelo banco.
        int CriarSimulacao(string descricao, Universo universo);

        // Grava um lote de quadros (iterações) de uma simulação.
        void GravarQuadros(int simulacaoId, IReadOnlyList<QuadroSimulacao> quadros);

        // Marca a simulação como concluída e guarda os dados de desempenho.
        void FinalizarSimulacao(int simulacaoId, int iteracoesGravadas, long tempoProcessamentoMs);

        List<ResumoSimulacao> ListarSimulacoes();

        // Carrega todos os quadros de uma simulação, em ordem de iteração.
        List<QuadroSimulacao> CarregarQuadros(int simulacaoId);
    }

    // Dados de uma simulação exibidos na listagem do replay.
    public sealed record ResumoSimulacao(
        int Id,
        string Descricao,
        DateTime DataInicio,
        int QuantidadeCorposInicial,
        int QuantidadeIteracoes,
        int IteracoesGravadas,
        double TempoEntreIteracoes,
        string ModoProcessamento,
        long? TempoProcessamentoMs);
}
