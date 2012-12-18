using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Service 
{
    public class ScopedActivity : IDisposable
    {
        public TraceSource TraceSource { get; private set; }
        public Guid? PriorActivityId { get; private set; }
        public Guid ActivityId { get; private set; }
 
        public ScopedActivity(string name, bool transfer = true)
        {
            ActivityId = Guid.NewGuid();
            TraceSource = new TraceSource("EmailingSystem.Service", SourceLevels.All);

            //if transferring from previous activity...
            if (transfer)
            {
                //capture old activity id
                PriorActivityId = Trace.CorrelationManager.ActivityId;

                //tansfer to new activity id with new trace source
                TraceSource.TraceTransfer(0, "Transferring", ActivityId);
            }

            //set activity and trace start event
            Trace.CorrelationManager.ActivityId = ActivityId;
            TraceSource.TraceEvent(TraceEventType.Start, 0, name);
        }

        public void Log(string format, params object[] arguments)
        {
            this.TraceSource.TraceInformation(format, arguments); 
        }

        public void LogException(Exception ex, string format, params object[] arguments)
        {
            this.TraceSource.TraceEvent(TraceEventType.Error, 0, format, arguments);
            this.TraceSource.TraceData(TraceEventType.Error, 0, ex);
        }

        public void Dispose()
        {
            //stop this activity
            TraceSource.TraceEvent(TraceEventType.Stop, 0);

            if (PriorActivityId.HasValue)
            {
                //transfer to old activity
                TraceSource.TraceTransfer(0, "Transferring", PriorActivityId.Value);

                //update the activity for the correlation manager
                Trace.CorrelationManager.ActivityId = PriorActivityId.Value;
            }
        }
    }
}
