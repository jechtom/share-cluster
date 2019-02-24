namespace ShareCluster.WebInterface.Models
{
    public class PackageInfoDto
    {
        public string GroupIdShort { get; set; }
        public long SizeBytes { get; set; }
        public string SizeFormatted { get; set; }
        public string IdShort { get; set; }
        public string Id { get; set; }
        public string KnownNames { get; set; }
        public long CreatedSortValue { get; set; }
        public string CreatedFormatted { get; set; }
        public int Seeders { get; set; }
        public int Leechers { get; set; }
    }
}
