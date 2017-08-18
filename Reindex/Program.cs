using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Reindex
{
    class Program
    {
        static void Main(string[] args)
        {
            var i = new Indexer();
            i.Run();
        }
    }
}
