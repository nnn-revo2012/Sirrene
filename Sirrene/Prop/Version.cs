using System.Reflection;

namespace Sirrene.Prop
{
    public class Ver
    {
        public static readonly string Version = "0.1.0.02";
        public static readonly string VerDate = "2023/06/19";

        public static string GetFullVersion()
        {
            return GetAssemblyName() + " Ver " + Version;
        }

        public static string GetAssemblyName()
        {
            var assembly = Assembly.GetExecutingAssembly().GetName();
            return assembly.Name;
        }
    }
}
