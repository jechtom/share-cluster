namespace ShareCluster.Network
{
    public interface IPeerCatalogUpdater
    {
        void StopScheduledUpdates();
        void ScheduleUpdateFromPeer(PeerInfo peer);
    }
}
