using System;
using System.Collections.Generic;
using System.Text;

namespace OrcaBot
{
    static class EliteHelper
    {
        private const double a = 49.84744;
        private const double b = 0.4114848;
        private const double c = 1042352000000;
        private const double d = 394885.5;

        public static TimeSpan SuperCruiseETTA(double distance)
        {
            return TimeSpan.FromSeconds(d + ((a - d) / (1 + Math.Pow(distance / c, b))));
        }

        public static TimeSpan JumpETTA(double distance, double jumprange, double jumprangefactor = 0.95)
        {
            return JumpETTA((int) Math.Ceiling(distance / (jumprange * jumprangefactor)));
        }

        public static TimeSpan JumpETTA(int jumpcount)
        {
            return TimeSpan.FromSeconds(jumpcount * 45);
        }
    }
}
