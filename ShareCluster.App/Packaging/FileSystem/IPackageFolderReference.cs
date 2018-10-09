namespace ShareCluster.Packaging.FileSystem
{
    public interface IPackageFolderReference
    {
        string FolderPath { get; }
        Id Id { get; }
    }
}
