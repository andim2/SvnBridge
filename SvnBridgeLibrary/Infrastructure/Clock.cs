using System;

namespace SvnBridge.Infrastructure
{
    public static class Clock
    {
        public static DateTime? FrozenCurrentTime;

        public static DateTime Now
        {
            get { return FrozenCurrentTime ?? DateTime.Now; }
        }
    }
}