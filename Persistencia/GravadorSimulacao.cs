using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HologramaGravitacional
{
    // Responsável por levar os estados da simulação até o banco
    // SEM fazer a simulação esperar pelo banco.
    //
    // Padrão Produtor-Consumidor:
    //
    //   [thread da simulação]  --Registrar()-->  [fila]  -->  [thread de gravação]  -->  MySQL
    //        (produtor)                                           (consumidor)
    //
    // Enquanto a thread de gravação está esperando o MySQL responder
    // (operação de E/S, lenta), a simulação já está calculando as
    // próximas iterações. As duas coisas acontecem ao mesmo tempo.
    // Gravando de forma síncrona, cada iteração teria de esperar
    // o banco terminar antes de continuar.
    public sealed class GravadorSimulacao : IDisposable
    {
        // Quantas iterações são juntadas antes de ir ao banco.
        private const int QuadrosPorLote = 20;

        // Limite da fila. Se o banco for mais lento que a simulação,
        // a fila enche e Registrar() passa a esperar (backpressure),
        // evitando que a memória cresça sem limite.
        private const int CapacidadeFila = 200;

        private readonly IRepositorioSimulacao repositorio;

        // BlockingCollection é uma fila segura para várias threads:
        // Add() e a leitura (GetConsumingEnumerable) já fazem o
        // controle de concorrência internamente.
        private readonly BlockingCollection<QuadroSimulacao> fila =
            new BlockingCollection<QuadroSimulacao>(CapacidadeFila);

        private readonly Task tarefaGravacao;

        // Mede só o tempo em que a thread de gravação esteve
        // efetivamente falando com o banco.
        private readonly Stopwatch cronometroGravacao = new Stopwatch();

        // Erro ocorrido na thread de gravação. É repassado para a
        // thread principal, porque exceções em outra thread não
        // aparecem sozinhas para quem chamou.
        private volatile Exception? erroGravacao;

        private bool finalizado;

        public int SimulacaoId { get; }

        // Maior iteração já enviada para gravação.
        public int UltimaIteracaoRegistrada { get; private set; } = -1;

        public TimeSpan TempoGravacao => cronometroGravacao.Elapsed;

        public GravadorSimulacao(
            IRepositorioSimulacao repositorio,
            string descricao,
            Universo universo)
        {
            this.repositorio = repositorio;

            SimulacaoId = repositorio.CriarSimulacao(descricao, universo);

            // LongRunning: indica que a tarefa vai durar a simulação inteira,
            // então o .NET cria uma thread dedicada em vez de ocupar uma
            // thread do pool (que o Parallel.For usa para a física).
            tarefaGravacao = Task.Factory.StartNew(
                ConsumirFila,
                TaskCreationOptions.LongRunning
            );
        }

        // Chamado pela simulação após cada iteração (e uma vez com
        // iteração 0, para gravar o estado inicial).
        public void Registrar(int iteracao, Universo universo)
        {
            // A cópia dos estados é feita AQUI, na thread da simulação,
            // antes de a próxima iteração alterar os corpos.
            Registrar(new QuadroSimulacao(iteracao, universo.ObterEstados()));
        }

        // Versão que recebe um quadro já copiado (usada pelo visualizador,
        // que precisa da mesma cópia para desenhar).
        public void Registrar(QuadroSimulacao quadro)
        {
            VerificarErro();

            int iteracao = quadro.Iteracao;

            try
            {
                fila.Add(quadro);
            }
            catch (InvalidOperationException)
            {
                // A fila foi fechada porque a gravação falhou.
                VerificarErro();
                throw;
            }

            UltimaIteracaoRegistrada = iteracao;
        }

        // Código executado pela thread de gravação (consumidor).
        private void ConsumirFila()
        {
            var lote = new List<QuadroSimulacao>(QuadrosPorLote);

            try
            {
                // GetConsumingEnumerable bloqueia esperando novos itens
                // e só termina depois de CompleteAdding() com a fila vazia.
                foreach (QuadroSimulacao quadro in fila.GetConsumingEnumerable())
                {
                    lote.Add(quadro);

                    if (lote.Count >= QuadrosPorLote)
                    {
                        GravarLote(lote);
                    }
                }

                // Sobra final (menos que um lote completo).
                if (lote.Count > 0)
                {
                    GravarLote(lote);
                }
            }
            catch (Exception erro)
            {
                erroGravacao = erro;

                // Fecha a fila para que a simulação pare de esperar
                // por um consumidor que não existe mais.
                fila.CompleteAdding();
            }
        }

        private void GravarLote(List<QuadroSimulacao> lote)
        {
            cronometroGravacao.Start();
            repositorio.GravarQuadros(SimulacaoId, lote);
            cronometroGravacao.Stop();

            lote.Clear();
        }

        // Espera a fila esvaziar e registra os dados finais da simulação.
        public void Finalizar(long tempoProcessamentoMs)
        {
            if (finalizado)
            {
                return;
            }

            finalizado = true;

            // Avisa o consumidor que não virão mais itens...
            if (!fila.IsAddingCompleted)
            {
                fila.CompleteAdding();
            }

            // ...e espera ele gravar o que ainda está na fila.
            tarefaGravacao.Wait();

            VerificarErro();

            repositorio.FinalizarSimulacao(
                SimulacaoId,
                Math.Max(0, UltimaIteracaoRegistrada),
                tempoProcessamentoMs
            );
        }

        private void VerificarErro()
        {
            if (erroGravacao != null)
            {
                throw new InvalidOperationException(
                    $"Falha ao gravar a simulação no banco: {erroGravacao.Message}",
                    erroGravacao
                );
            }
        }

        public void Dispose()
        {
            // Garante que a thread de gravação termine mesmo se
            // Finalizar() não foi chamado (por exemplo, após um erro).
            if (!fila.IsAddingCompleted)
            {
                fila.CompleteAdding();
            }

            try
            {
                tarefaGravacao.Wait();
            }
            catch (AggregateException)
            {
                // Erros já foram tratados em ConsumirFila.
            }

            fila.Dispose();
        }
    }
}
