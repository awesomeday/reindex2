using System.IO;

namespace Reindex
{
    public static class IndexTemplate
    {
        private static string tmplt = null;

        public static string Template
        {
            get
            {
                if (string.IsNullOrEmpty(tmplt))
                    GetFromFile();

                return tmplt;
            }
        }

        public static void GetFromFile()
        {
            tmplt = File.ReadAllText("tmplt.txt");
        }
    }
}
