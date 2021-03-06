﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NugetUtility
{
    /// <summary>
    /// More information at https://spdx.org/licenses/
    /// </summary>
    public class LicenseToUrlMappings : Dictionary<string, string>
    {
        private static readonly string Apache2_0 = "Apache-2.0";
        private static readonly string GPL2_0 = "GPL-2.0";
        private static readonly string AGPL3_0 = "AGPL-3.0";
        private static readonly string MIT = "MIT";
        private static readonly string BSD = "BSD";
        private static readonly string MS_PL = "MS-PL";
        private static readonly string MS_EULA = "MS-EULA";
        private static readonly string MS_EULA_Non_Redistributable = "MS-EULA-Non-Redistributable";

        public LicenseToUrlMappings() : base(ProtocolIgnorantOrdinalIgnoreCase.Default) { }

        public LicenseToUrlMappings(IDictionary<string, string> dictionary) : base(dictionary, ProtocolIgnorantOrdinalIgnoreCase.Default)
        {
        }

        private class ProtocolIgnorantOrdinalIgnoreCase : IEqualityComparer<string>
        {
            public static ProtocolIgnorantOrdinalIgnoreCase Default = new ProtocolIgnorantOrdinalIgnoreCase();

            public bool Equals([AllowNull] string x, [AllowNull] string y)
            {
                if (x is null || y is null) { return false; }

                return string.Compare(GetProtocolLessUrl(x), GetProtocolLessUrl(y), StringComparison.OrdinalIgnoreCase) == 0;
            }

            public int GetHashCode([DisallowNull] string obj) => GetProtocolLessUrl(obj).ToLower().GetHashCode();

            private static string GetProtocolLessUrl(string url) => url.Substring(url.IndexOf(':'));
        }

        public static LicenseToUrlMappings Default { get; } = new LicenseToUrlMappings
        {
            { "http://www.apache.org/licenses/LICENSE-2.0.html", Apache2_0 },
            { "http://www.apache.org/licenses/LICENSE-2.0", Apache2_0 },
            { "http://opensource.org/licenses/Apache-2.0", Apache2_0 },
            { "http://aws.amazon.com/apache2.0/", Apache2_0 },
            { "https://github.com/googleapis/gax-dotnet/raw/master/LICENSE", BSD},
            { "http://logging.apache.org/log4net/license.html", Apache2_0 },
            { "https://github.com/owin-contrib/owin-hosting/blob/master/LICENSE.txt", Apache2_0 },
            { "https://raw.githubusercontent.com/aspnet/Home/2.0.0/LICENSE.txt", Apache2_0 },
            { "https://raw.githubusercontent.com/aspnet/AspNetCore/2.0.0/LICENSE.txt", Apache2_0 },
            { "https://github.com/elastic/elasticsearch-net/raw/master/license.txt", Apache2_0 },
            { "https://github.com/Microsoft/Microsoft.IO.RecyclableMemoryStream/blob/master/LICENSE", MIT },
            { "https://github.com/AutoMapper/AutoMapper/blob/master/LICENSE.txt", MIT },
            { "https://github.com/zzzprojects/html-agility-pack/blob/master/LICENSE", MIT },
            { "https://raw.githubusercontent.com/hey-red/markdownsharp/master/LICENSE", MIT },
            { "https://raw.github.com/JamesNK/Newtonsoft.Json/master/LICENSE.md", MIT },
            { "https://raw.github.com/JamesNK/Newtonsoft.Json.Schema/master/LICENSE.md", AGPL3_0 },
            { "https://licenses.nuget.org/MIT", MIT },
            { "https://github.com/googleapis/google-api-dotnet-client/raw/master/LICENSE", Apache2_0 },
            { "https://github.com/google/google-api-dotnet-client/raw/master/LICENSE", Apache2_0 },
            { "https://numerics.mathdotnet.com/License.html", MIT },
            { "https://github.com/protocolbuffers/protobuf/raw/master/LICENSE", BSD },
            { "https://github.com/googleapis/google-cloud-dotnet/raw/master/LICENSE", Apache2_0 },
            { "https://github.com/dropbox/dropbox-sdk-dotnet/raw/master/dropbox-sdk-dotnet/LICENSE", MIT },
            { "https://github.com/DotNetSeleniumTools/DotNetSeleniumExtras/raw/master/LICENSE", Apache2_0 },
            { "http://opensource.org/licenses/MIT", MIT },
            { "https://github.com/huorswords/Microsoft.Extensions.Logging.Log4Net.AspNetCore/raw/master/LICENSE", Apache2_0},
            { "https://github.com/OfficeDev/ews-managed-api/raw/master/license.txt", MIT},
            { "http://www.opensource.org/licenses/mit-license.php", MIT },
            { "http://max.mit-license.org/", MIT },
            { "https://github.com/pauldambra/ModulusChecker/raw/master/MIT-LICENSE.txt", MIT},
            { "http://www.gnu.org/licenses/old-licenses/gpl-2.0.html", GPL2_0 },
            { "https://github.com/microsoft/ManagedEsent/blob/master/LICENSE.md", MIT },
            { "https://raw.githubusercontent.com/xunit/xunit.analyzers/master/LICENSE", Apache2_0},
            { "http://opensource.org/licenses/MS-PL", MS_PL },
            { "http://www.opensource.org/licenses/ms-pl", MS_PL },
            { "https://www.nuget.org/packages/NUnit/3.12.0/License", MIT},
            { "http://go.microsoft.com/fwlink/?LinkID=259741", MS_EULA},
            { "https://www.nuget.org/packages/Microsoft.Graph.Core/1.20.1/License", MIT},
            { "https://www.nuget.org/packages/Microsoft.Graph/3.8.0/License", MIT},
            { "https://www.microsoft.com/web/webpi/eula/aspnetmvc3update-eula.htm", MS_EULA },
            { "http://go.microsoft.com/fwlink/?LinkID=214339", MS_EULA },
            { "https://www.microsoft.com/web/webpi/eula/net_library_eula_enu.htm", MS_EULA },
            { "http://go.microsoft.com/fwlink/?LinkId=329770", MS_EULA },
            { "http://go.microsoft.com/fwlink/?LinkId=529443", MS_EULA },
            { "https://raw.githubusercontent.com/xunit/xunit/master/license.txt", Apache2_0 },
            { "https://www.microsoft.com/web/webpi/eula/dotnet_library_license_non_redistributable.htm", MS_EULA_Non_Redistributable  },
            { "http://go.microsoft.com/fwlink/?LinkId=529444", MS_EULA_Non_Redistributable  }
        };
    }
}
