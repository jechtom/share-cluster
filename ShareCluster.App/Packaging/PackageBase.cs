namespace ShareCluster.Packaging
{
    public abstract class PackageBase : IPackage
    {
        public abstract PackageId PackageId { get; }
    }
}
