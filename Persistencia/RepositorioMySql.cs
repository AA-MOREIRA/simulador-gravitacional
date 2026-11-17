using System;
using System.Collections.Generic;
using System.Text;
using MySqlConnector;

namespace HologramaGravitacional
{
    // Implementação do repositório usando MySQL.
    // Biblioteca de acesso: MySqlConnector (pacote NuGet).
    public class RepositorioMySql : IRepositorioSimulacao
    {
        // Quantas linhas de estado_corpo vão em cada comando INSERT.
        //
        // Inserir linha por linha seria muito lento: cada INSERT é uma
        // ida e volta até o servidor. Com 100 corpos e 1000 iterações
        // seriam 100.000 comandos. Agrupando 1000 linhas por INSERT
        // ("INSERT ... VALUES (...), (...), (...)") fazemos 100 comandos.
        private const int LinhasPorInsert = 1000;

        private readonly string stringConexao;

        public RepositorioMySql(string stringConexao)
        {
            this.stringConexao = stringConexao;
        }

        private MySqlConnection AbrirConexao()
        {
            // O MySqlConnector mantém um pool de conexões: "abrir" uma
            // conexão normalmente reaproveita uma já aberta, o que é barato.
            var conexao = new MySqlConnection(stringConexao);
            conexao.Open();
            return conexao;
        }

        public void PrepararBanco()
        {
            // 1) Cria o banco de dados, caso não exista.
            //    Para isso conectamos ao servidor SEM selecionar banco,
            //    pois ele ainda pode não existir.
            var construtor = new MySqlConnectionStringBuilder(stringConexao);
            string nomeBanco = construtor.Database;

            if (string.IsNullOrWhiteSpace(nomeBanco))
            {
                throw new InvalidOperationException(
                    "A string de conexão precisa informar o banco (Database=...)."
                );
            }

            construtor.Database = "";

            using (var conexaoServidor = new MySqlConnection(construtor.ConnectionString))
            {
                conexaoServidor.Open();

                // O nome do banco vem da nossa própria configuração; as crases
                // (`) delimitam o identificador no MySQL.
                using var criarBanco = new MySqlCommand(
                    $"CREATE DATABASE IF NOT EXISTS `{nomeBanco}` " +
                    "CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;",
                    conexaoServidor
                );
                criarBanco.ExecuteNonQuery();
            }

            // 2) Cria as tabelas (mesmo conteúdo de Banco/schema.sql).
            using var conexao = AbrirConexao();

            const string sqlTabelas = @"
                CREATE TABLE IF NOT EXISTS simulacao (
                    id                         INT          NOT NULL AUTO_INCREMENT,
                    descricao                  VARCHAR(200) NOT NULL,
                    data_inicio                DATETIME     NOT NULL,
                    data_fim                   DATETIME     NULL,
                    quantidade_corpos_inicial  INT          NOT NULL,
                    quantidade_iteracoes       INT          NOT NULL,
                    iteracoes_gravadas         INT          NOT NULL DEFAULT 0,
                    tempo_entre_iteracoes      DOUBLE       NOT NULL,
                    modo_processamento         VARCHAR(20)  NOT NULL,
                    tempo_processamento_ms     BIGINT       NULL,
                    PRIMARY KEY (id)
                ) ENGINE = InnoDB;

                CREATE TABLE IF NOT EXISTS estado_corpo (
                    id             BIGINT  NOT NULL AUTO_INCREMENT,
                    simulacao_id   INT     NOT NULL,
                    iteracao       INT     NOT NULL,
                    nome           TEXT    NOT NULL,
                    massa          DOUBLE  NOT NULL,
                    densidade      DOUBLE  NOT NULL,
                    pos_x          DOUBLE  NOT NULL,
                    pos_y          DOUBLE  NOT NULL,
                    vel_x          DOUBLE  NOT NULL,
                    vel_y          DOUBLE  NOT NULL,
                    PRIMARY KEY (id),
                    CONSTRAINT fk_estado_corpo_simulacao
                        FOREIGN KEY (simulacao_id) REFERENCES simulacao (id)
                        ON DELETE CASCADE,
                    INDEX idx_estado_simulacao_iteracao (simulacao_id, iteracao)
                ) ENGINE = InnoDB;";

            using var comando = new MySqlCommand(sqlTabelas, conexao);
            comando.ExecuteNonQuery();
        }

