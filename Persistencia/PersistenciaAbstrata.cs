using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HologramaGravitacional
{
    public abstract class PersistenciaAbstrata
    {
        // Abstração para persistência do estado do Universo.
        // Ao definir uma classe abstrata, o simulador fica desacoplado
        // do mecanismo de armazenamento (arquivo, banco de dados, etc.)
        // facilitando futuras extensões.
        public abstract void Salvar(
            Universo universo,
            string caminhoArquivo
        );

        public abstract Universo Carregar(
            string caminhoArquivo
        );
    }
}
