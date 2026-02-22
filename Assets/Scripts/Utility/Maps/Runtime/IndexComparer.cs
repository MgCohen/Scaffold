using System.Collections.Generic;

namespace Scaffold.Maps
{
    public class IndexComparer : IEqualityComparer<IIndex>
    {
        public bool Equals(IIndex x, IIndex y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(IIndex obj)
        {
            return obj.GetHashCode();
        }
    }
}
