namespace BrewLib.Util.Compression
{
    public class LosslessInputSettings
    {
        public string CustomInputArgs { get; set; }
        public string OptimizationLevel { get; set; }
    }
    public class LossyInputSettings
    {
        public string CustomInputArgs { get; set; }
        public int MinQuality { get; set; }
        public int MaxQuality { get; set; }
        public int Speed { get; set; }
    }
    public class OptimizationLevel
    {
        public const string 
            Level0 = "-o0", 
            Level1 = "-o1",
            Level2 = "-o2",
            Level3 = "-o3",
            Level4 = "-o4",
            Level5 = "-o5",
            Level6 = "-o6",
            Level7 = "-o7";
    }
    public class InputFormat
    {
        public const string 
            PNG = "png", 
            BMP = "bmp",
            GIF = "gif",
            PNM = "pnm",
            TIFF = "tiff";
    }
    public class UserCredential
    {
        public string UserName { get; private set; }
        public System.Security.SecureString Password { get; private set; }
        public string Domain { get; private set; }
    }
}