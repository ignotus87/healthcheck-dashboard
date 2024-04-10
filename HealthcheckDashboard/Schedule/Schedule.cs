using System;

namespace HealthcheckDashboard.ScheduleNS
{
    public class Schedule
    {
        public TimeSpan TimeSpan { get; private set; }

        public Schedule(TimeSpan timeSpan)
        {
            TimeSpan = timeSpan;
        }
    }
}
