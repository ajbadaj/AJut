namespace AJut.Bench.Models
{
    using System;
    using System.Collections.Generic;

    // Deterministic payload generation. Same seed -> same payload across runs so benchmark
    // numbers are comparable. If you change a Build* method, regenerate the committed sample
    // .json files in Payloads/ so what's on disk matches what the benchmarks actually run on.
    public static class PayloadFactory
    {
        private const int kSeed = 4242;
        private const int kLargeCount = 1000;

        public static TinyMessage BuildTiny ()
        {
            var rand = new Random(kSeed);
            return new TinyMessage
            {
                SessionId = NewGuid(rand),
                SenderId = NewGuid(rand),
                MessageType = "ChatPost",
                Timestamp = new DateTime(2026, 5, 1, 14, 30, 0, DateTimeKind.Utc),
                Sequence = 73921,
                Payload = "Hello world, this is a representative wire-message payload string.",
                IsRetransmit = false,
                Priority = 2,
                Channel = "general",
                CorrelationId = NewGuid(rand),
            };
        }

        public static TinyMessageReflection BuildTinyReflection ()
        {
            var rand = new Random(kSeed);
            return new TinyMessageReflection
            {
                SessionId = NewGuid(rand),
                SenderId = NewGuid(rand),
                MessageType = "ChatPost",
                Timestamp = new DateTime(2026, 5, 1, 14, 30, 0, DateTimeKind.Utc),
                Sequence = 73921,
                Payload = "Hello world, this is a representative wire-message payload string.",
                IsRetransmit = false,
                Priority = 2,
                Channel = "general",
                CorrelationId = NewGuid(rand),
            };
        }

        public static DockZoneLayout BuildMedium ()
        {
            var rand = new Random(kSeed);
            return BuildZone(rand, depth: 0, maxDepth: 3);
        }

        public static List<DockZoneLayout> BuildLarge ()
        {
            var rand = new Random(kSeed);
            var list = new List<DockZoneLayout>(kLargeCount);
            for (int i = 0; i < kLargeCount; ++i)
            {
                list.Add(BuildZone(rand, depth: 0, maxDepth: 2));
            }

            return list;
        }

        // ===========[ Helpers ]===================================
        private static DockZoneLayout BuildZone (Random rand, int depth, int maxDepth)
        {
            var zone = new DockZoneLayout
            {
                ZoneId = "zone-" + rand.Next(0, 1_000_000).ToString("X"),
                Orientation = (rand.Next(0, 2) == 0) ? eDockOrientation.Horizontal : eDockOrientation.Vertical,
                SplitRatio = Math.Round(rand.NextDouble(), 4),
            };

            // Leaf zones get tabs, branch zones get children. Mix it up so we exercise both shapes.
            if (depth >= maxDepth || rand.Next(0, 10) < 3)
            {
                int tabCount = rand.Next(1, 5);
                for (int i = 0; i < tabCount; ++i)
                {
                    zone.Tabs.Add(BuildTab(rand, i));
                }
            }
            else
            {
                int childCount = rand.Next(2, 4);
                for (int i = 0; i < childCount; ++i)
                {
                    zone.Children.Add(BuildZone(rand, depth + 1, maxDepth));
                }
            }

            return zone;
        }

        private static TabInfo BuildTab (Random rand, int indexInZone)
        {
            var tab = new TabInfo
            {
                Title = "Tab " + indexInZone,
                ContentTypeId = "ContentType.Sample." + rand.Next(0, 50),
                IsActive = (indexInZone == 0),
            };

            int propCount = rand.Next(2, 6);
            for (int i = 0; i < propCount; ++i)
            {
                tab.Properties["prop_" + i] = "value_" + rand.Next(0, 10000);
            }

            return tab;
        }

        private static Guid NewGuid (Random rand)
        {
            byte[] bytes = new byte[16];
            rand.NextBytes(bytes);
            return new Guid(bytes);
        }
    }
}
