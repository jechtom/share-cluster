namespace ShareCluster.Packaging.PackageFolders
{
    public interface IPackageFolderReference
    {
        string FolderPath { get; }
        Id Id { get; }
    }
}
