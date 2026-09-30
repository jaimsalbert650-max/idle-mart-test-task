using System.IO;

namespace IdleMart.Save
{
    /// <summary>
    /// Stores the save in a file. Writes go to a temp file first and then replace the
    /// real one, so a crash during saving never leaves a half-written save.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _path;

        public FileSaveStorage(string path)
        {
            _path = path;
        }

        public bool Exists => File.Exists(_path);

        public string Read() => File.ReadAllText(_path);

        public void Write(string content)
        {
            var temp = _path + ".tmp";
            File.WriteAllText(temp, content);

            if (File.Exists(_path)) File.Replace(temp, _path, null);
            else File.Move(temp, _path);
        }

        public void Delete()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        public void Backup()
        {
            if (File.Exists(_path)) File.Copy(_path, _path + ".bak", overwrite: true);
        }
    }
}
