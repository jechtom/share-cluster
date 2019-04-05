namespace ShareCluster.WebInterface.Models
{
    public class TaskDto
    {
        public int Id { get; set; }
        public bool IsSuccess { get; set; }
        public bool IsFaulted { get; set; }
        public bool IsRunning { get; set; }
        public string Title { get; set; }
    }
}
