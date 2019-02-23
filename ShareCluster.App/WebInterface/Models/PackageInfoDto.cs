namespace ShareCluster.WebInterface.Models
{
    public class PackageInfoDto
    {
        public long SizeBytes { get; set; }
        public string SizeFormatted { get; set; }
        public string IdShort { get; set; }
        public string Id { get; set; }
        public string KnownNames { get; set; }
    }
}
