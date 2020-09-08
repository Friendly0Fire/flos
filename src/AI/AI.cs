using System.Linq;
using FLServer.Object;

namespace FLServer.AI
{
    //TODO: make interface to all AI-able objects (loadout, powergen, etc)

    public class AI
    {

        protected SimObject SelectTarget(Ship.Ship ship, DPGameRunner runner)
        {
            return runner.Objects.Values.Where(obj => obj != ship).FirstOrDefault(obj => obj is Ship.Ship && obj.Position.DistanceTo(ship.Position) < 5000);
        }

        public virtual void Update(Ship.Ship ship, DPGameRunner server, double seconds)
        {
            
        }
    }
}