using System.Collections.Generic;

namespace ASA_Save_Inspector.ObjectModel
{

    public partial class TribeMember
    {
        public bool? IsActive { get; set; }
        public string? PlayerCharacterName { get; set; }
        public int? PlayerDataID { get; set; }
    }

    public partial class Tribe
    {
        public int? LogIndex { get; set; }
        public CustomList<int?>? MembersPlayerDataID { get; set; }
        public CustomList<string?>? MembersPlayerName { get; set; }
        public int? NumTribeDinos { get; set; }
        public int? OwnerPlayerDataId { get; set; }
        public int? TribeID { get; set; }
        public CustomList<string?>? TribeLog { get; set; }
        public CustomList<TribeMember?>? TribeMembers { get; set; }
        public string? TribeName { get; set; }
    }

}
