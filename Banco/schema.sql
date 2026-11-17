-- ============================================================
-- Holograma Gravitacional - N2
-- Script de criação do banco de dados MySQL
-- ============================================================
--
-- O programa executa estes mesmos comandos automaticamente ao
-- iniciar (RepositorioMySql.PrepararBanco). Este arquivo existe
-- para documentação e para criar o banco manualmente, se preferir
-- (por exemplo, pelo MySQL Workbench).

CREATE DATABASE IF NOT EXISTS holograma_gravitacional
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE holograma_gravitacional;

-- ------------------------------------------------------------
-- Uma linha para cada execução do simulador.
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS simulacao (
    id                         INT          NOT NULL AUTO_INCREMENT,
    descricao                  VARCHAR(200) NOT NULL,
    data_inicio                DATETIME     NOT NULL,
    data_fim                   DATETIME     NULL,
    quantidade_corpos_inicial  INT          NOT NULL,
    quantidade_iteracoes       INT          NOT NULL,  -- iterações configuradas
    iteracoes_gravadas         INT          NOT NULL DEFAULT 0,  -- efetivamente executadas
    tempo_entre_iteracoes      DOUBLE       NOT NULL,  -- Δt físico, em segundos
    modo_processamento         VARCHAR(20)  NOT NULL,  -- 'Sequencial' ou 'Paralelo'
    tempo_processamento_ms     BIGINT       NULL,      -- tempo gasto só com a física
    PRIMARY KEY (id)
) ENGINE = InnoDB;

-- ------------------------------------------------------------
-- Estado completo de cada corpo em cada iteração.
-- Iteração 0 = estado inicial.
--
-- Relação 1:N com simulacao. ON DELETE CASCADE apaga os estados
-- junto quando uma simulação é excluída.
--
-- O nome é TEXT porque corpos fundidos recebem nomes compostos
-- ("Corpo-1-Corpo-7-Corpo-3...") que podem crescer bastante.
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS estado_corpo (
    id             BIGINT  NOT NULL AUTO_INCREMENT,
    simulacao_id   INT     NOT NULL,
    iteracao       INT     NOT NULL,
    nome           TEXT    NOT NULL,
    massa          DOUBLE  NOT NULL,  -- kg
    densidade      DOUBLE  NOT NULL,  -- kg/m³
    pos_x          DOUBLE  NOT NULL,  -- m
    pos_y          DOUBLE  NOT NULL,  -- m
    vel_x          DOUBLE  NOT NULL,  -- m/s
    vel_y          DOUBLE  NOT NULL,  -- m/s
    PRIMARY KEY (id),
    CONSTRAINT fk_estado_corpo_simulacao
        FOREIGN KEY (simulacao_id) REFERENCES simulacao (id)
        ON DELETE CASCADE,
    -- O replay sempre busca "todos os estados da simulação X,
    -- em ordem de iteração"; este índice atende exatamente essa consulta.
    INDEX idx_estado_simulacao_iteracao (simulacao_id, iteracao)
) ENGINE = InnoDB;

-- ------------------------------------------------------------
-- Consultas úteis
-- ------------------------------------------------------------
-- Listar simulações:
--   SELECT * FROM simulacao ORDER BY id DESC;
--
-- Posição dos corpos de uma simulação em uma iteração:
--   SELECT nome, pos_x, pos_y FROM estado_corpo
--   WHERE simulacao_id = 1 AND iteracao = 10;
--
-- Quantidade de corpos em cada iteração (mostra as fusões):
--   SELECT iteracao, COUNT(*) AS corpos FROM estado_corpo
--   WHERE simulacao_id = 1 GROUP BY iteracao ORDER BY iteracao;
