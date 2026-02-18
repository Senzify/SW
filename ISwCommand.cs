using SolidWorks.Interop.sldworks;

namespace VasilevTools.Commands
{
    public interface ISwCommand
    {
        string Name { get; }
        string Description { get; }
        void Execute(ISldWorks swApp);
    }
}
