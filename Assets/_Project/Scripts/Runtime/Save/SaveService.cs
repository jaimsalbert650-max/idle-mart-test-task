using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleMart.Save
{
    /// <summary>Serialises <see cref="SaveData"/> and validates it on load.</summary>
    public sealed class SaveService
    {
        private readonly ISaveStorage _storage;

        public SaveService(ISaveStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public bool HasSave => _storage.Exists;

        public void Save(SaveData data)
        {
            data.version = SaveData.CurrentVersion;
            _storage.Write(JsonUtility.ToJson(data));
        }

        /// <summary>Loads the save, or returns null if there is none or it cannot be used.</summary>
        public SaveData Load()
        {
            if (!_storage.Exists) return null;

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(_storage.Read());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Save file is corrupt, starting a new game. {e.Message}");
                return null;
            }

            if (data == null || data.version < 1 || data.version > SaveData.CurrentVersion)
            {
                Debug.LogWarning($"Save version {data?.version} is not supported, starting a new game.");
                return null;
            }

            // Future migrations: if (data.version < 2) { ...; data.version = 2; }

            Sanitize(data);
            return data;
        }

        /// <summary>Clamps values a hand-edited or damaged file could break the game with.</summary>
        private static void Sanitize(SaveData data)
        {
            data.built ??= new List<BuiltObjectData>();
            data.unlockedZones ??= new List<string>();
            data.built.RemoveAll(b => b == null || string.IsNullOrEmpty(b.slotId) || string.IsNullOrEmpty(b.buildableId));
            data.money = Math.Max(0, data.money);
            data.xp = Math.Max(0, data.xp);
            data.stockers = Math.Max(0, data.stockers);
            if (double.IsNaN(data.incomePerSecond) || data.incomePerSecond < 0) data.incomePerSecond = 0;
            foreach (var b in data.built) b.level = Math.Max(1, b.level);
        }

        /// <summary>Keeps a copy of an unusable save so a failed restore never destroys the player's progress.</summary>
        public void Backup()
        {
            if (_storage.Exists) _storage.Backup();
        }

        public void Delete() => _storage.Delete();
    }
}
