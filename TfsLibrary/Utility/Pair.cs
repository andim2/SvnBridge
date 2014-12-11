using System.Collections.Generic;

namespace CodePlex.TfsLibrary
{
    public class Pair<TLeft, TRight>
    {
        readonly TLeft left;
        readonly TRight right;

        public Pair()
        {
            left = default(TLeft);
            right = default(TRight);
        }

        public Pair(TLeft left,
                    TRight right)
        {
            this.left = left;
            this.right = right;
        }

        public Pair(KeyValuePair<TLeft, TRight> kvp)
        {
            left = kvp.Key;
            right = kvp.Value;
        }

        public TLeft Left
        {
            get { return left; }
        }

        public TRight Right
        {
            get { return right; }
        }
    }
}