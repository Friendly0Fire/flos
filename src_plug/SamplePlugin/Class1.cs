using System;
using FLServer.Plugins;

namespace SamplePlugin
{
    [PlugDisplayName("Employees")]
    [PlugDescription("This plug is for managing employee data")]
    public class EmployeePlug : Object, IFOSPlugin
    {
        public event LogHandler LogEvent;

        EmployeePlug()
        {
            if (LogEvent != null)
            {
                LogEvent("EmployeePlug loaded!");
            }
        }

        public bool Save(string Path)
        {
            return true;
        }
    }
}
