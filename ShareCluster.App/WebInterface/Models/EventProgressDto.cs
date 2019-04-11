namespace ShareCluster.WebInterface.Models
{
    public class EventProgressDto
    {
        public string PackageId { get; set; }
        public string DownloadSpeedFormatted { get; set; }
        public string UploadSpeedFormatted { get; set; }
        public int ProgressPercent { get; set; }
        public string ProgressFormatted { get; set; }
    }
}
