using System;
using System.Collections.Generic;

namespace IdleMart.Save
{
    /// <summary>Everything persisted between sessions. Plain DTO for <see cref="UnityEngine.JsonUtility"/>.</summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Bump when the format changes and add a migration in <see cref="SaveService"/>.</summary>
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public long money;
        public int xp;
        public long savedAtUnix;
        public double incomePerSecond;
        public int stockers;
        public List<BuiltObjectData> built = new List<BuiltObjectData>();
        public List<string> unlockedZones = new List<string>();
    }

    /// <summary>An object placed in a build slot.</summary>
    [Serializable]
    public sealed class BuiltObjectData
    {
        public string slotId;
        public string buildableId;
        public int level;
        public bool hasCashier;
        /// <summary>Items on a shelf; -1 means "full" (also the default for older saves).</summary>
        public int stock = -1;
    }
}
