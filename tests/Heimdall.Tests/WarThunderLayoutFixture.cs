using Heimdall.Application;

namespace Heimdall.Tests
{
    /// <summary>Authored protocol shapes reproduce observed axis ordering and metadata without physical instance IDs or personal configuration.</summary>
    internal static class WarThunderLayoutFixture
    {
        #region BEGIN Public
        internal static ControllerObject Axis(string guid, int instance, uint offset) =>
            new(Guid.Parse(guid + "-c9f3-11cf-bfc7-444553540000"), (uint)(2 + instance * 256), "Synthetic axis") { Details = new(offset, 256) };
        internal static ControllerObject Button(int instance) =>
            new(Guid.Parse("a36d02f0-c9f3-11cf-bfc7-444553540000"), (uint)(4 + instance * 256), "Synthetic button") { Details = new((uint)(48 + instance), 0) };
        internal static ControllerObject Hat(int instance) =>
            new(Guid.Parse("a36d02f2-c9f3-11cf-bfc7-444553540000"), (uint)(16 + instance * 256), "Synthetic POV") { Details = new((uint)(32 + instance * 4), 256) };
        internal static ControllerObject[] Throttle()
        {
            var objects = Enumerable.Range(0, 21).Select(Button).ToList();
            objects.AddRange([Axis("a36d02e2", 2, 0), Axis("a36d02e1", 1, 4), Axis("a36d02e0", 0, 8),
                Axis("a36d02f5", 4, 12), Axis("a36d02f4", 3, 16)]);
            objects.AddRange(Enumerable.Range(21, 43).Select(Button));
            objects.Add(Axis("a36d02e3", 5, 24)); objects.Add(Axis("a36d02f3", 6, 28));
            return objects.ToArray();
        }
        internal static ControllerObject[] Stick()
        {
            var objects = Enumerable.Range(0, 21).Select(Button).ToList(); objects.Add(Hat(0));
            objects.AddRange([Axis("a36d02e4", 2, 4), Axis("a36d02e4", 6, 8), Axis("a36d02f4", 3, 12),
                Axis("a36d02f5", 4, 16), Axis("a36d02e3", 5, 20), Axis("a36d02e2", 7, 24),
                Axis("a36d02e1", 1, 28), Axis("a36d02e0", 0, 32), Axis("a36d02f3", 8, 36)]);
            return objects.ToArray();
        }
        #endregion END Public
    }
}
