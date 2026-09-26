using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HologramaGravitacional
{
    public abstract class PersistenciaAbstrata
    {
        public abstract void Salvar(
            Universo universo,
            string caminhoArquivo
        );

        public abstract Universo Carregar(
            string caminhoArquivo
        );
    }
}
