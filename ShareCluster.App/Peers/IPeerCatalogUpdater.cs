namespace ShareCluster.Peers
{
    public interface IPeerCatalogUpdater
    {
        void StopScheduledUpdates();
        void ScheduleUpdateFromPeer(PeerInfo peer);
    }
}
