using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Piksel.Nemesis.Security
{
    public class NoKey: KeyEncryptionBase
    {
        private static NoKey _default;
        public static NoKey Default
        {
            get
            {
                if (_default == null)
                    _default = new NoKey();
                return _default;
            }
        }

        public override KeyEncryptionType Type
        {
            get
            {
                return KeyEncryptionType.None;
            }
        }
    }
}
