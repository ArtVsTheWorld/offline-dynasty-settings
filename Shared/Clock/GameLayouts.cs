using System.Collections.Generic;
namespace DynastyPlayClock
{
    public sealed partial class DynastyPlayClockAction
    {
        private static Layout[] GetLayouts()
        {
            return new Layout[] {
                new Layout("September 2026", Regions, RuntimeOriginal, new Dictionary<long, long>()),
                new Layout("October 29, 2026 update", new Region[] {
                    new Region("Clock list", 0x7997640, 0x15c0, "9b3536b4f2add376575d1d6614946d59ec18246468283b28ef0f813fed601688"),
                    new Region("Dynasty writer", 0x79a5c30, 0x2d4d, "df5717e364c7381dd27a26745d1f71ba7079846ef184f4402a2823aeef84dc8c"),
                    new Region("Choice serializer", 0x799fa10, 0x2a2, "88bcf0b978e43c1f2fa0acbfb49cd3b2a29806e31fafd83a2e2becfb84dbb6c6"),
                    new Region("Profile accessor", 0x27a8710, 0x42, "e73de0614979d9e7796531aa360e8f945803baff2e0082c1bca433be75f507eb"),
                    new Region("Main-menu enum getter", 0x7f33870, 0x4f, "53eb9fc384850af5eaf3bacec0b405a9dd274ab0e90853ba649cbd04849ae5f6"),
                    new Region("Main-menu enum setter", 0x7f44040, 0x54, "1b2972aabc0ac793dd773457e8ca70f8d8b97440592a96d947bc0cef33882243"),
                    new Region("Profile getter", 0x27aa910, 0x36, "12fa11672fc926a706480646cc5fa013dcec2cd7f263ac544ef5129a3877f15f"),
                    new Region("Profile setter", 0x27b34f0, 0x49, "6f5bf186bab363ac3a575e7bf8f8cd6633296c385bbfb06003337f23fbeaf164"),
                    new Region("Enum metadata", 0x5cf93e0, 0x1d, "8ec64cf8ec928cf33d36e947e8070764fea5fc0ccc540fe2c7e969cac91e1481"),
                    new Region("Native Play Clock decision", 0x35e5aae, 0x12f, "86f17b2342a18614f9a88739d1bcbbeab405b40ab5a744e34ee36e52456ef3ea"),
                    new Region("Remaining-clock setter", 0x5d06160, 0x99, "b593b2fa139755ec7e9e25985ce60da5393042fb93417558001c696bc89bb372"),
                    new Region("Clock transition dispatcher", 0x6459840, 0x3c, "3d63926963ea64ee1ff059bb65db9a77aa434a8789a78f8862c75af028d34876"),
                    new Region("Clock transition reset", 0x646d5e0, 0x81, "a6531242282583ece301d958c10fcf864b680109bb274b23db0ffee3328640e5"),
                    new Region("CPU snap cadence", 0x360b5e0, 0xcc, "145e598ce68061978cf0d2c5ee916d06f96ac273707d12720ad0f437f58bdc82"),
                    new Region("Play-clock upper limit", 0x5d8794a, 0x31, "2304bf3fab985d6cfbdb248eec3c10c2d521a92319cd2629136ce0f13754984a"),
                    new Region("Clock duration reset", 0x5d05e10, 0x9e, "adde09d0b94a550fd8ade5dac8a513d3e57f19b1a6d0ccb15a62b1d3487746b0"),
                    new Region("Match enum getter", 0x5cf9830, 0x10, "4a94c23be822b90127b5ce6c14f9a13ddcda763bd3d4442ae3382f410e3d6792"),
                    new Region("Match type predicate", 0x3002a10, 0x10, "80aa4ff731824c4dac865ca0ad2c7547ccfc52d30c19b9fb363296b643488fa2"),
                    new Region("Legacy league mode predicate", 0x7ee5b50, 0x45, "6151f4b9a3b2d7a8722c2595a97f21f472536e917e7bb307336af3f6f75a7605"),
                }, Hex("488b0df798ce0aba01000000e82d3d7102"), new Dictionary<long, long> {
                    { 0x79a76d0, 0x7997640 },
                    { 0x79a79ce, 0x799793e },
                    { 0x79a7a9c, 0x7997a0c },
                    { 0x79b5d53, 0x79a5cc3 },
                    { 0x79b850d, 0x79a847d },
                    { 0x79afaa0, 0x799fa10 },
                    { 0x27ac630, 0x27a8710 },
                    { 0xac9aed0, 0xad960e0 },
                    { 0x35e7592, 0x35e5af2 },
                    { 0xe1963e0, 0xe2cf3f0 },
                    { 0x5d0e630, 0x5d06160 },
                    { 0x6461440, 0x6459860 },
                    { 0x360d0b0, 0x360b620 },
                    { 0x5d8fc30, 0x5d8794a },
                    { 0x5d0db10, 0x5d05640 },
                    { 0x5d0e2e0, 0x5d05e10 },
                    { 0xf7261f8, 0xf85f308 },
                    { 0x5d01d50, 0x5cf9830 },
                    { 0xd8b4808, 0xd9eb808 },
                    { 0xf2c4d48, 0xf3fddbc },
                    { 0xe1963d0, 0xe2cf3e0 },
                    { 0xe1963d8, 0xe2cf3e8 },
                    { 0xb67e30c, 0xb7792fc },
                })
            };
        }
        private sealed class Layout
        {
            public readonly string Name;
            public readonly Region[] Regions;
            public readonly byte[] RuntimeOriginal;
            private readonly Dictionary<long, long> addresses;
            public Layout(string name, Region[] regions, byte[] runtimeOriginal, Dictionary<long, long> addresses)
            { Name = name; Regions = regions; RuntimeOriginal = runtimeOriginal; this.addresses = addresses; }
            public long Map(long original) { long mapped; return addresses.TryGetValue(original, out mapped) ? mapped : original; }
        }
    }
}
