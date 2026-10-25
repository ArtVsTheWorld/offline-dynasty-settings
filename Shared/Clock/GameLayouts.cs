using System.Collections.Generic;
namespace DynastyPlayClock
{
    public sealed partial class DynastyPlayClockAction
    {
        private static Layout[] GetLayouts()
        {
            return new Layout[] {
                new Layout("September 2026", Regions, RuntimeOriginal, new Dictionary<long, long>()),
                new Layout("October 13, 2026 update", new Region[] {
                    new Region("Clock list", 0x7993aa0, 0x15c0, "e7bdc4d9a6d877daab1f48e46ddebcc94191d8e36b8b7c20b19bbeb672df7a0a"),
                    new Region("Dynasty writer", 0x79a2090, 0x2d4d, "2b05fce6990b117820e0b08b4fbf5db4adf37d7cd46bafd988b151ce97f8e5f2"),
                    new Region("Choice serializer", 0x799be70, 0x2a2, "46f4460b9d7ec4dc67e7ae7a3a11a92fc7fc9a45bd64e47e55cfa5f729003c2a"),
                    new Region("Profile accessor", 0x27a7910, 0x42, "b72e85baf81e92d9c246622dad7c77cd47f3871dbe3ebf2ea8d38629a7c79c87"),
                    new Region("Main-menu enum getter", 0x7f2fca0, 0x4f, "7e0e831302a3b5080bdfc8919673be4b77134c3db2ffe4c830304268dd1685fb"),
                    new Region("Main-menu enum setter", 0x7f40470, 0x54, "4fc01de2abe3e8ce2e5b72a84d50a5f649dc88646d6e733f78c492239c931aa6"),
                    new Region("Profile getter", 0x27a9b10, 0x36, "8cace7e0291664aba817f203923cf8258b2d5c8a94c7989e4398ab7da85c3c88"),
                    new Region("Profile setter", 0x27b26f0, 0x49, "b9a5c899bdf47a9009969b014b8fbd3bebab0bafeb371dc2dba36ca0d7611213"),
                    new Region("Enum metadata", 0x5cf5910, 0x1d, "c88aea993b9929bb03ceb1884b8c64148265608a4091b794fbfa662b197ffe6a"),
                    new Region("Native Play Clock decision", 0x35e336e, 0x12f, "5759f74d31a6c35e3dc402c3f1a50e07d8fcb4bb4729d25a9840030ffa4f9189"),
                    new Region("Remaining-clock setter", 0x5d02690, 0x99, "a7f744c3b7d25ab3b69a19db0f93ce10a9bd925814d0c01aa21209fb4505ddb1"),
                    new Region("Clock transition dispatcher", 0x6455cb0, 0x3c, "3d63926963ea64ee1ff059bb65db9a77aa434a8789a78f8862c75af028d34876"),
                    new Region("Clock transition reset", 0x6469a50, 0x81, "a6531242282583ece301d958c10fcf864b680109bb274b23db0ffee3328640e5"),
                    new Region("CPU snap cadence", 0x3608ea0, 0xcc, "4cbc4b4115488d6334b831c2d1c4d710cacebbfbba3309ba1b0a1149f4e89121"),
                    new Region("Play-clock upper limit", 0x5d83e7a, 0x31, "aba3a0f6745bb8da52b4bd631b96d6a549670b2fa1bb0631dd7ea3f959efe7c7"),
                    new Region("Clock duration reset", 0x5d02340, 0x9e, "65903f8efb248f5815afa40ef001a0e467699752c33be86df87fe8bf8792ab6c"),
                    new Region("Match enum getter", 0x5cf5d60, 0x10, "4a94c23be822b90127b5ce6c14f9a13ddcda763bd3d4442ae3382f410e3d6792"),
                    new Region("Match type predicate", 0x3009b70, 0x10, "c30bc95384a7c1b5b89b00e708e9667120a818f8fa06e52cbd6e9628734e23ca"),
                    new Region("Legacy league mode predicate", 0x7ee1fb0, 0x45, "48c553b0030661e62543ef8fdd4f5ad41155a6dac5c2a8f017b8e12478c49329"),
                }, Hex("488b0d3740ce0aba01000000e89d297102"), new Dictionary<long, long> {
                    { 0x79a76d0, 0x7993aa0 },
                    { 0x79a79ce, 0x7993d9e },
                    { 0x79a7a9c, 0x7993e6c },
                    { 0x79b5d53, 0x79a2123 },
                    { 0x79b850d, 0x79a48dd },
                    { 0x79afaa0, 0x799be70 },
                    { 0x27ac630, 0x27a7910 },
                    { 0xac9aed0, 0xad8fec0 },
                    { 0x35e7592, 0x35e33b2 },
                    { 0xe1963e0, 0xe2c73f0 },
                    { 0x5d0e630, 0x5d02690 },
                    { 0x6461440, 0x6455cd0 },
                    { 0x360d0b0, 0x3608ee0 },
                    { 0x5d8fc30, 0x5d83e7a },
                    { 0x5d0db10, 0x5d01b70 },
                    { 0x5d0e2e0, 0x5d02340 },
                    { 0xf7261f8, 0xf8572b8 },
                    { 0x5d01d50, 0x5cf5d60 },
                    { 0xd8b4808, 0xd9e3818 },
                    { 0xf2c4d48, 0xf3f5d48 },
                    { 0xe1963d0, 0xe2c73e0 },
                    { 0xe1963d8, 0xe2c73e8 },
                    { 0xb67e30c, 0xb772b9c },
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
