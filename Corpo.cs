using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HologramaGravitacional
{
    public class Corpo
    {
        // Representa um corpo físico no simulador.
        // Cada instância possui propriedades físicas básicas
        // (massa, densidade, posição e velocidade) e métodos
        // para atualizar estado ao longo do tempo.
        //
        // Unidades: massa em kg, densidade em kg/m^3, posições em metros,
        // velocidades em m/s. O raio é calculado a partir de massa e densidade
        // assumindo forma esférica (apenas para cálculo de colisões).

        // Limite máximo de densidade utilizado pelo simulador.
        // O valor está em kg/m³, conforme o Sistema Internacional (SI).
        private const double DensidadeMaxima = 22590;

        public string Nome { get; private set; }

        public double Massa { get; private set; }

        public double Densidade { get; private set; }

        public double PosX { get; private set; }

        public double PosY { get; private set; }

        public double VelX { get; private set; }

        public double VelY { get; private set; }

        // O raio não é armazenado.
        // Ele é calculado a partir da massa e da densidade:
        //
        // V = m / ρ
        //
        // Para uma esfera:
        //
        // V = 4/3 × π × r³
        //
        // Portanto:
        //
        // r = ³√(3m / 4πρ)
        public double Raio
        {
            get
            {
                return Math.Cbrt(
                    (3 * Massa) /
                    (4 * Math.PI * Densidade)
                );
            }
        }

        public Corpo(
            string nome,
            double massa,
            double densidade,
            double posX,
            double posY,
            double velX,
            double velY)
        {
            // A massa precisa ser positiva para representar
            // um corpo físico válido.
            if (massa <= 0)
            {
                throw new ArgumentException(
                    "A massa deve ser maior que zero."
                );
            }

            // A densidade também precisa ser positiva,
            // pois valores negativos ou zero não são válidos.
            if (densidade <= 0)
            {
                throw new ArgumentException(
                    "A densidade deve ser maior que zero."
                );
            }

            // Impede que o corpo seja criado com uma densidade
            // acima do limite estabelecido para o simulador.
            if (densidade > DensidadeMaxima)
            {
                throw new ArgumentException(
                    "A densidade ultrapassa o limite definido " +
                    "para o simulador."
                );
            }

            Nome = nome;
            Massa = massa;
            Densidade = densidade;
            PosX = posX;
            PosY = posY;
            VelX = velX;
            VelY = velY;
        }

        public void AtualizarVelocidade(
            double aceleracaoX,
            double aceleracaoY,
            double deltaTempo)
        {
            // Utilizamos a relação:
            //
            // Δv = a × Δt
            //
            // A velocidade é atualizada separadamente
            // nos eixos X e Y.
            VelX += aceleracaoX * deltaTempo;
            VelY += aceleracaoY * deltaTempo;
        }

        public void AtualizarPosicao(double deltaTempo)
        {
            // A nova posição é determinada pela velocidade
            // atual do corpo durante o intervalo de tempo.
            PosX += VelX * deltaTempo;
            PosY += VelY * deltaTempo;
        }
    }
}