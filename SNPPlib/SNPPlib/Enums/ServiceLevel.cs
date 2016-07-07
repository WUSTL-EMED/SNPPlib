using System;

namespace SNPPlib
{
    public enum ServiceLevel : byte
    {
        Priority = 0,
        Normal = 1,//default priority if not specified for a set of commands
        FiveMinutes = 2,
        FifteenMinutes = 3,
        OneHour = 4,
        FourHours = 5,
        TwelveHours = 6,
        TwentyFourHours = 7,
        CarrierSpecific1 = 8,
        CarrierSpecific2 = 9,
        CarrierSpecific3 = 10,
        CarrierSpecific4 = 11
    }
}