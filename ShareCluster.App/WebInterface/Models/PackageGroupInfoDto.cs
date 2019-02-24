using System.Collections.Generic;

namespace ShareCluster.WebInterface.Models
{
    public class PackageGroupInfoDto
    {
        public string GroupIdShort { get; set; }
        public string GroupId { get; set; }
        public IEnumerable<PackageInfoDto> Packages { get; set; }
    }
}
