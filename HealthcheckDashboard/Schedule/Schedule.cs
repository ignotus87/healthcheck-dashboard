using System;

namespace HealthcheckDashboard.Schedule
{
    class Schedule
    {
        public TimeSpan TimeSpan { get; private set; }

        public Schedule(TimeSpan timeSpan)
        {
            TimeSpan = timeSpan;
        }
    }
}
