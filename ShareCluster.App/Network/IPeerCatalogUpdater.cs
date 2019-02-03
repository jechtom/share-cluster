namespace ShareCluster.Network
{
    public interface IPeerCatalogUpdater
    {
        void StopScheduledUpdates();
        void ScheduleUpdateFromPeer(PeerInfo peer);
        void ForgetPeer(PeerInfo peer);
    }
}
