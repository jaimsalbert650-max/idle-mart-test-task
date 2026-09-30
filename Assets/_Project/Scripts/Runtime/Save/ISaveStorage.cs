namespace IdleMart.Save
{
    /// <summary>Where the save text lives. File on device, memory in tests.</summary>
    public interface ISaveStorage
    {
        bool Exists { get; }
        string Read();
        void Write(string content);
        void Delete();

        /// <summary>Copies the current save aside (e.g. save.json.bak).</summary>
        void Backup();
    }
}
