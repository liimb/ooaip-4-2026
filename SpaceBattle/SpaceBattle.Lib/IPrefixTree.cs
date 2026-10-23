public interface IPrefixTree<TKey, TValue>
{
    void Add(IEnumerable<TKey> sequence, TValue value);
    (bool Found, TValue? Value) TryGet(IEnumerable<TKey> sequence);
}
