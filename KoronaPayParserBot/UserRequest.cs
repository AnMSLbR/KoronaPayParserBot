using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KoronaPayParserBot
{
    internal class UserRequest
    {
        public string? Country { get; set; }
        public string? Currency { get; set; }
        public string? Amount { get; set; }
    }
}
