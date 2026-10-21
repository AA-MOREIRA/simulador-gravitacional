# 🌌 Simulador Gravitacional 2D

Simulador de interação gravitacional entre corpos em duas dimensões, desenvolvido em **C# (.NET)** para a disciplina **Programação Avançada de Sistemas** (Prof. Anker D. Lóss).

Os corpos são distribuídos aleatoriamente, atraem-se mutuamente segundo a **Lei da Gravitação Universal** e colidem conservando a **quantidade de movimento**. O estado do universo pode ser salvo em arquivo texto e recarregado para continuar a simulação.

![C#](https://img.shields.io/badge/C%23-.NET-512BD4?logo=dotnet&logoColor=white)
![Console](https://img.shields.io/badge/Interface-Console-lightgrey)
![Unidades](https://img.shields.io/badge/Unidades-SI-blue)

## 📑 Sumário

- [Funcionalidades](#-funcionalidades)
- [Tecnologias](#-tecnologias)
- [Estrutura do projeto](#-estrutura-do-projeto)
- [Modelo de dados](#-modelo-de-dados)
- [Modelo físico](#-modelo-físico)
- [Persistência](#-persistência)
- [Como executar](#-como-executar)
- [Autor](#-autor)

## ✨ Funcionalidades

- Distribuição aleatória de N corpos no plano 2D
- Cálculo da força gravitacional entre todos os pares de corpos
- Atualização de posição e velocidade a cada iteração
- Detecção e tratamento de colisões com conservação da quantidade de movimento
- Exibição do estado de todos os corpos no console a cada iteração
- Gravação da configuração do universo em arquivo texto e recarga posterior
- Persistência baseada em **classe abstrata**, preparada para uma futura versão com banco de dados
- Todas as grandezas no **Sistema Internacional (SI)**

## 🛠️ Tecnologias

- **C#**
- **.NET** (aplicação de console)
- Armazenamento em arquivo texto (`.txt`)

## 🧱 Estrutura do projeto

```
SimuladorGravitacional/
├── SimuladorGravitacional.sln
├── src/
│   └── SimuladorGravitacional/
│       ├── Program.cs                      # Ponto de entrada e menu
│       ├── Modelos/
│       │   ├── Corpo.cs                    # Entidade: atributos e cálculo do raio
│       │   └── Universo.cs                 # Simulação: forças, movimento e colisões
│       └── Persistencia/
│           ├── RepositorioUniverso.cs      # Classe ABSTRATA de gravação/leitura
│           └── RepositorioArquivoTexto.cs  # Implementação concreta (arquivo .txt)
├── exemplos/
│   └── universo.txt                        # Exemplo de configuração
└── README.md
```

## 📐 Modelo de dados

### Classe `Corpo`

| Atributo | Tipo | Descrição | Unidade (SI) |
|----------|------|-----------|--------------|
| `Nome` | `string` | Identificação do corpo | - |
| `Massa` | `double` | Massa | kg |
| `Densidade` | `double` | Densidade média | kg/m³ |
| `PosX`, `PosY` | `double` | Posição no plano | m |
| `VelX`, `VelY` | `double` | Componentes da velocidade | m/s |

O **raio** não é armazenado: é uma propriedade calculada a partir da massa e da densidade, tratando o corpo como uma esfera:

```csharp
public double Raio => Math.Cbrt(3 * Massa / (4 * Math.PI * Densidade));
```

Ou seja:

$$r = \sqrt[3]{\frac{3m}{4\pi\rho}}$$

onde `m` é a massa e `ρ` é a densidade do corpo.

### Classe `Universo`

Reúne a lista de corpos e executa a simulação: calcula as forças entre os pares, atualiza posições e velocidades a cada iteração e trata as colisões.

## ⚛️ Modelo físico

### Gravitação universal

A força de atração entre dois corpos de massas $m_1$ e $m_2$, separados por uma distância $d$, é:

$$F = G \, \frac{m_1 m_2}{d^2}$$

com $G = 6{,}674 \times 10^{-11}\ \text{N·m}^2/\text{kg}^2$. A força resultante sobre cada corpo é a soma vetorial das atrações exercidas por todos os outros.

### Atualização do movimento

A cada iteração, a aceleração de cada corpo ($a = F / m$) é usada para atualizar a velocidade e, em seguida, a posição.

### Colisões

Duas esferas colidem quando a distância entre seus centros é menor ou igual à soma de seus raios. No tratamento da colisão, a **quantidade de movimento total do sistema é conservada**:

$$m_1 \vec{v}_1 + m_2 \vec{v}_2 = m_1 \vec{v}_1' + m_2 \vec{v}_2'$$

## 💾 Persistência

A gravação e a leitura do universo são feitas por uma **classe abstrata**, `RepositorioUniverso`, que define o contrato de salvar e carregar o estado da simulação. A classe `RepositorioArquivoTexto` é a implementação concreta, que grava e lê um arquivo `.txt`.

Essa separação permite trocar a forma de armazenamento sem alterar a simulação: uma futura versão com **banco de dados** exige apenas uma nova implementação da classe abstrata.

Um exemplo de configuração está em [`exemplos/universo.txt`](exemplos/universo.txt).

## ▶️ Como executar

**Pré-requisito:** [.NET SDK](https://dotnet.microsoft.com/download) instalado.

```bash
# Clonar o repositório
git clone https://github.com/AA-MOREIRA/SimuladorGravitacional.git
cd SimuladorGravitacional

# Executar
dotnet run --project src/SimuladorGravitacional
```

Para apenas compilar:

```bash
dotnet build
```

## 👤 Autor

**Allan Augusto Moreira de Oliveira**
Estudante de Análise e Desenvolvimento de Sistemas — UCL

[GitHub](https://github.com/AA-MOREIRA) · [LinkedIn](https://www.linkedin.com/in/allan-moreir4)
