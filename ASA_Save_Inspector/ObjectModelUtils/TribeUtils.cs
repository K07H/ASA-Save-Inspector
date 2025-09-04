using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace ASA_Save_Inspector.ObjectModelUtils
{
    public static class TribeUtils
    {
        public static readonly List<string> DefaultSelectedColumns = new List<string>()
        {
            "TribeID",
            "TribeName",
            "OwnerPlayerDataID",
            "NumTribeDinos",
            "MembersPlayerDataID",
            "MembersPlayerName",
            "TribeMembers",
        };

        public static List<string> DefaultColumnsOrder = new List<string>()
        {
            "ID",
            "Name",
            "Owner ID",
            "Num dinos",
            "Members IDs",
            "Members names",
            "Members",
        };

        private static readonly Dictionary<string, string> CleanNames = new Dictionary<string, string>()
        {
            { "MembersPlayerDataID", "Members IDs" },
            { "MembersPlayerName", "Members names" },
            { "NumTribeDinos", "Num dinos" },
            { "OwnerPlayerDataID", "Owner ID" },
            { "TribeName", "Name" },
            { "TribeID", "ID" },
            { "TribeLog", "Logs" },
            { "TribeMembers", "Members" },
        };

        public static string? GetCleanNameFromPropertyName(string? propertyName) => (propertyName != null && CleanNames.ContainsKey(propertyName) ? CleanNames[propertyName] : propertyName);
        public static string? GetPropertyNameFromCleanName(string? cleanName) => Utils.GetPropertyNameFromCleanName(CleanNames, cleanName);
    }
}

namespace ASA_Save_Inspector.ObjectModel
{
    public partial class TribeMember
    {
        public override string ToString()
        {
            return $"ID={(PlayerDataID != null && PlayerDataID.HasValue ? PlayerDataID.Value.ToString(CultureInfo.InvariantCulture) : "0")} Name={(PlayerCharacterName != null ? PlayerCharacterName : string.Empty)} IsActive={(IsActive != null && IsActive.HasValue && IsActive.Value ? "True" : "False")}";
        }
    }

    public partial class Tribe : INotifyPropertyChanged
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067

        private string? _tribeLogsFormatted = null;
        public string? TribeLogsFormatted()
        {
            if (_tribeLogsFormatted != null)
                return _tribeLogsFormatted;
            string tribeLogs = "";
            if (TribeLog != null && TribeLog.Count > 0)
                foreach (var log in TribeLog)
                    if (log != null)
                        tribeLogs += $"{log}{Environment.NewLine}";
            _tribeLogsFormatted = tribeLogs;
            return _tribeLogsFormatted;
        }
    }
}
