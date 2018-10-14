using ShareCluster.Packaging.IO;

namespace ShareCluster.Tests.IO
{
    public class TestControllerChangeItem
    {
        public TestControllerChangeItem(IStreamPart oldPart, IStreamPart newPart, int index)
        {
            OldPart = oldPart;
            NewPart = newPart;
            Index = index;
        }

        public IStreamPart OldPart { get; set; }
        public IStreamPart NewPart { get; set; }
        public int Index { get; set; }
    }
}