        public int CriarSimulacao(string descricao, Universo universo)
        {
            using var conexao = AbrirConexao();

            // Parâmetros (@nome) em vez de concatenar valores no texto do SQL:
            // evita SQL Injection e problemas com vírgula/ponto em números.
            using var comando = new MySqlCommand(@"
                INSERT INTO simulacao
                    (descricao, data_inicio, quantidade_corpos_inicial,
                     quantidade_iteracoes, tempo_entre_iteracoes, modo_processamento)
                VALUES
                    (@descricao, @dataInicio, @corpos,
                     @iteracoes, @deltaTempo, @modo);",
                conexao);

            comando.Parameters.AddWithValue("@descricao", descricao);
            comando.Parameters.AddWithValue("@dataInicio", DateTime.Now);
            comando.Parameters.AddWithValue("@corpos", universo.QuantidadeCorpos);
            comando.Parameters.AddWithValue("@iteracoes", universo.QuantidadeIteracoes);
            comando.Parameters.AddWithValue("@deltaTempo", universo.TempoEntreIteracoes);
            comando.Parameters.AddWithValue("@modo", universo.Modo.ToString());

            comando.ExecuteNonQuery();

            // Id gerado pelo AUTO_INCREMENT.
            return (int)comando.LastInsertedId;
        }

        public void GravarQuadros(int simulacaoId, IReadOnlyList<QuadroSimulacao> quadros)
        {
            // Transforma os quadros em uma lista "achatada" de linhas
            // (iteração + estado de um corpo), que é o formato da tabela.
            var linhas = new List<(int Iteracao, EstadoCorpo Estado)>();

            foreach (QuadroSimulacao quadro in quadros)
            {
                foreach (EstadoCorpo estado in quadro.Corpos)
                {
                    linhas.Add((quadro.Iteracao, estado));
                }
            }

            if (linhas.Count == 0)
            {
                return;
            }

            using var conexao = AbrirConexao();

            // Transação: ou o lote inteiro é gravado, ou nada é.
            // Também é mais rápido, porque o MySQL só confirma
            // (grava em disco de forma definitiva) uma vez, no Commit.
            using var transacao = conexao.BeginTransaction();

            for (int inicio = 0; inicio < linhas.Count; inicio += LinhasPorInsert)
            {
                int fim = Math.Min(inicio + LinhasPorInsert, linhas.Count);

                using var comando = new MySqlCommand();
                comando.Connection = conexao;
                comando.Transaction = transacao;

                var sql = new StringBuilder(
                    "INSERT INTO estado_corpo " +
                    "(simulacao_id, iteracao, nome, massa, densidade, " +
                    "pos_x, pos_y, vel_x, vel_y) VALUES ");

                comando.Parameters.AddWithValue("@sim", simulacaoId);

                // Monta "(@sim, @it0, @n0, ...), (@sim, @it1, @n1, ...)".
                // Cada linha recebe parâmetros com sufixo próprio (k).
                for (int i = inicio; i < fim; i++)
                {
                    int k = i - inicio;
                    var (iteracao, estado) = linhas[i];

                    if (k > 0)
                    {
                        sql.Append(", ");
                    }

                    sql.Append(
                        $"(@sim, @it{k}, @n{k}, @m{k}, @d{k}, " +
                        $"@px{k}, @py{k}, @vx{k}, @vy{k})");

                    comando.Parameters.AddWithValue($"@it{k}", iteracao);
                    comando.Parameters.AddWithValue($"@n{k}", estado.Nome);
                    comando.Parameters.AddWithValue($"@m{k}", estado.Massa);
                    comando.Parameters.AddWithValue($"@d{k}", estado.Densidade);
                    comando.Parameters.AddWithValue($"@px{k}", estado.PosX);
                    comando.Parameters.AddWithValue($"@py{k}", estado.PosY);
                    comando.Parameters.AddWithValue($"@vx{k}", estado.VelX);
                    comando.Parameters.AddWithValue($"@vy{k}", estado.VelY);
                }

                comando.CommandText = sql.ToString();
                comando.ExecuteNonQuery();
            }

            transacao.Commit();
        }

