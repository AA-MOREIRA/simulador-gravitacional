# 🌌 Simulador Gravitacional 2D — Holograma Gravitacional

Simulador de interação gravitacional entre corpos em duas dimensões, desenvolvido em **C# (.NET)** para a disciplina **Programação Avançada de Sistemas** (Prof. Anker D. Lóss).

Os corpos são distribuídos aleatoriamente, atraem-se mutuamente segundo a **Lei da Gravitação Universal** e colidem conservando a **quantidade de movimento**. Na versão **N2**, todas as iterações são gravadas em **MySQL**, qualquer simulação pode ser reproduzida (**replay**) e o processamento usa **computação paralela**.

![C#](https://img.shields.io/badge/C%23-.NET%209-512BD4?logo=dotnet&logoColor=white)
![MySQL](https://img.shields.io/badge/Banco-MySQL-4479A1?logo=mysql&logoColor=white)
![Interface](https://img.shields.io/badge/Interface-Console%20%2B%20Windows%20Forms-lightgrey)
![Unidades](https://img.shields.io/badge/Unidades-SI-blue)

## 📑 Sumário

- [Funcionalidades](#-funcionalidades)
- [Tecnologias](#️-tecnologias)
- [Como executar](#️-como-executar)
- [Menu](#-menu)
- [Estrutura do projeto](#-estrutura-do-projeto)
- [Modelo físico](#️-modelo-físico)
- [Computação paralela](#-computação-paralela)
- [Banco de dados](#-banco-de-dados)
- [Autor](#-autor)

## ✨ Funcionalidades

- Distribuição aleatória de N corpos no plano 2D, sem sobreposição inicial
- Força gravitacional entre todos os pares de corpos
- Colisões com fusão (inelástica), conservando massa e quantidade de movimento
- Detecção **contínua** de colisão: os corpos se fundem no instante exato do contato
- Acompanhamento da conservação de momento linear, centro de massa e energia
- **N2:** gravação de todos os dados de cada corpo, em cada iteração, no **MySQL**
- **N2:** **replay** de qualquer simulação gravada, com linha do tempo e rebobinar
- **N2:** processamento **paralelo** (`Parallel.For`) e gravação no banco em thread separada
- **N2:** comparativo de performance sequencial (N1) × paralelo (N2), exportado em CSV
- Visualização 2D com zoom, rastros, legenda de densidade e detalhes de cada corpo
- Todas as grandezas no **Sistema Internacional (SI)**

## 🛠️ Tecnologias

- **C#** / **.NET 9** (console + Windows Forms)
- **MySQL 8+** com o driver [MySqlConnector](https://mysqlconnector.net/)
- `System.Threading.Tasks.Parallel` e `BlockingCollection` (produtor-consumidor)

## ▶️ Como executar

**Pré-requisitos:**
- Windows com **Visual Studio 2022 ou superior** (carga *Desenvolvimento para desktop com .NET*)
- **MySQL Server 8** ou superior em execução

**Passos:**

1. Clone o repositório e abra `HologramaGravitacional.sln` no Visual Studio.
2. Compile (**Ctrl+Shift+B**). O pacote `MySqlConnector` é baixado automaticamente.
3. Execute (**F5**).
4. O programa tenta conectar com `root`/`root` em `localhost:3306`. Se não conseguir, ele **pede servidor, porta, usuário e senha**.
   - A conexão que funcionar fica salva em `%APPDATA%\HologramaGravitacional\conexao.txt`, fora do projeto, então a senha nunca vai para o GitHub.
   - Para trocar de servidor depois, basta apagar esse arquivo.
5. O banco `holograma_gravitacional` e as tabelas são **criados automaticamente** na primeira conexão (script equivalente em [`Banco/schema.sql`](Banco/schema.sql)).

> Para medir performance, use a configuração **Release** e execute com **Ctrl+F5** (sem depurador).

## 📋 Menu

| Opção | Descrição |
|---|---|
| 1 | Cria um universo aleatório, simula e grava no banco |
| 2 | Lista as simulações gravadas e faz o replay da escolhida |
| 3 | Cenário de teste com dois corpos em rota de colisão |
| 4 | Comparativo de performance (gera `comparativo_performance.csv`) |
| 5 | Sair |

**Atalhos da visualização 2D:** `Espaço` reproduzir/pausar · `←` `→` passo · `B` rebobinar · `Home`/`End` início/fim · roda do mouse zoom · arrastar move · `T` rastros · `N` nomes.

## 🧱 Estrutura do projeto

```
Holograma_Gravitacional/
├── HologramaGravitacional.sln
├── Program.cs                      # Menu, simulação, replay e conexão com o banco
├── Corpo.cs                        # Entidade: massa, densidade, posição, velocidade, raio
├── EstadoCorpo.cs                  # Cópia imutável de um corpo em uma iteração
├── Universo.cs                     # Iterações (sequencial/paralela) e colisões
├── CalculosFisicos.cs              # Força, energia, momento e fusão
├── GeradorCorpos.cs                # Criação aleatória de corpos
├── Simulador.cs                    # Execução no console com análise de conservação
├── ComparadorPerformance.cs        # Medição sequencial × paralelo
├── Tela.cs                         # Cores e formatação do console
├── Visualizador2D.cs               # Janela gráfica com linha do tempo
├── Visualizacao/
│   └── FontesQuadros.cs            # Origem dos quadros: simulação ao vivo ou replay
├── Persistencia/
│   ├── IRepositorioSimulacao.cs    # Contrato de armazenamento
│   ├── RepositorioMySql.cs         # Implementação com MySQL
│   ├── GravadorSimulacao.cs        # Gravação assíncrona (produtor-consumidor)
│   └── ConfiguracaoBanco.cs        # Conexão com o MySQL de cada máquina
└── Banco/
    └── schema.sql                  # Script de criação do banco
```

## ⚛️ Modelo físico

### Raio

O raio não é armazenado: é calculado a partir da massa e da densidade, tratando o corpo como uma esfera:

$$r = \sqrt[3]{\frac{3m}{4\pi\rho}}$$

### Gravitação universal

$$F = G \, \frac{m_1 m_2}{d^2}$$

com $G = 6{,}674 \times 10^{-11}\ \text{N·m}^2/\text{kg}^2$. A força resultante sobre cada corpo é a soma vetorial das atrações de todos os outros.

### Atualização do movimento

A cada iteração, a aceleração ($a = F / m$) atualiza a velocidade e, em seguida, a posição de todos os corpos.

### Colisões

Durante um passo, cada corpo anda em linha reta. O simulador calcula o **instante exato** em que a distância entre os centros fica igual à soma dos raios, resolvendo:

$$(\vec v \cdot \vec v)\,s^2 - 2(\vec d \cdot \vec v)\,s + (\vec d \cdot \vec d - R^2) = 0$$

Os corpos são posicionados encostados nesse instante e fundidos na iteração seguinte. Isso também evita que corpos rápidos se atravessem sem colidir (*tunelamento*). Na fusão, a massa e a quantidade de movimento são conservadas:

$$m_1 \vec{v}_1 + m_2 \vec{v}_2 = (m_1 + m_2)\,\vec{v}'$$

## 🚀 Computação paralela

- **Forças:** o cálculo é O(n²) e concentra quase todo o custo. A força sobre cada corpo depende só das posições e massas dos outros, então cada corpo é calculado em paralelo, sem `lock`.
- **Colisões:** a busca pelo primeiro par em colisão também é paralela, e encontra exatamente o mesmo par que a versão sequencial.
- **Energia potencial:** soma paralela com resultados parciais por thread (redução).
- **Gravação:** a simulação coloca cada iteração numa fila e uma thread separada grava no MySQL em lotes. A física não espera o banco.

O modo **sequencial** (algoritmo da N1) foi mantido para o comparativo. As duas versões produzem resultados idênticos.

## 💾 Banco de dados

| Tabela | Conteúdo |
|---|---|
| `simulacao` | Uma linha por execução: descrição, data, corpos, iterações, Δt, modo de processamento e tempo de processamento |
| `estado_corpo` | Nome, massa, densidade, posição e velocidade de **cada corpo em cada iteração** (iteração 0 = estado inicial), com chave estrangeira para `simulacao` |

O acesso ao banco é feito pela interface `IRepositorioSimulacao`. Trocar de banco exige apenas uma nova implementação dela.

## 👤 Autor

**Allan Augusto Moreira de Oliveira**
Estudante de Análise e Desenvolvimento de Sistemas — UCL

[GitHub](https://github.com/AA-MOREIRA) · [LinkedIn](https://www.linkedin.com/in/allan-moreir4)
