namespace ShareCluster.Packaging.PackageFolders
{
    public interface IPackageFolderReferenceWithId : IPackageFolderReference
    {
        Id PackageId { get; }
    }
}
