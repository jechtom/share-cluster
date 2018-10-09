namespace ShareCluster.Packaging
{
    public abstract class PackageBase : IPackage
    {
        public abstract Id PackageId { get; }
    }
}
