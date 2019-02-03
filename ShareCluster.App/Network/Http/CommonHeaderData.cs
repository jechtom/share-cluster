namespace ShareCluster.Network.Http
{
    public class CommonHeaderData
    {
        public CommonHeaderData(VersionNumber catalogVersion, PeerId peerId, bool isLoopback, string typeString)
        {
            CatalogVersion = catalogVersion;
            PeerId = peerId;
            IsLoopback = isLoopback;
            TypeString = typeString;
        }

        public VersionNumber CatalogVersion { get; }
        public PeerId PeerId { get; }
        public bool IsLoopback { get; set; }
        public string TypeString { get; }

        public bool TypeIsStream => TypeString == HttpCommonHeadersProcessor.TypeHeaderForStream;
    }
}
