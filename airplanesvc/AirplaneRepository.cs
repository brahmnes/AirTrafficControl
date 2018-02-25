using System.Collections.Concurrent;
using AirTrafficControl.Interfaces;

namespace airplanesvc
{
    public class AirplaneRepository: ConcurrentDictionary<string, Airplane>
    {
    }
}
