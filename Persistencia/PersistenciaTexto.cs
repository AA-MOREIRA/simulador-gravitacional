using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Globalization;

namespace HologramaGravitacional
{
    public class PersistenciaTexto : PersistenciaAbstrata
    {
        public override void Salvar(
            Universo universo,
            string caminhoArquivo)
        {
            using StreamWriter arquivo =
                new StreamWriter(caminhoArquivo);

            // Primeira linha contém as informações gerais
            // necessárias para reconstruir a simulação.
            arquivo.WriteLine(
                $"{universo.QuantidadeCorpos};" +
                $"{universo.QuantidadeIteracoes};" +
                $"{universo.TempoEntreIteracoes.ToString(CultureInfo.InvariantCulture)}"
            );

            // Cada linha seguinte representa um corpo.
            foreach (Corpo corpo in universo.Corpos)
            {
                arquivo.WriteLine(
                    $"{corpo.Nome};" +
                    $"{corpo.Massa.ToString(CultureInfo.InvariantCulture)};" +
                    $"{corpo.Densidade.ToString(CultureInfo.InvariantCulture)};" +
                    $"{corpo.PosX.ToString(CultureInfo.InvariantCulture)};" +
                    $"{corpo.PosY.ToString(CultureInfo.InvariantCulture)};" +
                    $"{corpo.VelX.ToString(CultureInfo.InvariantCulture)};" +
                    $"{corpo.VelY.ToString(CultureInfo.InvariantCulture)}"
                );
            }
        }

        public override Universo Carregar(
    string caminhoArquivo)
        {
            using StreamReader arquivo =
                new StreamReader(caminhoArquivo);

            // A primeira linha contém as configurações da simulação.
            string? primeiraLinha = arquivo.ReadLine();

            if (string.IsNullOrWhiteSpace(primeiraLinha))
            {
                throw new InvalidDataException(
                    "O arquivo não possui uma configuração válida."
                );
            }

            string[] configuracao =
                primeiraLinha.Split(';');

            if (configuracao.Length != 3)
            {
                throw new InvalidDataException(
                    "A primeira linha do arquivo está inválida."
                );
            }

            int quantidadeCorpos =
                int.Parse(configuracao[0]);

            int quantidadeIteracoes =
                int.Parse(configuracao[1]);

            double tempoEntreIteracoes =
                double.Parse(
                    configuracao[2],
                    CultureInfo.InvariantCulture
                );

            Universo universo = new Universo();

            universo.QuantidadeIteracoes =
                quantidadeIteracoes;

            universo.TempoEntreIteracoes =
                tempoEntreIteracoes;

            // Cada linha restante representa um corpo.
            string? linha;

            while ((linha = arquivo.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(linha))
                {
                    continue;
                }

                string[] dados = linha.Split(';');

                if (dados.Length != 7)
                {
                    throw new InvalidDataException(
                        "Uma das linhas de corpo está inválida."
                    );
                }

                string nome = dados[0];

                double massa =
                    double.Parse(
                        dados[1],
                        CultureInfo.InvariantCulture
                    );

                double densidade =
                    double.Parse(
                        dados[2],
                        CultureInfo.InvariantCulture
                    );

                double posX =
                    double.Parse(
                        dados[3],
                        CultureInfo.InvariantCulture
                    );

                double posY =
                    double.Parse(
                        dados[4],
                        CultureInfo.InvariantCulture
                    );

                double velX =
                    double.Parse(
                        dados[5],
                        CultureInfo.InvariantCulture
                    );

                double velY =
                    double.Parse(
                        dados[6],
                        CultureInfo.InvariantCulture
                    );

                Corpo corpo = new Corpo(
                    nome,
                    massa,
                    densidade,
                    posX,
                    posY,
                    velX,
                    velY
                );

                universo.AdicionarCorpo(corpo);
            }

            // Confere se a quantidade informada no arquivo
            // corresponde à quantidade realmente carregada.
            if (universo.QuantidadeCorpos != quantidadeCorpos)
            {
                throw new InvalidDataException(
                    "A quantidade de corpos do arquivo não corresponde " +
                    "à quantidade de corpos carregados."
                );
            }

            return universo;
        }
    }
}