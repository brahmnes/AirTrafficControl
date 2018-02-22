using System.Collections.Concurrent;

namespace airplanesvc
{
    public class AirplaneRepository: ConcurrentDictionary<string, Airplane>
    {
    }
}
