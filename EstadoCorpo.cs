using System;
using System.Collections.Generic;

namespace HologramaGravitacional
{
    // Estado de um corpo em um instante (uma iteração) da simulação.
    //
    // É um "record" imutável: depois de criado, nenhum valor muda.
    // Isso é o que permite entregar o estado para outra thread
    // (a de gravação no banco) sem precisar de lock: ninguém vai
    // alterar os valores enquanto ela lê.
    //
    // Também é o formato usado no replay, já que no replay não existe
    // física sendo calculada, apenas posições lidas do banco.
    public sealed record EstadoCorpo(
        string Nome,
        double Massa,
        double Densidade,
        double PosX,
        double PosY,
        double VelX,
        double VelY)
    {
        // Mesma fórmula de Corpo.Raio: r = ³√(3m / 4πρ).
        // Necessário para desenhar o corpo no replay com o tamanho real.
        public double Raio => Math.Cbrt((3 * Massa) / (4 * Math.PI * Densidade));

        // Copia os valores atuais de um Corpo.
        public static EstadoCorpo De(Corpo corpo)
        {
            return new EstadoCorpo(
                corpo.Nome,
                corpo.Massa,
                corpo.Densidade,
                corpo.PosX,
                corpo.PosY,
                corpo.VelX,
                corpo.VelY
            );
        }
    }

    // Um "quadro" da simulação: todos os corpos em uma iteração.
    // A iteração 0 é o estado inicial, antes de qualquer cálculo.
    public sealed record QuadroSimulacao(
        int Iteracao,
        IReadOnlyList<EstadoCorpo> Corpos);
}
