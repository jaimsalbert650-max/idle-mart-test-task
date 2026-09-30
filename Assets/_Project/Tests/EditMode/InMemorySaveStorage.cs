using IdleMart.Save;

namespace IdleMart.Tests
{
    /// <summary>Save storage kept in memory for tests.</summary>
    public sealed class InMemorySaveStorage : ISaveStorage
    {
        public string Content;
        public bool Exists => Content != null;
        public string Read() => Content;
        public void Write(string content) => Content = content;
        public void Delete() => Content = null;
    }
}