        public void FinalizarSimulacao(int simulacaoId, int iteracoesGravadas, long tempoProcessamentoMs)
        {
            using var conexao = AbrirConexao();

            using var comando = new MySqlCommand(@"
                UPDATE simulacao
                SET data_fim = @dataFim,
                    iteracoes_gravadas = @iteracoes,
                    tempo_processamento_ms = @tempo
                WHERE id = @id;",
                conexao);

            comando.Parameters.AddWithValue("@dataFim", DateTime.Now);
            comando.Parameters.AddWithValue("@iteracoes", iteracoesGravadas);
            comando.Parameters.AddWithValue("@tempo", tempoProcessamentoMs);
            comando.Parameters.AddWithValue("@id", simulacaoId);

            comando.ExecuteNonQuery();
        }

        public List<ResumoSimulacao> ListarSimulacoes()
        {
            var simulacoes = new List<ResumoSimulacao>();

            using var conexao = AbrirConexao();

            using var comando = new MySqlCommand(@"
                SELECT id, descricao, data_inicio, quantidade_corpos_inicial,
                       quantidade_iteracoes, iteracoes_gravadas,
                       tempo_entre_iteracoes, modo_processamento,
                       tempo_processamento_ms
                FROM simulacao
                ORDER BY id;",
                conexao);

            using var leitor = comando.ExecuteReader();

            while (leitor.Read())
            {
                // Os índices seguem a ordem das colunas no SELECT acima.
                simulacoes.Add(new ResumoSimulacao(
                    Id: leitor.GetInt32(0),
                    Descricao: leitor.GetString(1),
                    DataInicio: leitor.GetDateTime(2),
                    QuantidadeCorposInicial: leitor.GetInt32(3),
                    QuantidadeIteracoes: leitor.GetInt32(4),
                    IteracoesGravadas: leitor.GetInt32(5),
                    TempoEntreIteracoes: leitor.GetDouble(6),
                    ModoProcessamento: leitor.GetString(7),
                    // A coluna aceita NULL (simulação interrompida antes de finalizar).
                    TempoProcessamentoMs: leitor.IsDBNull(8) ? null : leitor.GetInt64(8)
                ));
            }

            return simulacoes;
        }

        public List<QuadroSimulacao> CarregarQuadros(int simulacaoId)
        {
            var quadros = new List<QuadroSimulacao>();

            using var conexao = AbrirConexao();

            // Ordenar por iteração permite montar os quadros lendo
            // as linhas uma única vez, sem precisar de dicionário.
            // "id" como critério de desempate mantém a ordem original
            // dos corpos dentro da iteração.
            using var comando = new MySqlCommand(@"
                SELECT iteracao, nome, massa, densidade,
                       pos_x, pos_y, vel_x, vel_y
                FROM estado_corpo
                WHERE simulacao_id = @id
                ORDER BY iteracao, id;",
                conexao);

            comando.Parameters.AddWithValue("@id", simulacaoId);

            using var leitor = comando.ExecuteReader();

            int iteracaoAtual = -1;
            List<EstadoCorpo>? corposDaIteracao = null;

            while (leitor.Read())
            {
                int iteracao = leitor.GetInt32(0);

                // Mudou a iteração: fecha o quadro anterior e começa outro.
                if (iteracao != iteracaoAtual)
                {
                    if (corposDaIteracao != null)
                    {
                        quadros.Add(new QuadroSimulacao(iteracaoAtual, corposDaIteracao));
                    }

                    iteracaoAtual = iteracao;
                    corposDaIteracao = new List<EstadoCorpo>();
                }

                corposDaIteracao!.Add(new EstadoCorpo(
                    Nome: leitor.GetString(1),
                    Massa: leitor.GetDouble(2),
                    Densidade: leitor.GetDouble(3),
                    PosX: leitor.GetDouble(4),
                    PosY: leitor.GetDouble(5),
                    VelX: leitor.GetDouble(6),
                    VelY: leitor.GetDouble(7)
                ));
            }

            // Último quadro lido.
            if (corposDaIteracao != null)
            {
                quadros.Add(new QuadroSimulacao(iteracaoAtual, corposDaIteracao));
            }

            return quadros;
        }
    }
}
