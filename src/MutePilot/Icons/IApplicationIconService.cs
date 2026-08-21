using System.Windows.Media;

namespace MutePilot.Icons;

public interface IApplicationIconService
{
    ImageSource GetIcon(string applicationKey, IReadOnlyList<int> processIds);
}
