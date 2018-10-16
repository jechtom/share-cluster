namespace ShareCluster.Packaging.PackageFolders
{
    public interface IPackageFolderReference
    {
        string FolderPath { get; }
        PackageId Id { get; }
    }
}
