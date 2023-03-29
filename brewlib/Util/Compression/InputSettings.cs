namespace BrewLib.Util.Compression
{
    public class LosslessInputSettings
    {
        public string CustomInputArgs { get; set; }
        public int OptimizationLevel { get; set; }
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
        public const int 
            Level0 = 0, 
            Level1 = 1,
            Level2 = 2,
            Level3 = 3,
            Level4 = 4,
            Level5 = 5,
            Level6 = 6,
            Level7 = 7;
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