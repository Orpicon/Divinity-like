using NUnit.Framework;

namespace Divinity.Surfaces.Tests
{
    public class SurfaceRulesTests
    {
        static readonly SurfaceGridConfig Config = new SurfaceGridConfig
        {
            FireDuration = 3f,
            ElectricDuration = 1f,
            SteamDuration = 4f,
        };

        static Surface Cell(SurfaceType type) => SurfaceRules.Make(type, Config);

        [TestCase(SurfaceType.Fire, 3f)]
        [TestCase(SurfaceType.ElectrifiedWater, 1f)]
        [TestCase(SurfaceType.Steam, 4f)]
        [TestCase(SurfaceType.Water, 0f)]
        [TestCase(SurfaceType.Oil, 0f)]
        public void Make_SetsLifetimeFromConfig(SurfaceType type, float expected)
        {
            Assert.AreEqual(expected, SurfaceRules.Make(type, Config).Lifetime);
        }

        [TestCase(SurfaceType.ElectrifiedWater, SurfaceType.Water)]
        [TestCase(SurfaceType.Fire, SurfaceType.None)]
        [TestCase(SurfaceType.Steam, SurfaceType.None)]
        public void AfterExpire_ReturnsResidue(SurfaceType expired, SurfaceType expected)
        {
            Assert.AreEqual(expected, SurfaceRules.AfterExpire(expired));
        }

        [Test]
        public void FireBrush_OnWater_MakesSteam()
        {
            Assert.IsTrue(SurfaceRules.TryApplyBrush(SurfaceType.Fire, Cell(SurfaceType.Water), Config, 0f, out var result));
            Assert.AreEqual(SurfaceType.Steam, result.Type);
        }

        [Test]
        public void FireBrush_OnElectrifiedWater_MakesSteam()
        {
            Assert.IsTrue(SurfaceRules.TryApplyBrush(SurfaceType.Fire, Cell(SurfaceType.ElectrifiedWater), Config, 0f, out var result));
            Assert.AreEqual(SurfaceType.Steam, result.Type);
        }

        [TestCase(SurfaceType.None)]
        [TestCase(SurfaceType.Oil)]
        [TestCase(SurfaceType.Poison)]
        [TestCase(SurfaceType.Fire)]
        public void ElectricityBrush_NotOnWater_DoesNothing(SurfaceType ground)
        {
            var current = Cell(ground);
            Assert.IsFalse(SurfaceRules.TryApplyBrush(SurfaceType.ElectrifiedWater, current, Config, 0f, out var result));
            Assert.AreEqual(ground, result.Type);
        }

        [Test]
        public void ElectricityBrush_OnWater_Electrifies()
        {
            Assert.IsTrue(SurfaceRules.TryApplyBrush(SurfaceType.ElectrifiedWater, Cell(SurfaceType.Water), Config, 0f, out var result));
            Assert.AreEqual(SurfaceType.ElectrifiedWater, result.Type);
            Assert.AreEqual(Config.ElectricDuration, result.Lifetime);
        }

        [TestCase(SurfaceType.Water)]
        [TestCase(SurfaceType.Oil)]
        [TestCase(SurfaceType.Poison)]
        public void SamePermanentBrush_IsIgnored(SurfaceType type)
        {
            Assert.IsFalse(SurfaceRules.TryApplyBrush(type, Cell(type), Config, 0f, out _));
        }

        [Test]
        public void SameTemporaryBrush_RefreshesLifetime()
        {
            var burning = Cell(SurfaceType.Fire);
            burning.Lifetime = 0.5f;

            Assert.IsTrue(SurfaceRules.TryApplyBrush(SurfaceType.Fire, burning, Config, 0f, out var result));
            Assert.AreEqual(Config.FireDuration, result.Lifetime);
        }

        [Test]
        public void EraseBrush_ClearsSurface()
        {
            Assert.IsTrue(SurfaceRules.TryApplyBrush(SurfaceType.None, Cell(SurfaceType.Oil), Config, 0f, out var result));
            Assert.AreEqual(SurfaceType.None, result.Type);
        }

        [Test]
        public void ChangeTo_NewType_RemembersPreviousTypeAndTime()
        {
            var result = SurfaceRules.ChangeTo(Cell(SurfaceType.Oil), Cell(SurfaceType.Fire), 12f);

            Assert.AreEqual(SurfaceType.Oil, result.PreviousType);
            Assert.AreEqual(12f, result.ChangedAt);
        }

        [Test]
        public void ChangeTo_SameType_KeepsRunningTransition()
        {
            var current = SurfaceRules.ChangeTo(Cell(SurfaceType.Oil), Cell(SurfaceType.Fire), 5f);
            var result = SurfaceRules.ChangeTo(current, Cell(SurfaceType.Fire), 6f);

            Assert.AreEqual(SurfaceType.Oil, result.PreviousType);
            Assert.AreEqual(5f, result.ChangedAt);
        }
    }
}
