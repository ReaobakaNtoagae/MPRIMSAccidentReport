using static CrashReport.Models.FixedEnum;

namespace CrashReport.Models
{
    public class FixedEnum
    {

        public enum InjurySeverity
        {
            Fatal,
            Serious,
            Slight,
            NoInjury
        }

        public enum PersonRole
        {
            Driver,
            Passenger,
            Pedestrian,
            Bicyclist
        }

        
    }
    public static class EnumDisplay
    {
        public static string Display(this InjurySeverity s) => s switch
        {

            InjurySeverity.Fatal => "Fatal",
            InjurySeverity.Serious => "Serious",
            InjurySeverity.Slight => "Slight",
            InjurySeverity.NoInjury => "No Injury",
            _ => s.ToString()

        };

        public static string Display(this PersonRole r) => r switch
        {
            PersonRole.Bicyclist => "Bicyclist",
            _ => r.ToString()
        };
    }

}
