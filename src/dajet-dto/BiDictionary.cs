namespace DaJet
{
    public sealed class BiDictionary<T1, T2>
    {
        private readonly Dictionary<T1, T2> LeftToRight = new();
        private readonly Dictionary<T2, T1> RightToLeft = new();

        public void Add(T1 left, T2 right)
        {
            if (LeftToRight.ContainsKey(left)) throw new ArgumentException("left");
            if (RightToLeft.ContainsKey(right)) throw new ArgumentException("right");
            LeftToRight.Add(left, right);
            RightToLeft.Add(right, left);
        }

        public T2 this[T1 left]
        {
            get { return Get(left); }
            private set { }
        }

        public T1 this[T2 right]
        {
            get { return Get(right); }
            private set { }
        }

        public T2 Get(T1 left)
        {
            return LeftToRight[left];
        }

        public T1 Get(T2 right)
        {
            return RightToLeft[right];
        }

        public bool TryGet(T1 left, out T2 right)
        {
            return LeftToRight.TryGetValue(left, out right);
        }

        public bool TryGet(T2 right, out T1 left)
        {
            return RightToLeft.TryGetValue(right, out left);
        }

        public int Count()
        {
            return Math.Min(LeftToRight.Count, RightToLeft.Count);
        }

        public void Clear()
        {
            LeftToRight.Clear();
            RightToLeft.Clear();
        }
    }
}